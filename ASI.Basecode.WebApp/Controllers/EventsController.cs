using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ASI.Basecode.WebApp.Helpers;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    /* Officer screens for events. The request flow for every action is:
       browser (link, form or modal fetch) → this controller, which model-binds
       the query/form into a view model and checks ModelState → IEventService
       (business rules) → IEventRepository (EF Core reads, stored-procedure
       writes) → AllianceDB. The result comes back as a view model and is
       rendered by a Razor view, or as JSON for the modal and search scripts. */
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class EventsController : BaseController
    {
        private readonly IEventService _events;

        public EventsController(IEventService events)
        {
            _events = events;
        }

        public IActionResult Index(string status, string search)
        {
            return View(_events.GetEvents(status, search));
        }

        // JSON for the live search box: the closest few events by name or venue.
        [HttpGet]
        public IActionResult Suggest(string term)
        {
            var items = _events.Suggest(term).Select(e => new
            {
                url = Url.Action(nameof(Details), new { id = EventSlug.For(e.EventName, e.EventStart) }),
                title = e.EventName,
                subtitle = string.Join(" · ", new[] { e.Venue, e.EventType }.Where(x => !string.IsNullOrWhiteSpace(x))),
                meta = e.EventStart.ToString("MMM d, yyyy"),
                badge = e.EventStart.ToString("MMM"),
                tone = Ui.TicketType(e.EventType),
            });
            return Json(items);
        }

        // id: The event's slug (name and date), not its row id.
        public IActionResult Details(string id)
        {
            var eventId = _events.FindBySlug(id);
            if (eventId == null) return NotFound();

            try
            {
                return View(_events.GetEventDetail(eventId.Value));
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public IActionResult Create() => FormView(_events.NewEvent());

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                return FormView(_events.GetEvent(id));
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(EventViewModel model)
        {
            /* 1. MVC has bound the posted form fields into `model` and run its
                  [Required]/[Range] attributes and Validate(); failures land in
                  ModelState. On failure the form is sent back with its errors
                  (refilling the dropdowns, which are not posted). */
            if (!ModelState.IsValid)
            {
                _events.PopulateLookups(model);
                return FormView(model, invalid: true);
            }

            try
            {
                /* 2. The service maps the view model to an Event entity and the
                      repository calls the stored procedure. CurrentUserName (from
                      the sign-in cookie) is recorded as created_by / updated_by. */
                if (model.Id == 0)
                {
                    _events.AddEvent(model, CurrentUserName);
                    Audit(AuditActions.EventCreated, AuditActions.TargetEvent, null, model.EventName,
                        $"{model.EventStart:MMM d, yyyy h:mm tt} to {model.EventEnd:h:mm tt}"
                        + (string.IsNullOrWhiteSpace(model.Venue) ? "" : $" at {model.Venue}")
                        + $", {model.AttendanceWindowMinutes}-minute window");
                }
                else
                {
                    var before = _events.GetEvent(model.Id);
                    _events.UpdateEvent(model, CurrentUserName);

                    // The type is stored as an id; the log says its name.
                    var types = before.EventTypes.ToDictionary(t => (int?)t.Id, t => t.Name);
                    Audit(AuditActions.EventUpdated, AuditActions.TargetEvent, model.Id, model.EventName,
                        AuditDiff.Changes(before, model, new Dictionary<string, Func<object, string>>
                        {
                            [nameof(EventViewModel.EventTypeId)] = v => v is int id && types.TryGetValue(id, out var n) ? n : "—",
                        }));
                }

                /* 3. Saved: TempData carries the toast across the redirect, and
                      SavedRedirect answers the modal with JSON or a page with a 302. */
                TempData["Success"] = string.Format(Common.EventSaved, model.EventName);

                // An edit made from the event's own page goes back there.
                var back = model.Id != 0 ? Url.Action(nameof(Details), new { id = _events.GetSlug(model.Id) }) : Url.Action(nameof(Index));
                return SavedRedirect(back);
            }
            catch (InvalidDataException ex)
            {
                /* A rule the database enforced (THROW 50xxx in the procedure),
                   shown at the top of the form in the database's own words. */
                ModelState.AddModelError(string.Empty, ex.Message);
                _events.PopulateLookups(model);
                return FormView(model, invalid: true);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            try
            {
                var name = _events.GetEvent(id).EventName;
                _events.DeleteEvent(id);
                Audit(AuditActions.EventDeleted, AuditActions.TargetEvent, id, name);
                TempData["Success"] = Common.EventDeleted;
            }
            catch (InvalidDataException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
