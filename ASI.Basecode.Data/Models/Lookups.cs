namespace ASI.Basecode.Data.Models
{
    /// <summary>dbo.roles — Admin and Officer.</summary>
    public partial class Role
    {
        public int Id { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>dbo.event_types — General Assembly, Seminar, Workshop, ...</summary>
    public partial class EventType
    {
        public int Id { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string Color { get; set; }
    }

    /// <summary>dbo.attendance_modes — QR Code, Manual, RFID.</summary>
    public partial class AttendanceMode
    {
        public int Id { get; set; }
        public string ModeName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
