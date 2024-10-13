using CleoAssignment.ApiService;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CleoAssignment.Tests;

public class IncludedAdvancedUnitTests
{
    [Fact]
    public async Task SimultaneousRequestsThrottlingWorksForDefinedMaxLimitOnly()
    {
        var timeProvider = GetDefaultTimeProvider();
        var resourceProvider = new IncludedBasicUnitTests.InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = new ThrottleSettings()
        {
            MaxRequestsPerIp = 500,
            ThrottleInterval = TimeSpan.FromMinutes(1),
            BanTimeOut = TimeSpan.FromMinutes(1)
        };

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        var results = await Task.WhenAll(Enumerable.Range(0, throttleSettings.MaxRequestsPerIp * 2).Select(_ => apiService.GetResource(new("127.0.0.1", "id1"))));

        var succeededCount = results.Count(x => x.Success);

        Assert.Equal(throttleSettings.MaxRequestsPerIp, succeededCount);
    }

    [Fact]
    public async Task SimultaneousRequestsThrottling_WorksForDefinedMaxLimitOnly()
    {
        var timeProvider = GetDefaultTimeProvider();
        var resourceProvider = new IncludedBasicUnitTests.InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = new ThrottleSettings
        {
            MaxRequestsPerIp = 500,
            ThrottleInterval = TimeSpan.FromMinutes(1),
            BanTimeOut = TimeSpan.FromMinutes(1)
        };

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        var results = await Task.WhenAll(Enumerable.Range(0, throttleSettings.MaxRequestsPerIp * 2)
                                                   .Select(_ => apiService.GetResource(new("127.0.0.1", "id1"))));

        var succeededCount = results.Count(x => x.Success);

        Assert.Equal(throttleSettings.MaxRequestsPerIp, succeededCount);
    }

    [Fact]
    public async Task SimultaneousRequestsThrottling_DifferentIps_WorksForDefinedMaxLimitOnly()
    {
        var timeProvider = GetDefaultTimeProvider();
        var resourceProvider = new IncludedBasicUnitTests.InjectedResourceProvider<int>(_ => 1, (_, _) => { });

        var throttleSettings = new ThrottleSettings
        {
            MaxRequestsPerIp = 500,
            ThrottleInterval = TimeSpan.FromMinutes(1),
            BanTimeOut = TimeSpan.FromMinutes(1)
        };

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        // creating 5 groups of requests with doubled amount of max requests per interval
        var results = await Task.WhenAll(Enumerable.Range(1, 5)
                                   .Select(ip =>  Enumerable.Range(0, throttleSettings.MaxRequestsPerIp * 2)
                                            .Select(async _ =>
                                             {
                                                 var result = await apiService.GetResource(new("127.0.0." + ip, "id1"));
                                                 return new { Ip = ip, Result = result };
                                             }))
                                   .SelectMany(x => x));

        var succeededCount = results.GroupBy(x => x.Ip).Select(x => x.Count(r => r.Result.Success));

        Assert.All(succeededCount, succeededPerRequest => Assert.Equal(throttleSettings.MaxRequestsPerIp, succeededPerRequest));
    }

    [Fact]
    public async Task CachingWorksFor_DifferentAsyncRequesters()
    {
        var timeProvider = GetDefaultTimeProvider();

        var resourceCallCounter = 0;

        var resourceProvider = new IncludedBasicUnitTests.InjectedResourceProvider<int>(_ =>
                                    {
                                        Interlocked.Increment(ref resourceCallCounter);

                                        return 1;
                                    },
                                    (_, _) => { });

        var throttleSettings = new ThrottleSettings()
        {
            MaxRequestsPerIp = 500,
            ThrottleInterval = TimeSpan.FromMinutes(1),
            BanTimeOut = TimeSpan.FromMinutes(1)
        };

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        await Task.WhenAll(Enumerable.Range(0, throttleSettings.MaxRequestsPerIp)
                                                   .Select(_ => apiService.GetResource(new("127.0.0.1", "id1"))));

        Assert.Equal(1, resourceCallCounter);
    }

    [Fact]
    public async Task WritingAccessFromMultipleAsynchronousRequests_IsDoneExclusively()
    {
        var timeProvider = GetDefaultTimeProvider();

        var resourceCallCounterLock = new object();
        var currentResourceCallsAtATimeCount = 0;
        var maximumResourceCallsAtATimeCount = 0;

        var resourceProvider = new IncludedBasicUnitTests.InjectedResourceProvider<int>(_ => 1,
                                    (_, _) => 
                                    {
                                        lock (resourceCallCounterLock)
                                        {
                                            Interlocked.Increment(ref currentResourceCallsAtATimeCount);

                                            if (maximumResourceCallsAtATimeCount < currentResourceCallsAtATimeCount)
                                                maximumResourceCallsAtATimeCount = currentResourceCallsAtATimeCount;
                                        }

                                        Thread.Sleep(1000);

                                        lock (resourceCallCounterLock)
                                        {
                                            if (maximumResourceCallsAtATimeCount < currentResourceCallsAtATimeCount)
                                                maximumResourceCallsAtATimeCount = currentResourceCallsAtATimeCount;

                                            Interlocked.Decrement(ref currentResourceCallsAtATimeCount);
                                        }
                                    });

        var throttleSettings = new ThrottleSettings
        {
            MaxRequestsPerIp = 10,
            ThrottleInterval = TimeSpan.FromMinutes(1),
            BanTimeOut = TimeSpan.MaxValue
        };

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);;

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
                                     .Select(_ => Task.Run(() => apiService.AddOrUpdateResource(new("127.0.0.1", "id1", 1)))));

        // making sure all requests succeeded
        Assert.All(results, r => Assert.True(r.Success));
        Assert.Equal(1, maximumResourceCallsAtATimeCount);
    }

    [Fact]
    public async Task WritingAccessFromMultipleAsynchronousRequests_ForMultipleResources_Allowed()
    {
        var timeProvider = GetDefaultTimeProvider();

        var resourceCallCounterLock = new object();
        var currentResourceCallsAtATimeCount = 0;
        var maximumResourceCallsAtATimeCount = 0;

        var resourceProvider = new IncludedBasicUnitTests.InjectedResourceProvider<int>(_ => 1,
                                    (_, _) => 
                                    {
                                        lock (resourceCallCounterLock)
                                        {
                                            Interlocked.Increment(ref currentResourceCallsAtATimeCount);

                                            if (maximumResourceCallsAtATimeCount < currentResourceCallsAtATimeCount)
                                                maximumResourceCallsAtATimeCount = currentResourceCallsAtATimeCount;
                                        }

                                        Thread.Sleep(1000);

                                        lock (resourceCallCounterLock)
                                        {
                                            if (maximumResourceCallsAtATimeCount < currentResourceCallsAtATimeCount)
                                                maximumResourceCallsAtATimeCount = currentResourceCallsAtATimeCount;

                                            Interlocked.Decrement(ref currentResourceCallsAtATimeCount);
                                        }
                                    });

        var throttleSettings = new ThrottleSettings
        {
            MaxRequestsPerIp = 10,
            ThrottleInterval = TimeSpan.FromMinutes(1),
            BanTimeOut = TimeSpan.MaxValue
        };

        var apiService = ApiServiceFactory.CreateApiService(throttleSettings, resourceProvider, timeProvider);

        var results = await Task.WhenAll(Enumerable.Range(1, 2)
                                     .Select(id => Enumerable.Range(0, 5)
                                             .Select(_ => Task.Run(() => apiService.AddOrUpdateResource(new("127.0.0.1", "id" + id, 1)))))
                                     .SelectMany(x => x));

        // making sure all requests succeeded
        Assert.All(results, r => Assert.True(r.Success));
        // expecting +1 active writing request at a time per resource type
        Assert.Equal(2, maximumResourceCallsAtATimeCount);
    }

    private static ITimeProvider GetDefaultTimeProvider() => new IncludedBasicUnitTests.ManualTimeProvider { UtcNow = DateTime.UnixEpoch };

    private ThrottleSettings DefaultThrottleSettings => new()
    {
        ThrottleInterval = TimeSpan.FromMinutes(1),
        MaxRequestsPerIp = 2,
        BanTimeOut = TimeSpan.FromMinutes(1),
    };
}
