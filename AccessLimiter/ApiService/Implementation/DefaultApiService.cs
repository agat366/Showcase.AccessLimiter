using System.Threading.Tasks;

namespace CleoAssignment.ApiService.Implementation;

public class DefaultApiService<T> : IApiService<T>
    where T : notnull // not null required for proper tracking of a resource by some id
{
    private readonly ResourceRepository<T> _repository;
    private readonly ResourceAccessLimiter<string>? _limiter;

    public DefaultApiService(ResourceRepository<T> repository, ResourceAccessLimiter<string>? limiter = null)
    {
        _repository = repository;
        _limiter = limiter;
    }

    public Task<GetResponse<T>> GetResource(GetRequest request)
    {
        if (_limiter != null)
        {
            var limiterResult = _limiter.RequestAccess(request.IpAddress);
            if (limiterResult.IsBlocked)
            {
                return Task.FromResult(new GetResponse<T>(false, default, ErrorType.TooManyRequests));
            }
        }

        var resource = _repository.GetResource(request.ResourceId);

        return Task.FromResult(new GetResponse<T>(true, resource, null));
    }

    public Task<AddOrUpdateResponse> AddOrUpdateResource(AddOrUpdateRequest<T> request)
    {
        if (_limiter != null)
        {
            var limiterResult = _limiter.RequestAccess(request.IpAddress);
            if (limiterResult.IsBlocked)
            {
                return Task.FromResult(new AddOrUpdateResponse(false, ErrorType.TooManyRequests));
            }
        }

        _repository.AddOrUpdateResource(request.ResourceId, request.Resource);

        return Task.FromResult(new AddOrUpdateResponse(true, null));
    }
}
