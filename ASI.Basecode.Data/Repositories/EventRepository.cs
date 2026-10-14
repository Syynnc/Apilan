using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public class EventRepository : BaseRepository, IEventRepository
    {
        public EventRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<Event> GetEvents()
        {
            return this.GetDbSet<Event>().Include(e => e.EventType);
        }

        public IQueryable<EventSummary> GetEventSummaries()
        {
            return this.GetDbSet<EventSummary>();
        }

        public void AddEvent(Event evt, string createdBy)
        {
            Execute($@"EXEC dbo.sp_Event_Create
                @event_name = {evt.EventName}, @event_type_id = {evt.EventTypeId},
                @event_start = {evt.EventStart}, @event_end = {evt.EventEnd},
                @description = {evt.Description}, @venue = {evt.Venue}, @event_capacity = {evt.EventCapacity},
                @event_host = {evt.EventHost}, @late_grace_minutes = {evt.LateGraceMinutes}, @created_by = {createdBy}");
        }

        public void UpdateEvent(Event evt, string updatedBy)
        {
            Execute($@"EXEC dbo.sp_Event_Update @id = {evt.Id},
                @event_name = {evt.EventName}, @event_type_id = {evt.EventTypeId},
                @event_start = {evt.EventStart}, @event_end = {evt.EventEnd},
                @description = {evt.Description}, @venue = {evt.Venue}, @event_capacity = {evt.EventCapacity},
                @event_host = {evt.EventHost}, @late_grace_minutes = {evt.LateGraceMinutes}, @updated_by = {updatedBy}");
        }

        public void DeleteEvent(int id)
        {
            Execute($"EXEC dbo.sp_Event_Delete @id = {id}");
        }
    }
}
