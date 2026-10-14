using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories
{
    public class AuditLogRepository : BaseRepository, IAuditLogRepository
    {
        public AuditLogRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public IQueryable<AuditLog> GetLogs()
        {
            return this.GetDbSet<AuditLog>().AsNoTracking().Include(l => l.Account).ThenInclude(a => a.Role);
        }

        public void Add(AuditLog log)
        {
            // The database stamps occurred_at, so the time is the server's, not the caller's.
            Execute($@"EXEC dbo.sp_AuditLog_Add @account_id = {log.AccountId}, @actor = {log.Actor},
                @action = {log.Action}, @target_type = {log.TargetType}, @target_id = {log.TargetId},
                @target_label = {log.TargetLabel}, @details = {log.Details}, @ip_address = {log.IpAddress}");
        }
    }
}
