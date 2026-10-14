namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// Password Manager. AllianceDB stores bcrypt hashes only — hashing and
    /// verification happen here, never in the database.
    /// </summary>
    public static class PasswordManager
    {
        private const int WorkFactor = 12;

        public static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            {
                return false;
            }

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // A placeholder or corrupt hash in the table is a failed login, not a crash.
                return false;
            }
        }
    }
}
