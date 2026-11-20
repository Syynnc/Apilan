using System;
using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    /* Data layer, the last stop before SQL Server. Every repository reads
       through EF Core (tables and views as IQueryable, so the service's
       filters become the SQL WHERE clause) and writes by calling a stored
       procedure with Execute. Writes never go through
       SaveChanges: the procedures own validation, audit columns and triggers. */
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

        /* Runs a stored procedure. Business-rule violations raised inside the
           procedure (THROW 50xxx) surface to the caller as a SqlException. */
        protected void Execute(FormattableString sql)
        {
            Context.Database.ExecuteSqlInterpolated(sql);
        }
    }
}
