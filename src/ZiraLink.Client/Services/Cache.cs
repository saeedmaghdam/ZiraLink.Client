using Microsoft.Extensions.Caching.Memory;
using ZiraLink.Client.Framework.Services;

namespace ZiraLink.Client.Services
{
    public class Cache : ICache
    {
        private readonly IMemoryCache _memoryCache;

        public Cache(IMemoryCache memoryCache) => _memoryCache = memoryCache;

        public IWebSocket SetWebSocket(string host, IWebSocket value) => _memoryCache.Set($"ws:{host}", value);
        public bool TryGetWebSocket(string host, out IWebSocket value) => _memoryCache.TryGetValue($"ws:{host}", out value);
        public void RemoveWebSocket(string host) => _memoryCache.Remove($"ws:{host}");
    }
}
