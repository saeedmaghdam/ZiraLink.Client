using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Models;
using ZiraLink.Client.Models.CacheModels;

namespace ZiraLink.Client.Services
{
    public class Cache : ICache
    {
        private readonly IMemoryCache _memoryCache;

        public Cache(IMemoryCache memoryCache) => _memoryCache = memoryCache;

        public IWebSocket SetWebSocket(string host, IWebSocket value) => _memoryCache.Set($"ws:{host}", value);
        public bool TryGetWebSocket(string host, out IWebSocket value) => _memoryCache.TryGetValue($"ws:{host}", out value);
        public void RemoveWebSocket(string host) => _memoryCache.Remove($"ws:{host}");
        public void SetAppProjects(List<AppProjectDto> appProjects) => _memoryCache.Set("appprojects", appProjects);
        public bool TryGetAppProjects(out List<AppProjectDto> appProjects) => _memoryCache.TryGetValue("appprojects", out appProjects);
        public void SetTcpListener(int port, TcpListener tcpListener) => _memoryCache.Set(port, tcpListener);
        public bool TryGetTcpListener(int port, out TcpListener tcpListener) => _memoryCache.TryGetValue(port, out tcpListener);
        public void SetUsePortModel(int port, UsePortCacheModel usePortCacheModel) => _memoryCache.Set(port, usePortCacheModel);
        public bool TryGetUsePortModel(int port, out UsePortCacheModel usePortCacheModel) => _memoryCache.TryGetValue(port, out usePortCacheModel);
        public void SetSharePortModel(string useportUsername, int useportPort, SharePortCacheModel sharePortCacheModel) => _memoryCache.Set($"{useportUsername}_{useportPort}", sharePortCacheModel);
        public bool TryGetSharePortModel(string useportUsername, int useportPort, out SharePortCacheModel sharePortCacheModel) => _memoryCache.TryGetValue($"{useportUsername}_{useportPort}", out sharePortCacheModel);
    }
}
