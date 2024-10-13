using System;
using System.Collections.Concurrent;

namespace CleoAssignment.ApiService.Implementation;

/// <summary>
/// Handles limitation of access to a particular resource with "T" key with dynamic intervals counting.
/// Dynamic intervals allow controlling maximum request per interval at any given time period.
/// </summary>
/// <typeparam name="T">The resource identifier type.</typeparam>
public class ResourceAccessLimiter<T> where T : notnull
{
    private readonly ITimeProvider _timeProvider;
    private readonly ThrottleSettings _throttleSettings;

    private readonly long _ticksPerRequestSlot;
    private readonly long _ticksPerTrackingInterval;
    private readonly long _banTimeoutLength; // in ticks

    private readonly ConcurrentDictionary<T, ResourceLimiterItemTracker> _resourcesInfo = new();

    public ResourceAccessLimiter(ITimeProvider timeProvider, ThrottleSettings throttleSettings)
    {
        _timeProvider = timeProvider;
        _throttleSettings = throttleSettings;

        if (_throttleSettings.MaxRequestsPerIp <= 0)
            throw new Exception("Maximum requests amount must be greater than 0."); // not handled by the calling code for simplicity

        _ticksPerTrackingInterval = _throttleSettings.ThrottleInterval.Ticks;
        // determining how many ticks would a resource request cost
        _ticksPerRequestSlot = _ticksPerTrackingInterval / _throttleSettings.MaxRequestsPerIp;
        _banTimeoutLength = _throttleSettings.BanTimeOut.Ticks;
    }

    /// <summary>
    /// Checks whether the resource can be accessed at the moment and assesses it if allowed.
    /// </summary>
    /// <param name="resourceId">Particular resource id which the access is checked against.</param>
    /// <returns>Status of the requested operation. (Including, whether access blocked or not).</returns>
    public ResourceLimiterStatus RequestAccess(T resourceId)
    {
        var now = _timeProvider.UtcNow.Ticks;

        // ReSharper disable once InconsistentlySynchronizedField
        var resourceDetails = _resourcesInfo.GetOrAdd(resourceId, _ => new ResourceLimiterItemTracker { RequestSlotEndsAt = now });;

        lock (resourceDetails)
        {
            // doing nothing if still blocked
            if (now < resourceDetails.BlockUntilTick)
                return new ResourceLimiterStatus(true, new DateTime(resourceDetails.BlockUntilTick, DateTimeKind.Utc));

            // the interval frame is dynamic, so just checking if amount of requests doesn't exceed the interval period in total
            var intervalEndsAt = now + _ticksPerTrackingInterval;

            // if the requests queue is out of current slot, then initiating its start from now (i.e., starting a dynamic interval from now)
            if (resourceDetails.RequestSlotEndsAt < now)
                resourceDetails.RequestSlotEndsAt = now;

            resourceDetails.RequestSlotEndsAt += _ticksPerRequestSlot;

            // if the incoming request slot can't be fit into the current interval length, then the access to the resource gets blocked
            if (resourceDetails.RequestSlotEndsAt > intervalEndsAt)
            {
                // amount of requests per interval is exceeded. blocking now
                resourceDetails.BlockUntilTick = now + _banTimeoutLength;
                return new ResourceLimiterStatus(true, new DateTime(resourceDetails.BlockUntilTick, DateTimeKind.Utc));
            }

            return new ResourceLimiterStatus();
        }
    }
}

/// <summary>
/// Contains details of a particular resource timing details.
/// </summary>
internal class ResourceLimiterItemTracker
{
    /// <summary>
    /// A timeline pointer. Represents a current time shift created by the requests consumed by a client.
    /// Every new request (if the access is not blocked) increases the shift to indicate possible range of allowed requests before the slot interval gets elapsed.
    /// </summary>
    public long RequestSlotEndsAt { get; set; }

    /// <summary>
    /// A timeline pointer. Represents a moment till which the access is blocked.
    /// </summary>
    public long BlockUntilTick { get; set; }
}
