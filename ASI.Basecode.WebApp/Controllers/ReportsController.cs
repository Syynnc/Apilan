using System.Linq;
using System.Text;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    /* The participation report at /participation. The filter form submits by
       GET, so ReportFilter binds from the query string and the URL can be
       bookmarked; IReportService does all the figures. */
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class ReportsController : BaseController
    {
        private readonly IReportService _reports;

        public ReportsController(IReportService reports)
        {
            _reports = reports;
        }

        // view: The sub-page: overview (default), events, members or punctuality.
        [Route("participation")]
        public IActionResult Index(ReportFilter filter, string view = null)
        {
            Tidy(filter);
            ViewBag.View = view;
            return View(_reports.GetReport(filter));
        }

        // The report's analytics as a CSV, for the same filters.
        [Route("participation/export")]
        public IActionResult Export(ReportFilter filter)
        {
            Tidy(filter);
            var csv = _reports.ExportCsv(filter, DisplayName, out var fileName);
            // With a BOM, Excel reads the dashes and accents as UTF-8.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv", fileName);
        }

        private static void Tidy(ReportFilter filter)
        {
            // A backwards range is a typo, not a request for nothing.
            if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
            {
                (filter.From, filter.To) = (filter.To, filter.From);
            }
        }
    }
}
