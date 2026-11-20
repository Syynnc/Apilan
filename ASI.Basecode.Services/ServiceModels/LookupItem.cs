namespace ASI.Basecode.Services.ServiceModels
{
    // An id/name pair for dropdowns (event types, roles, modes, students).
    public class LookupItem
    {
        public LookupItem(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }
        public string Name { get; }
    }
}
