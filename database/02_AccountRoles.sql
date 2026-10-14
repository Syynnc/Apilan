/* ==========================================================================
   AllianceDB — changing an account's role
   --------------------------------------------------------------------------
   Officers can make a member an officer, and an officer a member again, from
   the Accounts page. The rules about who may do that (nobody but itself
   touches the initial officer, account 1; nobody demotes themselves) live in
   AccountService, beside the other account rules.

   Run after 01_RolesAndAccounts.sql. Idempotent.
   ========================================================================== */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Account_SetRole
    @id         INT,
    @role_id    INT,
    @updated_by VARCHAR(100) = 'system'
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.accounts WHERE id = @id)
        THROW 50404, 'Account not found.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.roles WHERE id = @role_id)
        THROW 50400, 'That role does not exist.', 1;

    UPDATE dbo.accounts SET role_id = @role_id, updated_by = @updated_by WHERE id = @id;
END;
GO
