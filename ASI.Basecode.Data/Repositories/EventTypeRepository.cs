using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>Event types have no stored procedures, so they go through EF directly.</summary>
    public class EventTypeRepository : BaseRepository, IEventTypeRepository
    {
        public EventTypeRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<EventType> GetEventTypes()
        {
            return this.GetDbSet<EventType>();
        }

        public void AddEventType(EventType eventType)
        {
            this.GetDbSet<EventType>().Add(eventType);
            UnitOfWork.SaveChanges();
        }

        public void UpdateEventType(EventType eventType)
        {
            this.GetDbSet<EventType>().Update(eventType);
            UnitOfWork.SaveChanges();
        }
    }
}
