using System.ComponentModel.DataAnnotations;
using Apilan.Models;

namespace Apilan.ViewModels;

// ===========================================================================
//  FEEDBACK · THE PARTICIPANT'S SIDE
// ===========================================================================

/// <summary>
/// The form a participant fills in after an event. Ratings default to 0 so a
/// half-finished form fails validation rather than quietly recording a 1.
/// </summary>
public class FeedbackFormViewModel
{
    public int RegistrationId { get; set; }

    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public string OrgName { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }

    /// <summary>Sessions attended out of the event's total, shown for context.</summary>
    public int Attended { get; set; }
    public int Sessions { get; set; }

    [Range(1, 5, ErrorMessage = "Give the event an overall score.")]
    [Display(Name = "Overall")]
    public int OverallRating { get; set; }

    [Range(1, 5, ErrorMessage = "Rate the content and speakers.")]
    [Display(Name = "Content and speakers")]
    public int ContentRating { get; set; }

    [Range(1, 5, ErrorMessage = "Rate the venue and facilities.")]
    [Display(Name = "Venue and facilities")]
    public int VenueRating { get; set; }

    [Range(1, 5, ErrorMessage = "Rate how well the event was run.")]
    [Display(Name = "How well it was run")]
    public int OrganizationRating { get; set; }

    [Display(Name = "I would recommend this to a batchmate")]
    public bool WouldRecommend { get; set; } = true;

    [StringLength(1000)]
    [Display(Name = "What worked")]
    public string? Comment { get; set; }

    [StringLength(1000)]
    [Display(Name = "What to improve")]
    public string? Improvement { get; set; }

    [Display(Name = "Submit without my name")]
    public bool IsAnonymous { get; set; }

    /// <summary>Set when the participant is editing a response they already left.</summary>
    public bool AlreadySubmitted { get; set; }
    public DateTime? SubmittedOn { get; set; }

    public static FeedbackFormViewModel FromEntity(EventFeedback f, FeedbackFormViewModel shell)
    {
        shell.OverallRating = f.OverallRating;
        shell.ContentRating = f.ContentRating;
        shell.VenueRating = f.VenueRating;
        shell.OrganizationRating = f.OrganizationRating;
        shell.WouldRecommend = f.WouldRecommend;
        shell.Comment = f.Comment;
        shell.Improvement = f.Improvement;
        shell.IsAnonymous = f.IsAnonymous;
        shell.AlreadySubmitted = true;
        shell.SubmittedOn = f.SubmittedOn;
        return shell;
    }

    public void ApplyTo(EventFeedback f)
    {
        f.OverallRating = OverallRating;
        f.ContentRating = ContentRating;
        f.VenueRating = VenueRating;
        f.OrganizationRating = OrganizationRating;
        f.WouldRecommend = WouldRecommend;
        f.Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim();
        f.Improvement = string.IsNullOrWhiteSpace(Improvement) ? null : Improvement.Trim();
        f.IsAnonymous = IsAnonymous;
        f.SubmittedOn = DateTime.Now;
    }
}

// ===========================================================================
//  FEEDBACK · THE OFFICER'S SIDE
// ===========================================================================

/// <summary>Every event that has feedback to read, newest first.</summary>
public class FeedbackListViewModel
{
    public List<FeedbackEventRow> Events { get; set; } = new();

    public int? OrganizationId { get; set; }
    public List<OrganizationOption> Organizations { get; set; } = new();

    public int TotalResponses => Events.Sum(e => e.Responses);

    /// <summary>Mean across every response, rather than a mean of means.</summary>
    public double? AverageRating
    {
        get
        {
            var counted = Events.Where(e => e.Responses > 0 && e.AverageRating.HasValue).ToList();

            if (!counted.Any())
            {
                return null;
            }

            return counted.Sum(e => e.AverageRating!.Value * e.Responses)
                   / counted.Sum(e => e.Responses);
        }
    }
}

public class FeedbackEventRow
{
    public int EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OrgAcronym { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string ColorKey { get; set; } = "talk";
    public DateTime StartsAt { get; set; }
    public EventStatus Status { get; set; }

    public int Attended { get; set; }
    public int Responses { get; set; }
    public double? AverageRating { get; set; }
    public int RecommendCount { get; set; }

    /// <summary>Share of the people who turned up who then said something.</summary>
    public int ResponseRatePercent => Attended <= 0
        ? 0
        : Math.Min(100, (int)Math.Round(Responses * 100.0 / Attended));
}

/// <summary>One event's feedback: the numbers, then what people actually wrote.</summary>
public class EventFeedbackViewModel
{
    public Event Event { get; set; } = null!;

    public List<FeedbackResponseRow> Responses { get; set; } = new();

    public int Attended { get; set; }

    public double? Overall { get; set; }
    public double? Content { get; set; }
    public double? Venue { get; set; }
    public double? Organization { get; set; }

    public int RecommendCount { get; set; }

    /// <summary>Count of responses per overall score, 1 through 5.</summary>
    public Dictionary<int, int> Distribution { get; set; } = new();

    public int Count => Responses.Count;

    public double? Average => Count == 0 ? null : Responses.Average(r => r.AverageRating);

    public int ResponseRatePercent => Attended <= 0
        ? 0
        : Math.Min(100, (int)Math.Round(Count * 100.0 / Attended));

    public int RecommendPercent => Count == 0
        ? 0
        : (int)Math.Round(RecommendCount * 100.0 / Count);

    /// <summary>The tallest bar, so the histogram can scale to it.</summary>
    public int PeakCount => Distribution.Count == 0 ? 0 : Distribution.Values.Max();
}

public class FeedbackResponseRow
{
    public int Id { get; set; }

    /// <summary>"Anonymous" when the participant asked not to be named.</summary>
    public string Who { get; set; } = string.Empty;
    public string? CourseYear { get; set; }

    public int OverallRating { get; set; }
    public int ContentRating { get; set; }
    public int VenueRating { get; set; }
    public int OrganizationRating { get; set; }
    public bool WouldRecommend { get; set; }

    public string? Comment { get; set; }
    public string? Improvement { get; set; }
    public DateTime SubmittedOn { get; set; }

    public double AverageRating =>
        (OverallRating + ContentRating + VenueRating + OrganizationRating) / 4.0;

    public bool HasWords =>
        !string.IsNullOrWhiteSpace(Comment) || !string.IsNullOrWhiteSpace(Improvement);
}
