using System;
using CleoAssignment.ApiService.Implementation;

namespace CleoAssignment.ApiService;

public static class ApiServiceFactory
{
    public static IApiService<T> CreateApiService<T>(ThrottleSettings throttleSettings,
                                                     IResourceProvider<T> resourceProvider,
                                                     ITimeProvider timeProvider)
        where T : notnull
    {
        var limiter = new ResourceAccessLimiter<string>(timeProvider, throttleSettings);
        var repository = new ResourceRepository<T>(resourceProvider);

        return new DefaultApiService<T>(repository, limiter);
    }
}