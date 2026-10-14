/* ==========================================================================
   AllianceDB — time-in and time-out windows
   --------------------------------------------------------------------------
   Every event has an attendance window of 15 to 30 minutes, and it opens
   by itself on the clock:

     Time-in   from the start  until start + window
     Time-out  from the end    until end   + window

   Outside those two windows the door records nothing. The window must fit
   inside the event, so the two never overlap, and "late after" has to fall
   inside the time-in window to mean anything.

   Before this, time-in opened an hour early and stayed open until the end,
   and time-out was accepted at any time.

   Run after 02_AccountRoles.sql. Idempotent.
   ========================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.events', 'attendance_window_minutes') IS NULL
BEGIN
    ALTER TABLE dbo.events
        ADD attendance_window_minutes INT NOT NULL
            CONSTRAINT DF_events_window DEFAULT (15);
END;
GO

/* Existing events: keep their late cut-off inside the window where it can be
   (a 30-minute grace gets a 30-minute window), otherwise the default. */
UPDATE dbo.events
   SET attendance_window_minutes =
        CASE WHEN late_grace_minutes > 30 THEN 30
             WHEN late_grace_minutes > 15 THEN late_grace_minutes
             ELSE 15 END
 WHERE attendance_window_minutes = 15
   AND late_grace_minutes > 15;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_events_window')
    ALTER TABLE dbo.events
        ADD CONSTRAINT CK_events_window CHECK (attendance_window_minutes BETWEEN 15 AND 30);
GO

/* ---- Events: create and update carry the window ------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_Event_Create
    @event_name                VARCHAR(255),
    @event_type_id             INT,
    @event_start               DATETIME2(0),
    @event_end                 DATETIME2(0),
    @description               VARCHAR(MAX) = NULL,
    @venue                     VARCHAR(255) = NULL,
    @event_capacity            INT          = NULL,
    @event_host                VARCHAR(255) = NULL,
    @late_grace_minutes        INT          = 15,
    @attendance_window_minutes INT          = 15,
    @created_by                VARCHAR(100) = 'system'
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.event_types WHERE id = @event_type_id AND is_active = 1)
        THROW 50201, 'Event type not found or inactive.', 1;

    IF @event_end <= @event_start
        THROW 50202, 'Event end must be later than event start.', 1;

    IF @attendance_window_minutes NOT BETWEEN 15 AND 30
        THROW 50204, 'The attendance window is 15 to 30 minutes.', 1;

    IF DATEDIFF(MINUTE, @event_start, @event_end) < @attendance_window_minutes
        THROW 50205, 'The event must last at least as long as its attendance window.', 1;

    INSERT INTO dbo.events (event_name, description, venue, event_capacity, event_host,
                            event_type_id, event_start, event_end, late_grace_minutes,
                            attendance_window_minutes, created_by)
    VALUES (@event_name, @description, @venue, @event_capacity, @event_host,
            @event_type_id, @event_start, @event_end, @late_grace_minutes,
            @attendance_window_minutes, @created_by);

    SELECT * FROM dbo.events WHERE id = SCOPE_IDENTITY();
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Event_Update
    @id                        INT,
    @event_name                VARCHAR(255),
    @event_type_id             INT,
    @event_start               DATETIME2(0),
    @event_end                 DATETIME2(0),
    @description               VARCHAR(MAX) = NULL,
    @venue                     VARCHAR(255) = NULL,
    @event_capacity            INT          = NULL,
    @event_host                VARCHAR(255) = NULL,
    @late_grace_minutes        INT          = 15,
    @attendance_window_minutes INT          = 15,
    @updated_by                VARCHAR(100) = 'system'
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.events WHERE id = @id)
        THROW 50203, 'Event not found.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.event_types WHERE id = @event_type_id AND is_active = 1)
        THROW 50201, 'Event type not found or inactive.', 1;

    IF @event_end <= @event_start
        THROW 50202, 'Event end must be later than event start.', 1;

    IF @attendance_window_minutes NOT BETWEEN 15 AND 30
        THROW 50204, 'The attendance window is 15 to 30 minutes.', 1;

    IF DATEDIFF(MINUTE, @event_start, @event_end) < @attendance_window_minutes
        THROW 50205, 'The event must last at least as long as its attendance window.', 1;

    UPDATE dbo.events
    SET event_name                = @event_name,
        description               = @description,
        venue                     = @venue,
        event_capacity            = @event_capacity,
        event_host                = @event_host,
        event_type_id             = @event_type_id,
        event_start               = @event_start,
        event_end                 = @event_end,
        late_grace_minutes        = @late_grace_minutes,
        attendance_window_minutes = @attendance_window_minutes,
        updated_by                = @updated_by
    WHERE id = @id;

    SELECT * FROM dbo.events WHERE id = @id;
END;
GO

/* ---- Time in: only inside the time-in window ---------------------------- */
/* Pass either @user_id or @student_number (e.g. from a QR scan). @time_in
   defaults to now; pass a value only for manual/backdated entries. */
CREATE OR ALTER PROCEDURE dbo.sp_Attendance_TimeIn
    @event_id           INT,
    @attendance_mode_id INT,
    @user_id            INT          = NULL,
    @student_number     VARCHAR(20)  = NULL,
    @time_in            DATETIME2(0) = NULL,
    @recorded_by        VARCHAR(100) = 'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @uid INT, @start DATETIME2(0), @window INT, @cap INT, @new_id INT, @closes DATETIME2(0);
    SET @time_in = COALESCE(@time_in, SYSDATETIME());

    SELECT @uid = id
    FROM dbo.users
    WHERE is_active = 1
      AND ((@user_id IS NOT NULL AND id = @user_id)
        OR (@user_id IS NULL AND student_number = @student_number));

    IF @uid IS NULL
        THROW 50301, 'Student not found or inactive.', 1;

    SELECT @start = event_start, @window = attendance_window_minutes, @cap = event_capacity
    FROM dbo.events WHERE id = @event_id;

    IF @start IS NULL
        THROW 50302, 'Event not found.', 1;

    SET @closes = DATEADD(MINUTE, @window, @start);

    IF @time_in < @start
        THROW 50303, 'Time-in opens when the event starts.', 1;

    IF @time_in > @closes
    BEGIN
        DECLARE @closed NVARCHAR(200) = CONCAT('Time-in closed at ', FORMAT(@closes, 'h:mm tt'), '.');
        THROW 50304, @closed, 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.attendance_modes WHERE id = @attendance_mode_id AND is_active = 1)
        THROW 50305, 'Attendance mode not found or inactive.', 1;

    BEGIN TRANSACTION;

        IF EXISTS (SELECT 1 FROM dbo.attendance WITH (UPDLOCK, HOLDLOCK)
                   WHERE user_id = @uid AND event_id = @event_id)
            THROW 50306, 'Student has already timed in for this event.', 1;

        IF @cap IS NOT NULL
           AND (SELECT COUNT(*) FROM dbo.attendance WITH (UPDLOCK, HOLDLOCK)
                WHERE event_id = @event_id) >= @cap
            THROW 50307, 'Event capacity reached.', 1;

        INSERT INTO dbo.attendance (user_id, event_id, attendance_mode_id, time_in, created_by)
        VALUES (@uid, @event_id, @attendance_mode_id, @time_in, @recorded_by);

        SET @new_id = SCOPE_IDENTITY();

    COMMIT TRANSACTION;

    SELECT * FROM dbo.vw_AttendanceDetails WHERE attendance_id = @new_id;
END;
GO

/* ---- Time out: only inside the time-out window -------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_Attendance_TimeOut
    @event_id       INT,
    @user_id        INT          = NULL,
    @student_number VARCHAR(20)  = NULL,
    @time_out       DATETIME2(0) = NULL,
    @recorded_by    VARCHAR(100) = 'system'
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @uid INT, @att_id INT, @in DATETIME2(0), @out DATETIME2(0),
            @end DATETIME2(0), @window INT, @closes DATETIME2(0);
    SET @time_out = COALESCE(@time_out, SYSDATETIME());

    SELECT @uid = id
    FROM dbo.users
    WHERE (@user_id IS NOT NULL AND id = @user_id)
       OR (@user_id IS NULL AND student_number = @student_number);

    IF @uid IS NULL
        THROW 50301, 'Student not found or inactive.', 1;

    SELECT @end = event_end, @window = attendance_window_minutes
    FROM dbo.events WHERE id = @event_id;

    IF @end IS NULL
        THROW 50302, 'Event not found.', 1;

    SET @closes = DATEADD(MINUTE, @window, @end);

    IF @time_out < @end
    BEGIN
        DECLARE @opens NVARCHAR(200) = CONCAT('Time-out opens when the event ends, at ', FORMAT(@end, 'h:mm tt'), '.');
        THROW 50311, @opens, 1;
    END;

    IF @time_out > @closes
    BEGIN
        DECLARE @closed NVARCHAR(200) = CONCAT('Time-out closed at ', FORMAT(@closes, 'h:mm tt'), '.');
        THROW 50312, @closed, 1;
    END;

    SELECT @att_id = id, @in = time_in, @out = time_out
    FROM dbo.attendance
    WHERE user_id = @uid AND event_id = @event_id;

    IF @att_id IS NULL
        THROW 50308, 'Student has not timed in for this event.', 1;

    IF @out IS NOT NULL
        THROW 50309, 'Student has already timed out for this event.', 1;

    IF @time_out < @in
        THROW 50310, 'Time-out cannot be earlier than time-in.', 1;

    UPDATE dbo.attendance
    SET time_out = @time_out, updated_by = @recorded_by
    WHERE id = @att_id;

    SELECT * FROM dbo.vw_AttendanceDetails WHERE attendance_id = @att_id;
END;
GO
