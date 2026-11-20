using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Interfaces
{
    // Unit of Work Interface
    public interface IUnitOfWork
    {
        // Gets the database context
        DbContext Database { get; }

        // Saves the changes to database
        void SaveChanges();
    }
}
