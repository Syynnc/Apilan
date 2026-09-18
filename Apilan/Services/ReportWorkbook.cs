using Apilan.Models;
using Apilan.ViewModels;
using ClosedXML.Excel;

namespace Apilan.Services;

/// <summary>
/// Builds the spreadsheets the officers hand in. A report is evidence, so
/// every sheet carries what it covers and when it was produced — a file that
/// cannot say what it is gets argued with later.
/// </summary>
public static class ReportWorkbook
{
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#16181F");
    private static readonly XLColor HeaderInk = XLColor.White;

    public static byte[] Participation(ParticipationReportViewModel report)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Participation");

        var scope = $"{report.From:d MMM yyyy} – {report.To:d MMM yyyy}"
            + (report.OrganizationId.HasValue
                ? $" · {report.Organizations.FirstOrDefault(o => o.Id == report.OrganizationId)?.Name}"
                : " · all organizations")
            + (report.TypeId.HasValue ? $" · {report.Types.FirstOrDefault(t => t.Id == report.TypeId)?.Name}" : "")
            + (string.IsNullOrWhiteSpace(report.Course) ? "" : $" · {report.Course}");

        var row = Title(sheet, "Participation report", scope);

        var headers = new[]
        {
            "Event", "Org", "Type", "Date", "Venue", "Sessions",
            "Registered", "Attended", "No-shows", "Turnout %", "Late arrivals",
            "Walk-ins", "Certificates", "Responses", "Rating", "Status"
        };

        row = Header(sheet, row, headers);

        foreach (var r in report.Rows)
        {
            sheet.Cell(row, 1).Value = r.Title;
            sheet.Cell(row, 2).Value = r.OrgAcronym;
            sheet.Cell(row, 3).Value = r.TypeLabel;
            sheet.Cell(row, 4).Value = r.StartsAt;
            sheet.Cell(row, 4).Style.DateFormat.Format = "dd mmm yyyy";
            sheet.Cell(row, 5).Value = r.VenueName;
            sheet.Cell(row, 6).Value = r.Sessions;
            sheet.Cell(row, 7).Value = r.Registered;
            sheet.Cell(row, 8).Value = r.Attended;
            sheet.Cell(row, 9).Value = r.NoShows;
            sheet.Cell(row, 10).Value = r.TurnoutPercent / 100.0;
            sheet.Cell(row, 10).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, 11).Value = r.LateArrivals;
            sheet.Cell(row, 12).Value = r.WalkIns;
            sheet.Cell(row, 13).Value = r.Certificates;
            sheet.Cell(row, 14).Value = r.Responses;
            Rating(sheet.Cell(row, 15), r.AverageRating);
            sheet.Cell(row, 16).Value = r.Status.ToString();
            row++;
        }

        if (report.Rows.Any())
        {
            var totals = sheet.Row(row);
            sheet.Cell(row, 1).Value = $"{report.Events} event(s)";
            sheet.Cell(row, 7).Value = report.Registered;
            sheet.Cell(row, 8).Value = report.Attended;
            sheet.Cell(row, 9).Value = report.Registered - report.Attended;
            sheet.Cell(row, 10).Value = report.TurnoutPercent / 100.0;
            sheet.Cell(row, 10).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, 11).Value = report.Rows.Sum(r => r.LateArrivals);
            sheet.Cell(row, 12).Value = report.WalkIns;
            sheet.Cell(row, 13).Value = report.Certificates;
            sheet.Cell(row, 14).Value = report.Rows.Sum(r => r.Responses);

            totals.Style.Font.Bold = true;
            totals.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(4);

        return Save(book);
    }

    /// <summary>
    /// The post-activity pack: the summary, the attendance matrix, and who
    /// earned a certificate — one file an adviser can read end to end.
    /// </summary>
    public static byte[] EventReport(EventReportViewModel report)
    {
        var ev = report.Event;

        using var book = new XLWorkbook();

        // ---- Summary ------------------------------------------------------
        var summary = book.AddWorksheet("Summary");
        var row = Title(summary, ev.Title, $"{ev.StartsAt:dddd, d MMMM yyyy} · {ev.Venue?.Name}");

        var facts = new (string Label, object Value)[]
        {
            ("Organization", ev.Organization?.Name ?? ""),
            ("Event type", ev.EventType?.Name ?? ""),
            ("Status", ev.Status.ToString()),
            ("Capacity", ev.Capacity),
            ("Sessions", report.Sessions.Count),
            ("Registered", report.Registered),
            ("Attended at least one session", report.Attended),
            ("No-shows", report.NoShows),
            ("Turnout", report.TurnoutPercent / 100.0),
            ("Average attendance", report.AverageAttendancePercent / 100.0),
            ("Walk-ins", report.WalkIns),
            ("Waitlisted", report.Waitlisted),
            ("Cancelled", report.Cancelled),
            ("Late to at least one session", report.LateArrivals),
            ("Certificate rule", ev.CertificateThresholdPercent / 100.0),
            ("Certificates earned", report.Certificates),
            ("Feedback responses", report.FeedbackResponses),
            ("Created by", ev.CreatedBy?.FullName ?? "—"),
            ("Last edited by", ev.LastEditedBy?.FullName ?? "—"),
            ("Last edited on", ev.LastEditedOn.HasValue
                ? ev.LastEditedOn.Value.ToString("d MMM yyyy, h:mm tt")
                : "—")
        };

        foreach (var (label, value) in facts)
        {
            summary.Cell(row, 1).Value = label;
            summary.Cell(row, 1).Style.Font.Bold = true;
            summary.Cell(row, 2).SetValue(XLCellValue.FromObject(value));

            if (label is "Turnout" or "Average attendance" or "Certificate rule")
            {
                summary.Cell(row, 2).Style.NumberFormat.Format = "0%";
            }

            row++;
        }

        row += 1;
        summary.Cell(row, 1).Value = "Per session";
        summary.Cell(row, 1).Style.Font.Bold = true;
        row++;

        row = Header(summary, row, new[]
        {
            "Session", "Starts", "Ends", "Late after", "Present", "Late", "Absent", "Still timed in"
        });

        foreach (var s in report.Sessions)
        {
            summary.Cell(row, 1).Value = s.Name;
            summary.Cell(row, 2).Value = s.StartsAt;
            summary.Cell(row, 2).Style.DateFormat.Format = "dd mmm yyyy hh:mm";
            summary.Cell(row, 3).Value = s.EndsAt;
            summary.Cell(row, 3).Style.DateFormat.Format = "dd mmm yyyy hh:mm";
            summary.Cell(row, 4).Value = $"{s.LateAfterMinutes} min";
            summary.Cell(row, 5).Value = s.Present;
            summary.Cell(row, 6).Value = s.Late;
            summary.Cell(row, 7).Value = s.Absent;
            summary.Cell(row, 8).Value = s.StillIn;
            row++;
        }

        row += 1;
        row = Breakdown(summary, row, "By course", report.ByCourse);
        row = Breakdown(summary, row, "By year level", report.ByYear);

        summary.Columns().AdjustToContents();

        // ---- Attendance matrix --------------------------------------------
        var sheet = book.AddWorksheet("Attendance");
        var line = Title(sheet, "Attendance", $"{ev.Title} · {report.Sessions.Count} session(s)");

        var headers = new List<string>
        {
            "Last, First", "Student no.", "Course", "Year", "Section", "Code", "Registration", "Walk-in"
        };
        headers.AddRange(report.Sessions.Select(s => s.Name));
        headers.AddRange(new[] { "Sessions attended", "Late", "Attendance %", "Certificate", "Rating" });

        line = Header(sheet, line, headers.ToArray());

        foreach (var p in report.Participants)
        {
            var column = 1;
            sheet.Cell(line, column++).Value = p.ListName;
            sheet.Cell(line, column++).Value = p.StudentNumber;
            sheet.Cell(line, column++).Value = p.Course;
            sheet.Cell(line, column++).Value = p.YearLevel;
            sheet.Cell(line, column++).Value = p.Section ?? "";
            sheet.Cell(line, column++).Value = p.Code;
            sheet.Cell(line, column++).Value = p.Status.ToString();
            sheet.Cell(line, column++).Value = p.IsWalkIn ? "Yes" : "";

            foreach (var s in report.Sessions)
            {
                var entry = p.SessionCells.TryGetValue(s.Id, out var value)
                    ? value
                    : SessionAttendanceCell.None;

                var cell = sheet.Cell(line, column++);

                // The arrival time is the useful figure when somebody turned
                // up; the verdict is the useful one when they did not.
                if (entry.Attended && entry.TimeIn.HasValue)
                {
                    cell.Value = entry.TimeIn.Value;
                    cell.Style.DateFormat.Format = "hh:mm AM/PM";

                    if (entry.Status == AttendanceStatus.Late)
                    {
                        cell.Style.Font.FontColor = XLColor.FromHtml("#8A5A00");
                        cell.Style.Font.Bold = true;
                    }
                }
                else if (entry.Status == AttendanceStatus.Absent)
                {
                    cell.Value = "Absent";
                    cell.Style.Font.FontColor = XLColor.FromHtml("#9B1C1C");
                }
                else
                {
                    cell.Value = "—";
                    cell.Style.Font.FontColor = XLColor.Gray;
                }

                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            sheet.Cell(line, column++).Value = p.Attended;
            sheet.Cell(line, column++).Value = p.Late;

            var pct = sheet.Cell(line, column++);
            pct.Value = p.AttendancePercent / 100.0;
            pct.Style.NumberFormat.Format = "0%";

            sheet.Cell(line, column++).Value = AttendanceRules.Label(p.Eligibility);
            Rating(sheet.Cell(line, column), p.FeedbackRating);

            line++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(4);

        // ---- Certificates --------------------------------------------------
        var certificates = book.AddWorksheet("Certificates");
        var certRow = Title(certificates, "Certificates earned", $"{ev.Title} · rule: {ev.CertificateThresholdPercent}% of sessions");

        certRow = Header(certificates, certRow, new[]
        {
            "Name", "Student no.", "Course & year", "Sessions attended", "Attendance %", "Decision"
        });

        foreach (var p in report.Participants.Where(p => p.Earned))
        {
            certificates.Cell(certRow, 1).Value = p.ListName;
            certificates.Cell(certRow, 2).Value = p.StudentNumber;
            certificates.Cell(certRow, 3).Value = p.CourseYear;
            certificates.Cell(certRow, 4).Value = $"{p.Attended} of {p.Sessions}";
            certificates.Cell(certRow, 5).Value = p.AttendancePercent / 100.0;
            certificates.Cell(certRow, 5).Style.NumberFormat.Format = "0%";
            certificates.Cell(certRow, 6).Value = AttendanceRules.Label(p.Eligibility);
            certRow++;
        }

        certificates.Columns().AdjustToContents();

        // ---- Feedback ------------------------------------------------------
        if (report.FeedbackResponses > 0)
        {
            var voices = book.AddWorksheet("Feedback");

            var subtitle = $"{ev.Title} · {report.FeedbackResponses} response(s)"
                + (report.FeedbackAverage.HasValue
                    ? $" · {report.FeedbackAverage.Value:0.00} of 5"
                    : "")
                + $" · {report.FeedbackRecommendPercent}% would recommend";

            var voiceRow = Title(voices, "What participants said", subtitle);

            voiceRow = Header(voices, voiceRow, new[]
            {
                "Name", "Course & year", "Overall", "Content", "Venue", "Organisation",
                "Recommends", "What worked", "What to improve", "Submitted"
            });

            foreach (var f in report.Feedback)
            {
                voices.Cell(voiceRow, 1).Value = f.Who;
                voices.Cell(voiceRow, 2).Value = f.CourseYear ?? "—";
                voices.Cell(voiceRow, 3).Value = f.OverallRating;
                voices.Cell(voiceRow, 4).Value = f.ContentRating;
                voices.Cell(voiceRow, 5).Value = f.VenueRating;
                voices.Cell(voiceRow, 6).Value = f.OrganizationRating;
                voices.Cell(voiceRow, 7).Value = f.WouldRecommend ? "Yes" : "No";
                voices.Cell(voiceRow, 8).Value = f.Comment ?? "";
                voices.Cell(voiceRow, 9).Value = f.Improvement ?? "";
                voices.Cell(voiceRow, 10).Value = f.SubmittedOn;
                voices.Cell(voiceRow, 10).Style.DateFormat.Format = "dd mmm yyyy hh:mm";

                // Free text wraps rather than bleeding across the sheet.
                voices.Cell(voiceRow, 8).Style.Alignment.WrapText = true;
                voices.Cell(voiceRow, 9).Style.Alignment.WrapText = true;

                voiceRow++;
            }

            voices.Columns().AdjustToContents();
            voices.Column(8).Width = 50;
            voices.Column(9).Width = 50;
            voices.SheetView.FreezeRows(4);
        }

        return Save(book);
    }

    // ---- Shared furniture -------------------------------------------------

    /// <summary>
    /// A 1–5 score, or an em dash when nobody left one. An empty cell would
    /// read as a zero score to anyone sorting the column.
    /// </summary>
    private static void Rating(IXLCell cell, double? rating)
    {
        if (rating.HasValue)
        {
            cell.Value = Math.Round(rating.Value, 2);
            cell.Style.NumberFormat.Format = "0.00";
        }
        else
        {
            cell.Value = "—";
            cell.Style.Font.FontColor = XLColor.Gray;
        }
    }

    /// <summary>Writes the title block and returns the first free row.</summary>
    private static int Title(IXLWorksheet sheet, string title, string subtitle)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = subtitle;
        sheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        sheet.Cell(3, 1).Value = $"Produced {DateTime.Now:d MMM yyyy, h:mm tt} · Apilan";
        sheet.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;
        sheet.Cell(3, 1).Style.Font.FontSize = 9;

        return 5;
    }

    private static int Header(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = HeaderInk;
            cell.Style.Fill.BackgroundColor = HeaderFill;
        }

        return row + 1;
    }

    private static int Breakdown(IXLWorksheet sheet, int row, string title, List<BreakdownRow> rows)
    {
        sheet.Cell(row, 1).Value = title;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        row = Header(sheet, row, new[] { "Group", "Registered", "Attended", "Turnout" });

        foreach (var r in rows)
        {
            sheet.Cell(row, 1).Value = r.Label;
            sheet.Cell(row, 2).Value = r.Registered;
            sheet.Cell(row, 3).Value = r.Attended;
            sheet.Cell(row, 4).Value = r.Percent / 100.0;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "0%";
            row++;
        }

        return row + 1;
    }

    private static byte[] Save(XLWorkbook book)
    {
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
