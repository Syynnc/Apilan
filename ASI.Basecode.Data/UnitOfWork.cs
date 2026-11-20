using System;
using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data
{
    // Unit of Work Implementation
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        // Gets the database context
        public DbContext Database { get; private set; }

        public UnitOfWork(AllianceDbContext serviceContext)
        {
            Database = serviceContext;
        }

        // Saves the changes to database
        public void SaveChanges()
        {
            Database.SaveChanges();
        }

        public void Dispose()
        {
            Database.Dispose();
        }
    }
}
