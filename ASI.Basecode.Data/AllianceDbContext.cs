using System.Linq;
using System.Text.RegularExpressions;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data
{
    /// <summary>
    /// Database-first context over AllianceDB. The schema, triggers, views and
    /// stored procedures live in the database; this class only maps onto them.
    /// </summary>
    public partial class AllianceDbContext : DbContext
    {
        public AllianceDbContext()
        {
        }

        public AllianceDbContext(DbContextOptions<AllianceDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Account> Accounts { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<Event> Events { get; set; }
        public virtual DbSet<EventType> EventTypes { get; set; }
        public virtual DbSet<Attendance> Attendances { get; set; }
        public virtual DbSet<AttendanceMode> AttendanceModes { get; set; }

        // Views (read-only)
        public virtual DbSet<AttendanceDetail> AttendanceDetails { get; set; }
        public virtual DbSet<EventSummary> EventSummaries { get; set; }
        public virtual DbSet<StudentSummary> StudentSummaries { get; set; }
        public virtual DbSet<EventAbsentee> EventAbsentees { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Tables. The triggers are declared so EF does not use OUTPUT
            // clauses, which SQL Server rejects on tables with triggers.
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users", tb => tb.HasTrigger("trg_users_updated_time"));
                entity.HasIndex(e => e.StudentNumber).IsUnique();
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("accounts", tb => tb.HasTrigger("trg_accounts_updated_time"));
                entity.HasOne(e => e.Role).WithMany().HasForeignKey(e => e.RoleId);
                entity.HasOne(e => e.User).WithOne(u => u.Account).HasForeignKey<Account>(e => e.UserId);
            });

            modelBuilder.Entity<Role>().ToTable("roles");
            modelBuilder.Entity<EventType>().ToTable("event_types");
            modelBuilder.Entity<AttendanceMode>().ToTable("attendance_modes");

            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("events", tb =>
                {
                    tb.HasTrigger("trg_events_updated_time");
                    tb.HasTrigger("trg_events_capacity_check");
                });
                entity.HasOne(e => e.EventType).WithMany().HasForeignKey(e => e.EventTypeId);
            });

            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.ToTable("attendance", tb =>
                {
                    tb.HasTrigger("trg_attendance_updated_time");
                    tb.HasTrigger("trg_attendance_capacity");
                });
                entity.HasOne(e => e.User).WithMany(u => u.Attendances).HasForeignKey(e => e.UserId);
                entity.HasOne(e => e.Event).WithMany(ev => ev.Attendances).HasForeignKey(e => e.EventId);
                entity.HasOne(e => e.AttendanceMode).WithMany().HasForeignKey(e => e.AttendanceModeId);
            });

            // Views
            modelBuilder.Entity<AttendanceDetail>().HasNoKey().ToView("vw_AttendanceDetails");
            modelBuilder.Entity<EventSummary>().HasNoKey().ToView("vw_EventAttendanceSummary");
            modelBuilder.Entity<StudentSummary>().HasNoKey().ToView("vw_StudentAttendanceSummary");
            modelBuilder.Entity<EventAbsentee>().HasNoKey().ToView("vw_EventAbsentees");

            // Every column in AllianceDB is the snake_case of its property name.
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }

            OnModelCreatingPartial(modelBuilder);
        }

        private static string ToSnakeCase(string name) =>
            Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
