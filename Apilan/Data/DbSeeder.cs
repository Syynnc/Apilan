using Apilan.Models;
using Apilan.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Apilan.Data;

/// <summary>
/// Creates the three roles and the first admin account on startup, and — when
/// Apilan:SeedDemoData is true — a set of demo records so the app is not an
/// empty shell on a fresh machine. Everything here is idempotent.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApilanDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        // The database may not exist yet — migrations are run by hand.
        if (!await db.Database.CanConnectAsync())
        {
            log.LogWarning("Apilan database is not reachable. Run Update-Database, then restart.");
            return;
        }

        if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            log.LogWarning("There are pending migrations. Run Update-Database, then restart.");
            return;
        }

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        await SeedAdminAsync(users, config, log);

        if (config.GetValue("Apilan:SeedDemoData", false))
        {
            await SeedOrganizationsAsync(db);
            await SeedMasterDataAsync(db);
            await SeedMembersAsync(db);
            await SeedEventsAsync(db);
            await SeedStaffAsync(users, db, config);
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> users, IConfiguration config, ILogger log)
    {
        var email = config["Apilan:DefaultAdminEmail"] ?? "admin@apilan.local";
        var password = config["Apilan:DefaultAdminPassword"] ?? "Apilan!2026";

        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "System Administrator",
            Status = AccountStatus.Active,
            ReviewedOn = DateTime.Now
        };

        var result = await users.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await users.AddToRoleAsync(admin, Roles.Admin);
            log.LogInformation("Seeded default admin {Email}.", email);
        }
        else
        {
            log.LogError("Could not seed admin: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedOrganizationsAsync(ApilanDbContext db)
    {
        if (await db.Organizations.AnyAsync())
        {
            return;
        }

        db.Organizations.AddRange(
            new Organization
            {
                Name = "CCS Student Council",
                Acronym = "CCSSC",
                Description = "The college-wide council for the College of Computer Studies.",
                Adviser = "Prof. M. Salvador",
                ContactEmail = "ccssc@student.edu.ph",
                ColorKey = "assembly"
            },
            new Organization
            {
                Name = "Society of Information Technology Students",
                Acronym = "SITS",
                Description = "Technical talks, workshops, and the annual hackathon.",
                Adviser = "Prof. R. Bautista",
                ContactEmail = "sits@student.edu.ph",
                ColorKey = "workshop"
            },
            new Organization
            {
                Name = "Computer Science Guild",
                Acronym = "CSG",
                Description = "Competitive programming and research interest group.",
                Adviser = "Dr. A. Lim",
                ContactEmail = "csguild@student.edu.ph",
                ColorKey = "talk"
            },
            new Organization
            {
                Name = "CCS Community Outreach Corps",
                Acronym = "COC",
                Description = "Community service and extension activities.",
                Adviser = "Prof. J. Navarro",
                ContactEmail = "outreach@student.edu.ph",
                ColorKey = "service"
            });

        await db.SaveChangesAsync();
    }

    private static async Task SeedMasterDataAsync(ApilanDbContext db)
    {
        if (!await db.EventTypes.AnyAsync())
        {
            db.EventTypes.AddRange(
                new EventType { Name = "Seminar", ColorKey = "talk", Description = "Talks and guest sessions." },
                new EventType { Name = "Workshop", ColorKey = "workshop", Description = "Hands-on skills sessions." },
                new EventType { Name = "General Assembly", ColorKey = "assembly", Description = "Org-wide meetings." },
                new EventType { Name = "Outreach", ColorKey = "service", Description = "Community service activities." },
                new EventType { Name = "Training", ColorKey = "training", Description = "Leadership and officer training." },
                new EventType { Name = "Competition", ColorKey = "workshop", Description = "Contests and tournaments." });
        }

        if (!await db.Venues.AnyAsync())
        {
            db.Venues.AddRange(
                new Venue { Name = "AVR 2", Location = "Main Building, 2F", Capacity = 180, ContactPerson = "Property Office" },
                new Venue { Name = "Gymnasium", Location = "Sports Complex", Capacity = 400, ContactPerson = "Athletics Office" },
                new Venue { Name = "Computer Lab 3", Location = "CCS Building, 3F", Capacity = 60, ContactPerson = "CCS Dean's Office" },
                new Venue { Name = "Function Hall", Location = "Student Center", Capacity = 250, ContactPerson = "Student Affairs" },
                new Venue { Name = "Org Room", Location = "Student Center, 2F", Capacity = 25, ContactPerson = "Student Affairs" },
                new Venue { Name = "Quadrangle", Location = "Campus grounds", Capacity = 500, AvailabilityNotes = "Weather dependent." });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedMembersAsync(ApilanDbContext db)
    {
        if (await db.Members.AnyAsync())
        {
            return;
        }

        var people = new (string No, string Last, string First, string Course, int Year)[]
        {
            ("2022-04417", "Villanueva", "Mara",   "BSCS", 4),
            ("2023-01188", "Dela Cruz",  "Job",    "BSIT", 2),
            ("2022-09043", "Ramos",      "Aliyah", "BSCS", 3),
            ("2021-07725", "Tolentino",  "Kier",   "BSIT", 4),
            ("2023-02914", "Bautista",   "Nica",   "BSCS", 2),
            ("2022-06310", "Medina",     "Paolo",  "BSIS", 3),
            ("2024-00512", "Aguilar",    "Shane",  "BSCS", 1),
            ("2021-03398", "Domingo",    "Rex",    "BSCS", 4),
            ("2023-05127", "Lazaro",     "Trina",  "BSIT", 2),
            ("2022-08804", "Corpuz",     "Ivan",   "BSIS", 3),
            ("2024-01266", "Sarmiento",  "Bea",    "BSCS", 1),
            ("2023-07450", "Mercado",    "Dana",   "BSIT", 2)
        };

        var joined = DateTime.Now.AddMonths(-8);

        db.Members.AddRange(people.Select((p, i) => new Member
        {
            StudentNumber = p.No,
            LastName = p.Last,
            FirstName = p.First,
            Course = p.Course,
            YearLevel = p.Year,
            Section = "A",
            Email = $"{p.First.ToLowerInvariant()}.{p.Last.Replace(" ", "").ToLowerInvariant()}@student.edu.ph",
            MembershipType = i < 3 ? MembershipType.Officer : MembershipType.Regular,
            Status = MembershipStatus.Active,
            JoinedOn = joined.AddDays(i)
        }));

        await db.SaveChangesAsync();
    }

    private static async Task SeedEventsAsync(ApilanDbContext db)
    {
        if (await db.Events.AnyAsync())
        {
            return;
        }

        var seminar = await db.EventTypes.FirstAsync(t => t.Name == "Seminar");
        var workshop = await db.EventTypes.FirstAsync(t => t.Name == "Workshop");
        var assembly = await db.EventTypes.FirstAsync(t => t.Name == "General Assembly");

        var council = await db.Organizations.FirstAsync(o => o.Acronym == "CCSSC");
        var sits = await db.Organizations.FirstAsync(o => o.Acronym == "SITS");
        var guild = await db.Organizations.FirstAsync(o => o.Acronym == "CSG");

        var avr = await db.Venues.FirstAsync(v => v.Name == "AVR 2");
        var lab = await db.Venues.FirstAsync(v => v.Name == "Computer Lab 3");
        var gym = await db.Venues.FirstAsync(v => v.Name == "Gymnasium");

        // Anchored to today so the demo data never goes stale.
        var today = DateTime.Today;

        var techNight = new Event
        {
            Title = "Tech Night 2026",
            Description = "An evening of lightning talks from alumni working in the industry.",
            OrganizationId = sits.Id,
            EventTypeId = seminar.Id,
            VenueId = avr.Id,
            StartsAt = today.AddDays(5).AddHours(17),
            EndsAt = today.AddDays(5).AddHours(20),
            RegistrationDeadline = today.AddDays(4).AddHours(23),
            Capacity = 180,
            Status = EventStatus.Published,
            PublishedOn = DateTime.Now,
            Sessions =
            {
                new EventSession { Name = "Main session", Sequence = 1, StartsAt = today.AddDays(5).AddHours(17), EndsAt = today.AddDays(5).AddHours(20) }
            }
        };

        var resume = new Event
        {
            Title = "Résumé Workshop",
            Description = "Bring a draft. Leave with something you would actually send.",
            OrganizationId = guild.Id,
            EventTypeId = workshop.Id,
            VenueId = lab.Id,
            StartsAt = today.AddDays(12).AddHours(13),
            EndsAt = today.AddDays(12).AddHours(16),
            RegistrationDeadline = today.AddDays(11).AddHours(23),
            Capacity = 60,
            Status = EventStatus.Published,
            PublishedOn = DateTime.Now,
            Sessions =
            {
                new EventSession { Name = "Main session", Sequence = 1, StartsAt = today.AddDays(12).AddHours(13), EndsAt = today.AddDays(12).AddHours(16) }
            }
        };

        var ga = new Event
        {
            Title = "General Assembly 2026",
            Description = "Semestral general assembly. Attendance is required for certificate eligibility.",
            OrganizationId = council.Id,
            EventTypeId = assembly.Id,
            VenueId = gym.Id,
            StartsAt = today.AddDays(19).AddHours(15),
            EndsAt = today.AddDays(20).AddHours(18),
            RegistrationDeadline = today.AddDays(18).AddHours(23),
            Capacity = 400,
            Status = EventStatus.Published,
            PublishedOn = DateTime.Now,
            CertificateThresholdPercent = 80,
            Sessions =
            {
                new EventSession { Name = "Day 1 · Afternoon", Sequence = 1, StartsAt = today.AddDays(19).AddHours(15), EndsAt = today.AddDays(19).AddHours(18) },
                new EventSession { Name = "Day 2 · Afternoon", Sequence = 2, StartsAt = today.AddDays(20).AddHours(15), EndsAt = today.AddDays(20).AddHours(18) }
            }
        };

        // One event already behind us, so the reports, certificates, and
        // activity log have something real to show on a fresh machine.
        var orientation = new Event
        {
            Title = "Freshman Orientation",
            Description = "Two-part welcome for first years: the org, and how to survive first sem.",
            OrganizationId = council.Id,
            EventTypeId = assembly.Id,
            VenueId = avr.Id,
            StartsAt = today.AddDays(-21).AddHours(9),
            EndsAt = today.AddDays(-21).AddHours(16),
            RegistrationDeadline = today.AddDays(-23).AddHours(23),
            Capacity = 120,
            Status = EventStatus.Completed,
            PublishedOn = DateTime.Now.AddDays(-30),
            CertificateThresholdPercent = 80,
            Sessions =
            {
                new EventSession { Name = "Morning", Sequence = 1, LateAfterMinutes = 15, StartsAt = today.AddDays(-21).AddHours(9), EndsAt = today.AddDays(-21).AddHours(12) },
                new EventSession { Name = "Afternoon", Sequence = 2, LateAfterMinutes = 10, StartsAt = today.AddDays(-21).AddHours(13), EndsAt = today.AddDays(-21).AddHours(16) }
            }
        };

        db.Events.AddRange(techNight, resume, ga, orientation);
        await db.SaveChangesAsync();

        // A handful of registrations so the officer screens are not empty.
        var members = await db.Members.OrderBy(m => m.Id).Take(8).ToListAsync();
        var codes = new HashSet<string>();

        foreach (var (ev, take) in new[] { (techNight, 8), (resume, 5), (ga, 6) })
        {
            foreach (var member in members.Take(take))
            {
                string code;
                do { code = RegistrationCode.Next(); } while (!codes.Add(code));

                db.Registrations.Add(new Registration
                {
                    EventId = ev.Id,
                    MemberId = member.Id,
                    Code = code,
                    Status = RegistrationStatus.Confirmed,
                    RegisteredOn = DateTime.Now.AddDays(-2)
                });
            }
        }

        await db.SaveChangesAsync();

        await SeedPastAttendanceAsync(db, orientation, codes);
    }

    /// <summary>
    /// Fills in the finished event: everybody registered, most turned up for
    /// both sessions, a couple for one, and one not at all — enough shape for
    /// the certificate rule to actually decide something.
    /// </summary>
    private static async Task SeedPastAttendanceAsync(
        ApilanDbContext db, Event past, HashSet<string> codes)
    {
        var members = await db.Members.OrderBy(m => m.Id).ToListAsync();
        var sessions = await db.EventSessions
            .Where(s => s.EventId == past.Id)
            .OrderBy(s => s.Sequence)
            .ToListAsync();

        var registrations = new List<Registration>();

        foreach (var member in members)
        {
            string code;
            do { code = RegistrationCode.Next(); } while (!codes.Add(code));

            registrations.Add(new Registration
            {
                EventId = past.Id,
                MemberId = member.Id,
                Code = code,
                Status = RegistrationStatus.Confirmed,
                RegisteredOn = past.StartsAt.AddDays(-5),
                IsWalkIn = member.Id % 7 == 0
            });
        }

        db.Registrations.AddRange(registrations);
        await db.SaveChangesAsync();

        for (var i = 0; i < registrations.Count; i++)
        {
            // Every fifth person missed the afternoon; every ninth never came.
            var attends = i % 9 == 0 ? 0 : i % 5 == 0 ? 1 : sessions.Count;
            var attended = 0;

            foreach (var session in sessions)
            {
                var turnedUp = attended < attends;

                if (!turnedUp)
                {
                    // Accounted for and did not come — an absence is a record.
                    db.AttendanceRecords.Add(new AttendanceRecord
                    {
                        RegistrationId = registrations[i].Id,
                        EventSessionId = session.Id,
                        Status = AttendanceStatus.Absent,
                        TimeIn = null,
                        Method = CheckInMethod.ManualSearch,
                        Note = "Marked absent when the session was closed."
                    });

                    continue;
                }

                // Every third arrival is a straggler, so the late figures are
                // not all zero on a fresh machine.
                var minutesLate = i % 3 == 0 ? session.LateAfterMinutes + 4 + (i % 12) : i % 7;
                var timeIn = session.StartsAt.AddMinutes(minutesLate);

                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    RegistrationId = registrations[i].Id,
                    EventSessionId = session.Id,
                    Status = AttendanceRules.Classify(timeIn, session.StartsAt, session.LateAfterMinutes),
                    TimeIn = timeIn,
                    TimeOut = session.EndsAt.AddMinutes(-(i % 10)),
                    Method = i % 3 == 0 ? CheckInMethod.ManualSearch : CheckInMethod.QrCode
                });

                attended++;
            }

            registrations[i].Eligibility = AttendanceRules.MeetsThreshold(
                RegistrationStatus.Confirmed, attends, sessions.Count, past.CertificateThresholdPercent)
                    ? EligibilityStatus.Eligible
                    : EligibilityStatus.NotEligible;
        }

        await db.SaveChangesAsync();
        await SeedFeedbackAsync(db, registrations);
    }

    /// <summary>
    /// Feedback on the finished event from most, but not all, of the people who
    /// turned up — a 100% response rate would be a lie the screens then design
    /// around. Two responses are anonymous so that path is exercised too.
    /// </summary>
    private static async Task SeedFeedbackAsync(ApilanDbContext db, List<Registration> registrations)
    {
        var words = new (string Worked, string Improve)[]
        {
            ("The speakers were actually from the industry, not just faculty.",
             "Start on time. We waited 40 minutes for the opening remarks."),
            ("Registration at the door was quick — scanned and in.",
             "The room was too small for the number of people who signed up."),
            ("Good mix of talks and the open forum at the end.",
             "Share the slides afterwards."),
            ("Free lunch, and the certificate was issued on the same day.",
             "Aircon in the afternoon session."),
            ("The hands-on part was worth more than the lecture part.",
             "More time for the workshop, less for the introductions.")
        };

        var attendees = await db.Registrations
            .Where(r => registrations.Select(x => x.Id).Contains(r.Id))
            .Where(r => r.AttendanceRecords.Any(a => a.Status != AttendanceStatus.Absent))
            .OrderBy(r => r.Id)
            .ToListAsync();

        for (var i = 0; i < attendees.Count; i++)
        {
            // Roughly three in four say something.
            if (i % 4 == 3)
            {
                continue;
            }

            var (worked, improve) = words[i % words.Length];

            db.EventFeedback.Add(new EventFeedback
            {
                RegistrationId = attendees[i].Id,
                OverallRating = 3 + (i % 3),
                ContentRating = 3 + ((i + 1) % 3),
                VenueRating = 2 + (i % 4),
                OrganizationRating = 3 + ((i + 2) % 3),
                WouldRecommend = i % 6 != 0,
                Comment = worked,
                Improvement = i % 3 == 0 ? improve : null,
                IsAnonymous = i % 5 == 0,
                SubmittedOn = DateTime.Now.AddDays(-20).AddHours(i)
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedStaffAsync(
        UserManager<ApplicationUser> users, ApilanDbContext db, IConfiguration config)
    {
        var password = config["Apilan:DefaultAdminPassword"] ?? "Apilan!2026";

        // One officer and one member account, each linked to a member record,
        // so every role can be demonstrated without creating accounts by hand.
        var accounts = new (string Email, string Role, string StudentNumber, string? Org)[]
        {
            ("officer@apilan.local", Roles.Officer, "2022-04417", "CCSSC"),
            ("member@apilan.local",  Roles.Member,  "2023-01188", null)
        };

        foreach (var (email, role, studentNumber, acronym) in accounts)
        {
            if (await users.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var member = await db.Members.FirstOrDefaultAsync(m => m.StudentNumber == studentNumber);

            var org = acronym is null
                ? null
                : await db.Organizations.FirstOrDefaultAsync(o => o.Acronym == acronym);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = member?.FullName ?? email,
                StudentNumber = studentNumber,
                Status = AccountStatus.Active,
                ReviewedOn = DateTime.Now,
                MemberId = member?.Id,
                OrganizationId = org?.Id
            };

            var result = await users.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await users.AddToRoleAsync(user, role);
            }
        }
    }
}
