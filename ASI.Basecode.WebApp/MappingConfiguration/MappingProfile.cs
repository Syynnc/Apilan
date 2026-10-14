using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ServiceModels;
using AutoMapper;

namespace ASI.Basecode.WebApp.MappingConfiguration
{
    /// <summary>
    /// Entity ↔ view-model maps. The services map between the two in both
    /// directions, so every map is a ReverseMap. Unmapped destination members
    /// (navigation properties, audit columns, dropdown lists) are left at their
    /// defaults — the stored procedures own those columns, not the web layer.
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, StudentViewModel>().ReverseMap();
            CreateMap<Event, EventViewModel>().ReverseMap();
            CreateMap<EventType, EventTypeViewModel>().ReverseMap();
        }
    }
}
