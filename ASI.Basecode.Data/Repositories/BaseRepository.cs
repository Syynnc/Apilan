using System;
using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Data layer, the last stop before SQL Server. Every repository reads
    /// through EF Core (tables and views as IQueryable, so the service's
    /// filters become the SQL WHERE clause) and writes by calling a stored
    /// procedure with <see cref="Execute"/>. Writes never go through
    /// SaveChanges: the procedures own validation, audit columns and triggers.
    /// </summary>
    public class BaseRepository
    {
        protected IUnitOfWork UnitOfWork { get; set; }

        protected AllianceDbContext Context => (AllianceDbContext)UnitOfWork.Database;

        public BaseRepository(IUnitOfWork unitOfWork)
        {
            UnitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        protected virtual DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
        {
            return Context.Set<TEntity>();
        }

        /// <summary>
        /// Runs a stored procedure. Business-rule violations raised inside the
        /// procedure (THROW 50xxx) surface to the caller as a SqlException.
        /// </summary>
        protected void Execute(FormattableString sql)
        {
            Context.Database.ExecuteSqlInterpolated(sql);
        }
    }
}
