using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class ReportsController : BaseController
    {
        private readonly IReportService _reports;

        public ReportsController(IReportService reports)
        {
            _reports = reports;
        }

        public IActionResult Index(ReportFilter filter)
        {
            // A backwards range is a typo, not a request for nothing.
            if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
            {
                (filter.From, filter.To) = (filter.To, filter.From);
            }

            return View(_reports.GetReport(filter));
        }
    }
}
