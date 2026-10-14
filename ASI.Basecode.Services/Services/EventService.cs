using System;
using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;

namespace ASI.Basecode.Services.Services
{
    public class EventService : ServiceBase, IEventService
    {
        private readonly IEventRepository _events;
        private readonly IEventTypeRepository _eventTypes;
        private readonly IAttendanceRepository _attendance;
        private readonly IMapper _mapper;

        public EventService(IEventRepository events, IEventTypeRepository eventTypes,
                            IAttendanceRepository attendance, IMapper mapper)
        {
            _events = events;
            _eventTypes = eventTypes;
            _attendance = attendance;
            _mapper = mapper;
        }

        public EventListViewModel GetEvents(string status, string search)
        {
            var all = _events.GetEventSummaries();

            var query = all;
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(e => e.EventStatus == status);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Name and venue — what an officer remembers an event by.
                var term = search.Trim();
                query = query.Where(e => e.EventName.Contains(term) || e.Venue.Contains(term));
            }

            var counts = all.GroupBy(e => e.EventStatus)
                            .Select(g => new { Status = g.Key, Count = g.Count() })
                            .ToDictionary(g => g.Status, g => g.Count);

            return new EventListViewModel
            {
                Status = status,
                Search = search,
                Events = query.OrderByDescending(e => e.EventStart).ToList(),
                UpcomingCount = counts.GetValueOrDefault(Const.StatusUpcoming),
                OngoingCount = counts.GetValueOrDefault(Const.StatusOngoing),
                FinishedCount = counts.GetValueOrDefault(Const.StatusFinished),
            };
        }

        public List<EventSummary> Suggest(string term, int take = 6)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<EventSummary>();
            }

            term = term.Trim();

            // Names that start with the term first, then the most recent.
            return _events.GetEventSummaries()
                .Where(e => e.EventName.Contains(term) || e.Venue.Contains(term))
                .OrderBy(e => e.EventName.StartsWith(term) ? 0 : 1)
                .ThenByDescending(e => e.EventStart)
                .Take(take)
                .ToList();
        }

        public EventDetailViewModel GetEventDetail(int id)
        {
            var summary = _events.GetEventSummaries().FirstOrDefault(e => e.EventId == id) ?? throw NotFound();
            var evt = _events.GetEvents().First(e => e.Id == id);
            var modes = _attendance.GetModes().Where(m => m.IsActive).OrderBy(m => m.Id).ToList();

            return new EventDetailViewModel
            {
                Summary = summary,
                Description = evt.Description,
                LateGraceMinutes = evt.LateGraceMinutes,
                Window = new AttendanceWindow(evt.EventStart, evt.EventEnd, evt.AttendanceWindowMinutes),
                Attendance = _attendance.GetAttendanceDetails()
                    .Where(a => a.EventId == id)
                    .OrderByDescending(a => a.TimeIn)
                    .ToList(),
                Absentees = _attendance.GetAbsentees()
                    .Where(a => a.EventId == id)
                    .OrderBy(a => a.FullName)
                    .ToList(),
                Modes = modes.Select(m => new LookupItem(m.Id, m.ModeName)).ToList(),
                Entry = new AttendanceEntryViewModel
                {
                    EventId = id,
                    AttendanceModeId = modes.Select(m => m.Id).FirstOrDefault(),
                },
            };
        }

        public EventViewModel GetEvent(int id)
        {
            var evt = _events.GetEvents().FirstOrDefault(e => e.Id == id) ?? throw NotFound();
            var model = _mapper.Map<EventViewModel>(evt);
            PopulateLookups(model);
            return model;
        }

        public EventViewModel NewEvent()
        {
            // Default to the next whole hour, two hours long.
            var start = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
            var model = new EventViewModel { EventStart = start, EventEnd = start.AddHours(2) };
            PopulateLookups(model);
            return model;
        }

        public void PopulateLookups(EventViewModel model)
        {
            // Inactive types stay listed only for an event that already uses one.
            model.EventTypes = _eventTypes.GetEventTypes()
                .Where(t => t.IsActive || t.Id == model.EventTypeId)
                .OrderBy(t => t.TypeName)
                .Select(t => new LookupItem(t.Id, t.TypeName))
                .ToList();
        }

        public void AddEvent(EventViewModel model, string createdBy)
        {
            var evt = _mapper.Map<Event>(model);
            Run(() => _events.AddEvent(evt, createdBy));
        }

        public void UpdateEvent(EventViewModel model, string updatedBy)
        {
            var evt = _mapper.Map<Event>(model);
            Run(() => _events.UpdateEvent(evt, updatedBy));
        }

        public void DeleteEvent(int id)
        {
            Run(() => _events.DeleteEvent(id));
        }
    }
}
