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
    public class EventTypesController : BaseController
    {
        private readonly IEventTypeService _eventTypes;

        public EventTypesController(IEventTypeService eventTypes)
        {
            _eventTypes = eventTypes;
        }

        public IActionResult Index()
        {
            return View(_eventTypes.GetEventTypes());
        }

        [HttpGet]
        public IActionResult Create() => View("Form", new EventTypeViewModel());

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                return View("Form", _eventTypes.GetEventType(id));
            }
            catch (InvalidDataException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(EventTypeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Form", model);
            }

            try
            {
                _eventTypes.SaveEventType(model);
                TempData["Success"] = string.Format(Common.EventTypeSaved, model.TypeName);
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Form", model);
            }
        }
    }
}
