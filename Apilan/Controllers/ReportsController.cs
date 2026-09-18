using Apilan.Data;
using Apilan.Models;
using Apilan.Services;
using Apilan.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Controllers;

/// <summary>
/// Reports. Nothing here is entered by hand — every number is read back out of
/// the registrations and attendance records, which is the whole point of
/// taking attendance in the system rather than on a sheet.
/// </summary>
[Authorize(Policy = "StaffOnly")]
public class ReportsController : Controller
{
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly ApilanDbContext _db;

    public ReportsController(ApilanDbContext db) => _db = db;

    // ---- Participation across events --------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from, DateTime? to, int? typeId, string? course, int? organizationId)
    {
        var model = await BuildParticipationAsync(from, to, typeId, course, organizationId);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        DateTime? from, DateTime? to, int? typeId, string? course, int? organizationId)
    {
        var model = await BuildParticipationAsync(from, to, typeId, course, organizationId);
        var bytes = ReportWorkbook.Participation(model);

        var name = $"Apilan-participation-{model.From:yyyyMMdd}-{model.To:yyyyMMdd}.xlsx";
        return File(bytes, ExcelType, name);
    }

    // ---- One event's post-activity report ---------------------------------

    [HttpGet]
    public async Task<IActionResult> Event(int id)
    {
        var model = await BuildEventReportAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportEvent(int id)
    {
        var model = await BuildEventReportAsync(id);
        if (model is null)
        {
            return NotFound();
        }

        var bytes = ReportWorkbook.EventReport(model);

        var safeTitle = string.Concat(model.Event.Title.Split(Path.GetInvalidFileNameChars()));
        return File(bytes, ExcelType, $"Apilan-{safeTitle}-{model.Event.StartsAt:yyyyMMdd}.xlsx");
    }

    // ---- Building the numbers ---------------------------------------------

    private async Task<ParticipationReportViewModel> BuildParticipationAsync(
        DateTime? from, DateTime? to, int? typeId, string? course, int? organizationId)
    {
        // Default window: the current term, near enough — six months back.
        var start = (from ?? DateTime.Today.AddMonths(-6)).Date;
        var end = (to ?? DateTime.Today.AddMonths(1)).Date.AddDays(1).AddTicks(-1);

        var query = _db.Events
            .AsNoTracking()
            .Where(e => e.Status != EventStatus.Draft)
            .Where(e => e.StartsAt >= start && e.StartsAt <= end);

        if (typeId.HasValue)
        {
            query = query.Where(e => e.EventTypeId == typeId.Value);
        }

        if (organizationId.HasValue)
        {
            query = query.Where(e => e.OrganizationId == organizationId.Value);
        }

        // Filtering by course counts only that course's participants, so the
        // turnout figure answers "how did BSCS turn out", not "how full was it".
        var rows = await query
            .OrderBy(e => e.StartsAt)
            .Select(e => new EventParticipationRow
            {
                EventId = e.Id,
                Title = e.Title,
                TypeLabel = e.EventType!.Name,
                ColorKey = e.EventType.ColorKey,
                OrgAcronym = e.Organization!.Acronym,
                VenueName = e.Venue!.Name,
                StartsAt = e.StartsAt,
                Status = e.Status,
                Sessions = e.Sessions.Count,
                Registered = e.Registrations.Count(r =>
                    (r.Status == RegistrationStatus.Confirmed || r.Status == RegistrationStatus.Pending) &&
                    (course == null || r.Member!.Course == course)),
                Attended = e.Registrations.Count(r =>
                    r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent) &&
                    (course == null || r.Member!.Course == course)),
                LateArrivals = e.Registrations.Count(r =>
                    r.AttendanceRecords.Any(a => a.Status == AttendanceStatus.Late) &&
                    (course == null || r.Member!.Course == course)),
                WalkIns = e.Registrations.Count(r =>
                    r.IsWalkIn && (course == null || r.Member!.Course == course)),
                Certificates = e.Registrations.Count(r =>
                    (r.Eligibility == EligibilityStatus.Eligible || r.Eligibility == EligibilityStatus.OverrideGranted) &&
                    (course == null || r.Member!.Course == course)),
                Responses = e.Registrations.Count(r =>
                    r.Feedback != null && (course == null || r.Member!.Course == course)),
                AverageRating = e.Registrations
                    .Where(r => r.Feedback != null && (course == null || r.Member!.Course == course))
                    .Average(r => (double?)(
                        r.Feedback!.OverallRating +
                        r.Feedback.ContentRating +
                        r.Feedback.VenueRating +
                        r.Feedback.OrganizationRating) / 4.0)
            })
            .ToListAsync();

        return new ParticipationReportViewModel
        {
            Rows = rows,
            From = start,
            To = end.Date,
            TypeId = typeId,
            OrganizationId = organizationId,
            Course = course,
            Types = await _db.EventTypes
                .OrderBy(t => t.Name)
                .Select(t => new EventTypeOption(t.Id, t.Name, t.ColorKey))
                .ToListAsync(),
            Organizations = await _db.Organizations
                .OrderBy(o => o.Name)
                .Select(o => new OrganizationOption(o.Id, o.Name, o.Acronym, o.ColorKey))
                .ToListAsync(),
            Courses = await _db.Members
                .Select(m => m.Course)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync()
        };
    }

    private async Task<EventReportViewModel?> BuildEventReportAsync(int id)
    {
        var ev = await _db.Events
            .AsNoTracking()
            .Include(e => e.EventType)
            .Include(e => e.Venue)
            .Include(e => e.Organization)
            .Include(e => e.CreatedBy)
            .Include(e => e.LastEditedBy)
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (ev is null)
        {
            return null;
        }

        var sessions = await SessionSummaries.ForEventAsync(_db, id);

        var registrations = await _db.Registrations
            .AsNoTracking()
            .Where(r => r.EventId == id)
            .Include(r => r.Member)
            .Include(r => r.Feedback)
            .Include(r => r.AttendanceRecords)
            .OrderBy(r => r.Member!.LastName).ThenBy(r => r.Member!.FirstName)
            .ToListAsync();

        var participants = registrations
            .Where(r => r.Status != RegistrationStatus.Cancelled && r.Status != RegistrationStatus.Waitlisted)
            .Select(r => new ParticipantReportRow
            {
                MemberId = r.MemberId,
                ListName = r.Member!.ListName,
                StudentNumber = r.Member.StudentNumber,
                Course = r.Member.Course,
                YearLevel = r.Member.YearLevel,
                Section = r.Member.Section,
                Code = r.Code,
                Status = r.Status,
                IsWalkIn = r.IsWalkIn,
                Eligibility = r.Eligibility,
                Earned = AttendanceRules.IsEarned(r.Eligibility),
                Sessions = sessions.Count,
                FeedbackRating = r.Feedback is null ? null : r.Feedback.AverageRating,
                SessionCells = sessions.ToDictionary(
                    s => s.Id,
                    s => r.AttendanceRecords
                        .Where(a => a.EventSessionId == s.Id)
                        .Select(a => new SessionAttendanceCell(a.Status, a.TimeIn, a.TimeOut))
                        .FirstOrDefault() ?? SessionAttendanceCell.None)
            })
            .ToList();

        var feedback = registrations
            .Where(r => r.Feedback is not null)
            .Select(r => r.Feedback!)
            .ToList();

        // The report is a document that circulates, so an anonymous response
        // loses its name here rather than relying on the view to hide it.
        var voices = registrations
            .Where(r => r.Feedback is not null)
            .OrderByDescending(r => r.Feedback!.SubmittedOn)
            .Select(r => new FeedbackResponseRow
            {
                Id = r.Feedback!.Id,
                Who = r.Feedback.IsAnonymous ? "Anonymous" : r.Member!.ListName,
                CourseYear = r.Feedback.IsAnonymous ? null : $"{r.Member!.Course} · {r.Member.YearLevel}",
                OverallRating = r.Feedback.OverallRating,
                ContentRating = r.Feedback.ContentRating,
                VenueRating = r.Feedback.VenueRating,
                OrganizationRating = r.Feedback.OrganizationRating,
                WouldRecommend = r.Feedback.WouldRecommend,
                Comment = r.Feedback.Comment,
                Improvement = r.Feedback.Improvement,
                SubmittedOn = r.Feedback.SubmittedOn
            })
            .ToList();

        return new EventReportViewModel
        {
            Event = ev,
            Sessions = sessions,
            Participants = participants,
            Registered = participants.Count,
            Attended = participants.Count(p => p.Attended > 0),
            WalkIns = participants.Count(p => p.IsWalkIn),
            Certificates = participants.Count(p => p.Earned),
            Waitlisted = registrations.Count(r => r.Status == RegistrationStatus.Waitlisted),
            Cancelled = registrations.Count(r => r.Status == RegistrationStatus.Cancelled),
            FeedbackResponses = feedback.Count,
            Feedback = voices,
            FeedbackAverage = feedback.Any() ? feedback.Average(f => f.AverageRating) : null,
            FeedbackRecommendPercent = feedback.Any()
                ? (int)Math.Round(feedback.Count(f => f.WouldRecommend) * 100.0 / feedback.Count)
                : 0,
            ByCourse = Group(participants, p => p.Course),
            ByYear = Group(participants, p => $"Year {p.YearLevel}")
        };
    }

    private static List<BreakdownRow> Group(
        List<ParticipantReportRow> participants, Func<ParticipantReportRow, string> key) =>
        participants
            .GroupBy(key)
            .OrderBy(g => g.Key)
            .Select(g => new BreakdownRow
            {
                Label = g.Key,
                Registered = g.Count(),
                Attended = g.Count(p => p.Attended > 0)
            })
            .ToList();
}
