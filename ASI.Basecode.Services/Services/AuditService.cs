using System;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.Services.Services
{
    public class AuditService : IAuditService
    {
        /// <summary>The page lists this many of the newest matches.</summary>
        private const int PageLimit = 500;

        private readonly IAuditLogRepository _logs;
        private readonly IAccountRepository _accounts;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IAuditLogRepository logs, IAccountRepository accounts, ILogger<AuditService> logger)
        {
            _logs = logs;
            _accounts = accounts;
            _logger = logger;
        }

        public void Record(AuditEntry entry)
        {
            // The change itself has already been saved. A log that cannot be
            // written must not turn that into an error page, but it must not
            // vanish quietly either: it goes to the server log instead.
            try
            {
                _logs.Add(new AuditLog
                {
                    AccountId = entry.AccountId,
                    Actor = Trim(entry.Actor, 100) ?? "system",
                    Action = entry.Action,
                    TargetType = entry.TargetType,
                    TargetId = entry.TargetId,
                    TargetLabel = Trim(entry.TargetLabel, 255),
                    Details = Trim(entry.Details, 1000),
                    IpAddress = Trim(entry.IpAddress, 45),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not write the activity log: {Actor} {Action} {TargetType} {TargetId} {Details}",
                    entry.Actor, entry.Action, entry.TargetType, entry.TargetId, entry.Details);
            }
        }

        public ActivityPageViewModel GetPage(ActivityFilter filter)
        {
            filter ??= new ActivityFilter();
            var hidden = AuditActions.Hidden.ToList();
            var all = _logs.GetLogs().Where(l => !hidden.Contains(l.Action));
            var query = all;

            if (filter.Who.HasValue)
            {
                query = query.Where(l => l.AccountId == filter.Who);
            }

            if (!string.IsNullOrEmpty(filter.Group))
            {
                var actions = AuditActions.All.Where(a => a.Value.Group == filter.Group).Select(a => a.Key).ToList();
                query = query.Where(l => actions.Contains(l.Action));
            }

            if (filter.From.HasValue)
            {
                query = query.Where(l => l.OccurredAt >= filter.From.Value.Date);
            }

            if (filter.To.HasValue)
            {
                var end = filter.To.Value.Date.AddDays(1);
                query = query.Where(l => l.OccurredAt < end);
            }

            var today = DateTime.Today;

            return new ActivityPageViewModel
            {
                Filter = filter,
                Limit = PageLimit,
                Total = query.Count(),
                Rows = query
                    .OrderByDescending(l => l.OccurredAt).ThenByDescending(l => l.Id)
                    .Take(PageLimit)
                    .Select(l => new ActivityRow
                    {
                        Id = l.Id,
                        OccurredAt = l.OccurredAt,
                        AccountId = l.AccountId,
                        Actor = l.Actor,
                        ActorName = l.Account != null && l.Account.User != null
                            ? l.Account.User.FirstName + " " + l.Account.User.LastName
                            : l.Actor,
                        ActorRole = l.Account != null ? l.Account.Role.RoleName : null,
                        Action = l.Action,
                        TargetType = l.TargetType,
                        TargetId = l.TargetId,
                        TargetLabel = l.TargetLabel,
                        Details = l.Details,
                        IpAddress = l.IpAddress,
                    })
                    .ToList(),
                Today = all.Count(l => l.OccurredAt >= today),
                Officers = all.Where(l => l.OccurredAt >= today && l.Account != null && l.Account.Role.RoleName == Const.RoleOfficer)
                    .Select(l => l.AccountId).Distinct().Count(),
                Latest = all.OrderByDescending(l => l.OccurredAt).Select(l => (DateTime?)l.OccurredAt).FirstOrDefault(),
                // Everyone who appears in the log, officers first, for the "who" filter.
                People = _accounts.GetAccounts()
                    .Where(a => all.Any(l => l.AccountId == a.Id))
                    .OrderBy(a => a.Role.RoleName == Const.RoleOfficer ? 0 : 1)
                    .ThenBy(a => a.Username)
                    .Select(a => new LookupItem(a.Id,
                        (a.User != null ? a.User.FirstName + " " + a.User.LastName + " · " : "") + a.Username))
                    .ToList(),
            };
        }

        private static string Trim(string value, int max) =>
            string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value.Substring(0, max - 1) + "…";
    }
}
