/* ==========================================================================
   AllianceDB — role realignment: Officer + Member
   --------------------------------------------------------------------------
   The application has two kinds of user, and no separate administrator:

     Officer — runs the console. Manages members, events and event types,
               records attendance at the door, and reads the reports.
     Member  — a student who attends events and can see their own attendance.

   AllianceDB originally shipped Admin (id 1) + Officer (id 2). This script
   renames those two rows in place — Admin's full access becomes the Officer's,
   and the old Officer row becomes Member — so no ids move and every existing
   account, attendance row and foreign key stays valid.

   It is idempotent: run it as many times as you like.
   Passwords here are bcrypt hashes of "Password123" — dev only; change them
   before this runs anywhere real.
   ========================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

/* Rename the old Officer row (id 2) to Member first, so renaming Admin (id 1)
   to Officer afterwards cannot collide on the unique role name. */
UPDATE dbo.roles
   SET role_name = 'Member',
       description = 'A student who attends events and can view their own attendance.'
 WHERE id = 2;

UPDATE dbo.roles
   SET role_name = 'Officer',
       description = 'Runs the console: manages members, events, and records attendance.'
 WHERE id = 1;

/* The bootstrap officer (was "admin") and one example member (was "officer1",
   linked to a student). Renamed for clarity; passwords reset to a known value. */
DECLARE @devHash VARCHAR(255) = '$2a$12$BEl4WESV38r3O.ZzpsJuTuof3JDumVijh8VFPpzU2tMIISoIn2SIK';

UPDATE dbo.accounts
   SET username = 'officer', password_hash = @devHash, role_id = 1, updated_by = 'setup'
 WHERE id = 1;

UPDATE dbo.accounts
   SET username = 'member', password_hash = @devHash, role_id = 2, updated_by = 'setup'
 WHERE id = 2;

SELECT a.id, a.username, r.role_name, a.user_id, a.is_active
FROM dbo.accounts a JOIN dbo.roles r ON r.id = a.role_id
ORDER BY a.id;
