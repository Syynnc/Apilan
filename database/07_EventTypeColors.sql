/* ==========================================================================
   AllianceDB — event type colours, no RFID
   --------------------------------------------------------------------------
   event_types.color holds one of the ticket palette keys (talk, workshop,
   assembly, training, service). NULL keeps the colour picked from the name.

   RFID is retired: the mode is switched off, so the door no longer offers
   it and sp_Attendance_TimeIn rejects it. Past RFID records keep their mode.

   Run after 06_LateArrivalsAndYears.sql. Idempotent.
   ========================================================================== */

SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.event_types', 'color') IS NULL
    ALTER TABLE dbo.event_types ADD color VARCHAR(20) NULL;
GO

UPDATE dbo.attendance_modes SET is_active = 0 WHERE mode_name = 'RFID' AND is_active = 1;
GO
