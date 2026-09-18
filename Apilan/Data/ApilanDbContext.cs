using Apilan.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Data;

public class ApilanDbContext : IdentityDbContext<ApplicationUser>
{
    public ApilanDbContext(DbContextOptions<ApilanDbContext> options) : base(options) { }

    public DbSet<Member> Members => Set<Member>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventSession> EventSessions => Set<EventSession>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<EventFeedback> EventFeedback => Set<EventFeedback>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---- Accounts -----------------------------------------------------
        b.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(u => u.StudentNumber).IsUnique().HasFilter("[StudentNumber] IS NOT NULL");

            e.HasOne(u => u.Member)
             .WithOne()
             .HasForeignKey<ApplicationUser>(u => u.MemberId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(u => u.ReviewedBy)
             .WithMany()
             .HasForeignKey(u => u.ReviewedByUserId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(u => u.Organization)
             .WithMany()
             .HasForeignKey(u => u.OrganizationId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ---- Master data --------------------------------------------------
        b.Entity<Member>(e =>
        {
            e.HasIndex(m => m.StudentNumber).IsUnique();
            e.HasIndex(m => new { m.LastName, m.FirstName });
        });

        b.Entity<EventType>().HasIndex(t => t.Name).IsUnique();
        b.Entity<Venue>().HasIndex(v => v.Name).IsUnique();

        b.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.Name).IsUnique();
            e.HasIndex(o => o.Acronym).IsUnique();
        });

        // ---- Events -------------------------------------------------------
        b.Entity<Event>(e =>
        {
            // Restrict: an organization with a history of events cannot be
            // deleted out from under them. Deactivate it instead.
            e.HasOne(x => x.Organization)
             .WithMany(o => o.Events)
             .HasForeignKey(x => x.OrganizationId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.EventType)
             .WithMany(t => t.Events)
             .HasForeignKey(x => x.EventTypeId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Venue)
             .WithMany(v => v.Events)
             .HasForeignKey(x => x.VenueId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedBy)
             .WithMany()
             .HasForeignKey(x => x.CreatedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            // NoAction rather than SetNull: CreatedBy already nulls itself out
            // when a user is removed, and SQL Server refuses a second SET NULL
            // path from the same principal table. Same reason ReviewedBy on
            // ApplicationUser is NoAction.
            e.HasOne(x => x.LastEditedBy)
             .WithMany()
             .HasForeignKey(x => x.LastEditedByUserId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasIndex(x => x.StartsAt);
            e.HasIndex(x => new { x.OrganizationId, x.StartsAt });
        });

        b.Entity<EventSession>(e =>
        {
            e.HasOne(s => s.Event)
             .WithMany(x => x.Sessions)
             .HasForeignKey(s => s.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => new { s.EventId, s.Sequence });
        });

        // ---- Registrations ------------------------------------------------
        b.Entity<Registration>(e =>
        {
            e.HasIndex(r => r.Code).IsUnique();

            // One member cannot register for the same event twice.
            e.HasIndex(r => new { r.EventId, r.MemberId }).IsUnique();

            e.HasOne(r => r.Event)
             .WithMany(x => x.Registrations)
             .HasForeignKey(r => r.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            // Restrict so a member with history cannot be silently deleted.
            e.HasOne(r => r.Member)
             .WithMany(m => m.Registrations)
             .HasForeignKey(r => r.MemberId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AttendanceRecord>(e =>
        {
            // A participant has at most one record per session.
            e.HasIndex(a => new { a.RegistrationId, a.EventSessionId }).IsUnique();

            e.HasOne(a => a.Registration)
             .WithMany(r => r.AttendanceRecords)
             .HasForeignKey(a => a.RegistrationId)
             .OnDelete(DeleteBehavior.Cascade);

            // NoAction: both paths lead back to Event, so cascading twice would
            // create multiple cascade paths and SQL Server rejects that.
            e.HasOne(a => a.EventSession)
             .WithMany(s => s.AttendanceRecords)
             .HasForeignKey(a => a.EventSessionId)
             .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(a => a.RecordedBy)
             .WithMany()
             .HasForeignKey(a => a.RecordedByUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(a => new { a.EventSessionId, a.Status });
        });

        // ---- Feedback -----------------------------------------------------
        b.Entity<EventFeedback>(e =>
        {
            // One response per registration: a participant speaks once.
            e.HasIndex(f => f.RegistrationId).IsUnique();

            e.HasOne(f => f.Registration)
             .WithOne(r => r.Feedback)
             .HasForeignKey<EventFeedback>(f => f.RegistrationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Audit --------------------------------------------------------
        b.Entity<AuditLog>(e =>
        {
            e.HasOne(a => a.Actor)
             .WithMany()
             .HasForeignKey(a => a.ActorUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(a => a.OccurredOn);
        });
    }
}
