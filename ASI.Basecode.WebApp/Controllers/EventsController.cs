using System.IO;
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
        public IActionResult Create() => View("Form", _events.NewEvent());

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                return View("Form", _events.GetEvent(id));
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
                return View("Form", model);
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
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                _events.PopulateLookups(model);
                return View("Form", model);
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
