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

        /// <summary>JSON for the live search box: the closest few events by name or venue.</summary>
        [HttpGet]
        public IActionResult Suggest(string term)
        {
            var items = _events.Suggest(term).Select(e => new
            {
                url = Url.Action(nameof(Details), new { id = e.EventId }),
                title = e.EventName,
                subtitle = string.Join(" · ", new[] { e.Venue, e.EventType }.Where(x => !string.IsNullOrWhiteSpace(x))),
                meta = e.EventStart.ToString("MMM d, yyyy"),
                badge = e.EventStart.ToString("MMM"),
                tone = Ui.TicketType(e.EventType),
            });
            return Json(items);
        }

        public IActionResult Details(int id)
        {
            try
            {
                return View(_events.GetEventDetail(id));
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
            if (!ModelState.IsValid)
            {
                _events.PopulateLookups(model);
                return FormView(model, invalid: true);
            }

            try
            {
                if (model.Id == 0)
                {
                    _events.AddEvent(model, CurrentUserName);
                }
                else
                {
                    _events.UpdateEvent(model, CurrentUserName);
                }

                TempData["Success"] = string.Format(Common.EventSaved, model.EventName);

                // An edit made from the event's own page goes back there.
                var back = model.Id != 0 ? Url.Action(nameof(Details), new { id = model.Id }) : Url.Action(nameof(Index));
                return SavedRedirect(back);
            }
            catch (InvalidDataException ex)
            {
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
                _events.DeleteEvent(id);
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
