/* ==========================================================================
   AllianceDB — activity log
   --------------------------------------------------------------------------
   One row per change made in Apilan: who did it, what they did, to what,
   and exactly when (to the millisecond, the database server's clock). Sign-ins
   and sign-outs are recorded too. Rows are only ever added, never edited.

     occurred_at   when it happened, set by the database, not the browser
     account_id    the account that did it (NULL for a failed sign-in)
     actor         that account's username at the time, kept even if renamed
     action        a code such as MemberCreated, EventUpdated, MadeOfficer
     target_type   Member, Account, Event, EventType, Attendance, Session
     target_id     the row it was done to, when there is one
     target_label  a readable name for it at the time (a member's name, ...)
     details       what changed, e.g. "Venue: AVR → Lab 1"
     ip_address    where the request came from

   Run after 04_MemberAccounts.sql. Idempotent.
   ========================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.audit_logs', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.audit_logs (
        id            BIGINT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_audit_logs PRIMARY KEY,
        occurred_at   DATETIME2(3)   NOT NULL CONSTRAINT DF_audit_logs_occurred DEFAULT (SYSDATETIME()),
        account_id    INT            NULL CONSTRAINT FK_audit_logs_account REFERENCES dbo.accounts (id),
        actor         VARCHAR(100)   NOT NULL,
        action        VARCHAR(50)    NOT NULL,
        target_type   VARCHAR(30)    NULL,
        target_id     INT            NULL,
        target_label  NVARCHAR(255)  NULL,
        details       NVARCHAR(1000) NULL,
        ip_address    VARCHAR(45)    NULL
    );

    CREATE INDEX IX_audit_logs_occurred ON dbo.audit_logs (occurred_at DESC);
    CREATE INDEX IX_audit_logs_account  ON dbo.audit_logs (account_id, occurred_at DESC);
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_AuditLog_Add
    @account_id   INT            = NULL,
    @actor        VARCHAR(100),
    @action       VARCHAR(50),
    @target_type  VARCHAR(30)    = NULL,
    @target_id    INT            = NULL,
    @target_label NVARCHAR(255)  = NULL,
    @details      NVARCHAR(1000) = NULL,
    @ip_address   VARCHAR(45)    = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.audit_logs (account_id, actor, action, target_type, target_id, target_label, details, ip_address)
    VALUES (@account_id, @actor, @action, @target_type, @target_id, @target_label, @details, @ip_address);
END;
GO
