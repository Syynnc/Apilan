using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    /* Events. Reads dbo.events (with its type) and the vw_EventAttendanceSummary
       view; writes go through sp_Event_Create / _Update / _Delete. The entity
       arrives already mapped from EventViewModel by EventService. */
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
            /* Interpolated SQL: EF sends each {value} as a SQL parameter, never
               pasted into the text, so form input can't inject SQL. */
            Execute($@"EXEC dbo.sp_Event_Create
                @event_name = {evt.EventName}, @event_type_id = {evt.EventTypeId},
                @event_start = {evt.EventStart}, @event_end = {evt.EventEnd},
                @description = {evt.Description}, @venue = {evt.Venue}, @event_capacity = {evt.EventCapacity},
                @event_host = {evt.EventHost},
                @attendance_window_minutes = {evt.AttendanceWindowMinutes}, @created_by = {createdBy}");
        }

        public void UpdateEvent(Event evt, string updatedBy)
        {
            Execute($@"EXEC dbo.sp_Event_Update @id = {evt.Id},
                @event_name = {evt.EventName}, @event_type_id = {evt.EventTypeId},
                @event_start = {evt.EventStart}, @event_end = {evt.EventEnd},
                @description = {evt.Description}, @venue = {evt.Venue}, @event_capacity = {evt.EventCapacity},
                @event_host = {evt.EventHost},
                @attendance_window_minutes = {evt.AttendanceWindowMinutes}, @updated_by = {updatedBy}");
        }

        public void DeleteEvent(int id)
        {
            Execute($"EXEC dbo.sp_Event_Delete @id = {id}");
        }
    }
}
