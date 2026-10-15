/* ==========================================================================
   AllianceDB — late arrivals, required venues, year levels 1–5
   --------------------------------------------------------------------------
   Late is no longer a separate setting. An arrival is on time inside the
   time-in window (the start + the event's 15–30 minutes) and late after it:

     start ─ on time ─ start + window ─ late ─ end ─ time-out ─ end + window

   So time-in stays open after the window closes, until the event ends, and
   late_grace_minutes always equals attendance_window_minutes (the views that
   flag lateness read late_grace_minutes, so they need no change).

   Every event needs a venue.

   Year levels run 1 to 5; 0 means a non-blocked (irregular) student.

   Run after 05_ActivityLog.sql. Idempotent.
   ========================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---- Late = after the time-in window ------------------------------------ */
UPDATE dbo.events
   SET late_grace_minutes = attendance_window_minutes
 WHERE late_grace_minutes <> attendance_window_minutes;
GO

/* Events saved before venues were required get a placeholder to edit. */
UPDATE dbo.events SET venue = 'To be announced' WHERE venue IS NULL OR LTRIM(RTRIM(venue)) = '';
GO

CREATE OR ALTER PROCEDURE dbo.sp_Event_Create
    @event_name                VARCHAR(255),
    @event_type_id             INT,
    @event_start               DATETIME2(0),
    @event_end                 DATETIME2(0),
    @description               VARCHAR(MAX) = NULL,
    @venue                     VARCHAR(255) = NULL,
    @event_capacity            INT          = NULL,
    @event_host                VARCHAR(255) = NULL,
    @late_grace_minutes        INT          = NULL,   -- ignored: late is after the window
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

    IF @venue IS NULL OR LTRIM(RTRIM(@venue)) = ''
        THROW 50206, 'Venue is required.', 1;

    INSERT INTO dbo.events (event_name, description, venue, event_capacity, event_host,
                            event_type_id, event_start, event_end, late_grace_minutes,
                            attendance_window_minutes, created_by)
    VALUES (@event_name, @description, @venue, @event_capacity, @event_host,
            @event_type_id, @event_start, @event_end, @attendance_window_minutes,
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
    @late_grace_minutes        INT          = NULL,   -- ignored: late is after the window
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

    IF @venue IS NULL OR LTRIM(RTRIM(@venue)) = ''
        THROW 50206, 'Venue is required.', 1;

    UPDATE dbo.events
    SET event_name                = @event_name,
        description               = @description,
        venue                     = @venue,
        event_capacity            = @event_capacity,
        event_host                = @event_host,
        event_type_id             = @event_type_id,
        event_start               = @event_start,
        event_end                 = @event_end,
        late_grace_minutes        = @attendance_window_minutes,
        attendance_window_minutes = @attendance_window_minutes,
        updated_by                = @updated_by
    WHERE id = @id;

    SELECT * FROM dbo.events WHERE id = @id;
END;
GO

/* ---- Time in: on time in the window, late after it, closed at the end --- */
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

    DECLARE @uid INT, @start DATETIME2(0), @end DATETIME2(0), @cap INT, @new_id INT;
    SET @time_in = COALESCE(@time_in, SYSDATETIME());

    SELECT @uid = id
    FROM dbo.users
    WHERE is_active = 1
      AND ((@user_id IS NOT NULL AND id = @user_id)
        OR (@user_id IS NULL AND student_number = @student_number));

    IF @uid IS NULL
        THROW 50301, 'Student not found or inactive.', 1;

    SELECT @start = event_start, @end = event_end, @cap = event_capacity
    FROM dbo.events WHERE id = @event_id;

    IF @start IS NULL
        THROW 50302, 'Event not found.', 1;

    IF @time_in < @start
        THROW 50303, 'Time-in opens when the event starts.', 1;

    IF @time_in > @end
        THROW 50304, 'This event has already ended. Only time-outs are recorded now.', 1;

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

/* ---- Year levels: 0 (non-blocked) to 5 ---------------------------------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_users_year_level'
           AND definition NOT LIKE '%>=(0)%')
BEGIN
    UPDATE dbo.users SET year_level = 5 WHERE year_level > 5;
    ALTER TABLE dbo.users DROP CONSTRAINT CK_users_year_level;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_users_year_level')
    ALTER TABLE dbo.users ADD CONSTRAINT CK_users_year_level CHECK (year_level >= 0 AND year_level <= 5);
GO
