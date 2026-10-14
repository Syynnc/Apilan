using System;
using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
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
