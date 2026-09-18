using Apilan.Data;
using Apilan.Models;
using Apilan.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Services;

/// <summary>
/// The per-session attendance tally, in one place.
///
/// The event screen, the door console, and the post-activity report all show
/// "how did this session go", and they have to agree to the person — an
/// officer comparing the door count against the report they file should never
/// find a difference. So they all read it from here.
/// </summary>
public static class SessionSummaries
{
    public static async Task<List<SessionSummary>> ForEventAsync(ApilanDbContext db, int eventId) =>
        await db.EventSessions
            .AsNoTracking()
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Sequence)
            .Select(s => new SessionSummary
            {
                Id = s.Id,
                Name = s.Name,
                Sequence = s.Sequence,
                StartsAt = s.StartsAt,
                EndsAt = s.EndsAt,
                LateAfterMinutes = s.LateAfterMinutes,
                Present = s.AttendanceRecords.Count(a => a.Status == AttendanceStatus.Present),
                Late = s.AttendanceRecords.Count(a => a.Status == AttendanceStatus.Late),
                Absent = s.AttendanceRecords.Count(a => a.Status == AttendanceStatus.Absent),
                StillIn = s.AttendanceRecords.Count(a =>
                    a.Status != AttendanceStatus.Absent && a.TimeOut == null)
            })
            .ToListAsync();
}
