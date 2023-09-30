using System.Net.Sockets;
using ZiraLink.Client.Models;
using ZiraLink.Client.Models.CacheModels;

namespace ZiraLink.Client.Framework.Services
{
    public interface ICache
    {
        IWebSocket SetWebSocket(string host, IWebSocket value);
        bool TryGetWebSocket(string host, out IWebSocket value);
        void RemoveWebSocket(string host);
        void SetAppProjects(List<AppProjectDto> appProjects);
        bool TryGetAppProjects(out List<AppProjectDto> appProjects);
        void SetTcpListener(int port, TcpListener tcpListener);
        bool TryGetTcpListener(int port, out TcpListener tcpListener);
        void SetUsePortModel(int port, string connectionId, UsePortCacheModel usePortCacheModel);
        bool TryGetUsePortModel(int port, string connectionId, out UsePortCacheModel usePortCacheModel);
        void SetSharePortModel(string useportUsername, int useportPort, string connectionId, SharePortCacheModel sharePortCacheModel);
        bool TryGetSharePortModel(string useportUsername, int useportPort, string connectionId, out SharePortCacheModel sharePortCacheModel);
    }
}
