namespace ASI.Basecode.Data.Models
{
    // dbo.roles — Admin and Officer.
    public partial class Role
    {
        public int Id { get; set; }
        public string RoleName { get; set; }
        public string Description { get; set; }
    }

    // dbo.event_types — General Assembly, Seminar, Workshop, ...
    public partial class EventType
    {
        public int Id { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string Color { get; set; }
    }

    // dbo.attendance_modes — QR Code, Manual, RFID.
    public partial class AttendanceMode
    {
        public int Id { get; set; }
        public string ModeName { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }
}
