using System;

namespace CleoAssignment.ApiService.Implementation;

/// <summary>
/// Represents the resource access current status.
/// </summary>
public class ResourceLimiterStatus
{
    public bool IsBlocked { get; }
    public DateTime? BlockedTill { get; }

    public ResourceLimiterStatus(bool isBlocked = false, DateTime? blockedTill = null)
    {
        IsBlocked = isBlocked;
        BlockedTill = blockedTill;
    }
}
