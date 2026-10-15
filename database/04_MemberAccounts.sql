/* ==========================================================================
   AllianceDB — every member has a sign-in, and so does the initial officer
   --------------------------------------------------------------------------
   New members get their account in the same step as their member record,
   but members added before that had none. This gives each of them one:

     username  = their student number (what they sign in with)
     password  = the configured default (Apilan@2026), so they are asked to
                 choose their own at the first sign-in
     role      = Member
     enabled   = the same as the member (a deactivated member stays shut out)

   Sign-in accepts a student ID or a username, nothing else, so the initial
   officer (account 1) gets a member record of its own with a student ID:
   "Alliance Officer", 2000-00001. Its username, password and officer role
   do not change; rename the member from the Members page whenever you like.

   Run after 03_AttendanceWindows.sql. Idempotent.
   ========================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

/* bcrypt (work factor 12) of the default password in appsettings.json,
   "Apilan@2026". If that setting changes, existing defaults still work until
   reset; only accounts made here use this hash. */
DECLARE @defaultHash VARCHAR(255) = '$2a$12$Nx9g8uOhN8VhX0EI9vL4b.cOF1ZCM6.55JhSOQ51KIrc4yxWSBzPu';
DECLARE @memberRole INT = (SELECT id FROM dbo.roles WHERE role_name = 'Member');

IF @memberRole IS NULL
    THROW 50400, 'The Member role is missing from dbo.roles. Run 01_RolesAndAccounts.sql first.', 1;

BEGIN TRANSACTION;

    /* ---- The initial officer's member record ---------------------------- */
    IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE student_number = '2000-00001')
        INSERT INTO dbo.users (student_number, first_name, last_name, course, year_level, is_active, created_by)
        VALUES ('2000-00001', 'Alliance', 'Officer', 'BSIT', 4, 1, 'setup');

    UPDATE dbo.accounts
       SET user_id = (SELECT id FROM dbo.users WHERE student_number = '2000-00001'),
           updated_by = 'setup'
     WHERE id = 1
       AND user_id IS NULL;

    /* ---- A sign-in for every member without one ------------------------- */
    INSERT INTO dbo.accounts (username, password_hash, role_id, user_id, is_active, created_by)
    SELECT u.student_number, @defaultHash, @memberRole, u.id, u.is_active, 'setup'
    FROM dbo.users u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.accounts a WHERE a.user_id = u.id)
      AND NOT EXISTS (SELECT 1 FROM dbo.accounts a WHERE a.username = u.student_number);

COMMIT TRANSACTION;

SELECT u.student_number, u.first_name + ' ' + u.last_name AS member,
       a.username, r.role_name, a.is_active
FROM dbo.users u
LEFT JOIN dbo.accounts a ON a.user_id = u.id
LEFT JOIN dbo.roles r    ON r.id = a.role_id
ORDER BY u.student_number;
