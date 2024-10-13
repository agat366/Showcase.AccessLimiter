using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace CleoAssignment.ApiService.Implementation;

/// <summary>
/// Handles resources caching.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ResourceRepository<T>
{
    private readonly IResourceProvider<T> _resourceProvider;
    private readonly ConcurrentDictionary<string, RepositoryCachedItem> _cache = new();

    public ResourceRepository(IResourceProvider<T> resourceProvider)
    {
        _resourceProvider = resourceProvider;
    }

    /// <summary>
    /// Retrieves the resource from storage as well as caches the resource first, and/or retrieves it from the cache.
    /// (Rather suppose to be Task<T> in real life, but we can pretend this the most appropriate case for code simplicity for now).
    /// </summary>
    /// <param name="resourceId"></param>
    /// <returns></returns>
    public T GetResource(string resourceId)
    {
        var cachedResource = _cache.GetOrAdd(resourceId, _ => new RepositoryCachedItem());

        if (!cachedResource.IsCached)
        {
            cachedResource.AccessLock.EnterWriteLock();
            try
            {
                if(!cachedResource.IsCached)
                {
                    var resource = _resourceProvider.GetResource(resourceId);
                    cachedResource.Resource = resource;
                }
            }
            finally
            {
                cachedResource.AccessLock.ExitWriteLock();
            }
        }

        cachedResource.AccessLock.EnterReadLock();
        try
        {
            // cachedResource.Resource ??= _resourceProvider.GetResource(resourceId);
            
            return cachedResource.Resource;
        }
        finally
        {
            cachedResource.AccessLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Stores into storage as well as updates it in cache.
    /// Processes the update exclusively with write access.
    /// </summary>
    /// <param name="resourceId"></param>
    /// <param name="resource"></param>
    public void AddOrUpdateResource(string resourceId, T resource)
    {
        var cachedResource = _cache.GetOrAdd(resourceId, _ => new RepositoryCachedItem());

        cachedResource.AccessLock.EnterWriteLock();
        try
        {
            _resourceProvider.AddOrUpdateResource(resourceId, resource);
            cachedResource.Resource = resource;
        }
        finally
        {
            cachedResource.AccessLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Contains the cached resource as well as its update access lock.
    /// </summary>
    private class RepositoryCachedItem
    {
        private T _resource;
        public T Resource
        {
            get => _resource;
            set
            {
                _resource = value;
                IsCached = true;
            }
        }

        public ReaderWriterLockSlim AccessLock { get; } = new();

        public bool IsCached { get; private set; }
    }
}

