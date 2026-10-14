namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>An id/name pair for dropdowns (event types, roles, modes, students).</summary>
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
