using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>The activity log: who did what, to what, and exactly when.</summary>
    [Authorize(Policy = Const.OfficerOnlyPolicy)]
    public class ActivityController : BaseController
    {
        private readonly IAuditService _audit;

        public ActivityController(IAuditService audit)
        {
            _audit = audit;
        }

        public IActionResult Index(ActivityFilter filter)
        {
            return View(_audit.GetPage(filter));
        }
    }
}
