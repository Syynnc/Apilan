using System.Collections.Generic;

namespace ASI.Basecode.Resources.Constants
{
    /* The "Apilan" section of appsettings.json. Apilan runs for a single
       school, so who that school is lives in configuration, not in the views. */
    public class ApilanSettings
    {
        public const string Section = "Apilan";

        // The one school this deployment serves, shown across every screen.
        public string SchoolName { get; set; } = "Your school";

        // Short form of the school name for tight spots (ticket kickers, chips).
        public string SchoolShortName { get; set; } = "School";

        // The student organization running the console.
        public string OrganizationName { get; set; } = "Student organization";

        /* Every new member account starts with this password, and an officer's
           reset puts it back. Signing in with it forces a change first. */
        public string DefaultPassword { get; set; } = "Apilan@2026";

        /* The courses offered in the member form's course picker. Officers can
           still type one that is not listed. */
        public List<CourseOption> Courses { get; set; } = new();
    }

    public class CourseOption
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }
}
