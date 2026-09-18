using Apilan.Data;
using Apilan.Models;

namespace Apilan.Services;

/// <summary>
/// Writes the administration module's trail. Callers add the entry inside the
/// same unit of work as the change it describes, so a log line and the change
/// it records are saved together or not at all.
/// </summary>
public static class AuditTrail
{
    public static void Record(
        ApilanDbContext db,
        string? actorUserId,
        string action,
        string entityName,
        object? entityId = null,
        string? detail = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId?.ToString(),
            Detail = Truncate(detail, 500)
        });
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= max ? value
        : value[..max];
}
