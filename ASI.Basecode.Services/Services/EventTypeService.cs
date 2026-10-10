using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Messages;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;

namespace ASI.Basecode.Services.Services
{
    public class EventTypeService : ServiceBase, IEventTypeService
    {
        private readonly IEventTypeRepository _repository;
        private readonly IMapper _mapper;

        public EventTypeService(IEventTypeRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        public List<EventTypeViewModel> GetEventTypes()
        {
            return _repository.GetEventTypes()
                .OrderBy(t => t.TypeName)
                .ToList()
                .Select(t => _mapper.Map<EventTypeViewModel>(t))
                .ToList();
        }

        public EventTypeViewModel GetEventType(int id)
        {
            var type = _repository.GetEventTypes().FirstOrDefault(t => t.Id == id) ?? throw NotFound();
            return _mapper.Map<EventTypeViewModel>(type);
        }

        public void SaveEventType(EventTypeViewModel model)
        {
            if (model.Id == 0)
            {
                var type = _mapper.Map<EventType>(model);
                Run(() => _repository.AddEventType(type), Errors.EventTypeExists);
                return;
            }

            var existing = _repository.GetEventTypes().FirstOrDefault(t => t.Id == model.Id) ?? throw NotFound();
            _mapper.Map(model, existing);
            Run(() => _repository.UpdateEventType(existing), Errors.EventTypeExists);
        }
    }
}
