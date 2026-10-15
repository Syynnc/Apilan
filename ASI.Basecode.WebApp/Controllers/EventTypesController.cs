using ASI.Basecode.WebApp.Helpers;
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
        public IActionResult Create() => FormView(new EventTypeViewModel());

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                return FormView(_eventTypes.GetEventType(id));
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
                return FormView(model, invalid: true);
            }

            try
            {
                var before = model.Id != 0 ? _eventTypes.GetEventType(model.Id) : null;
                _eventTypes.SaveEventType(model);
                if (before == null)
                {
                    Audit(AuditActions.EventTypeCreated, AuditActions.TargetEventType, null, model.TypeName);
                }
                else
                {
                    Audit(AuditActions.EventTypeUpdated, AuditActions.TargetEventType, model.Id, model.TypeName,
                        AuditDiff.Changes(before, model));
                }
                TempData["Success"] = string.Format(Common.EventTypeSaved, model.TypeName);
                return SavedRedirect(Url.Action(nameof(Index)));
            }
            catch (InvalidDataException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return FormView(model, invalid: true);
            }
        }
    }
}
