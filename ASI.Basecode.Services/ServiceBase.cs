using System;
using System.IO;
using ASI.Basecode.Resources.Messages;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Services
{
    /// <summary>
    /// Service Base class implementation.
    /// AllianceDB enforces its own rules (duplicate student numbers, time-in
    /// windows, capacity, ...) and raises them with THROW 50xxx. Services run
    /// writes through <see cref="Run"/> so those arrive at the controller as an
    /// <see cref="InvalidDataException"/> carrying the database's own message.
    /// </summary>
    public class ServiceBase
    {
        private const int UserDefinedErrorStart = 50000;
        private const int UniqueIndexViolation = 2601;
        private const int UniqueConstraintViolation = 2627;

        protected static void Run(Action action, string duplicateMessage = null)
        {
            try
            {
                action();
            }
            catch (SqlException ex) when (ex.Number >= UserDefinedErrorStart)
            {
                throw new InvalidDataException(ex.Message, ex);
            }
            catch (Exception ex) when (IsDuplicate(ex))
            {
                throw new InvalidDataException(duplicateMessage ?? ex.GetBaseException().Message, ex);
            }
        }

        protected static InvalidDataException NotFound()
        {
            return new InvalidDataException(Errors.RecordNotFound);
        }

        private static bool IsDuplicate(Exception ex)
        {
            var sql = ex as SqlException ?? (ex as DbUpdateException)?.InnerException as SqlException;
            return sql != null && (sql.Number == UniqueIndexViolation || sql.Number == UniqueConstraintViolation);
        }
    }
}
