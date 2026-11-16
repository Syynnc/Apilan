using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class DashboardController : BaseController
    {
        private readonly IReportService _reports;

        public DashboardController(IReportService reports)
        {
            _reports = reports;
        }

        public IActionResult Index()
        {
            return View(_reports.GetDashboard());
        }
    }
}
