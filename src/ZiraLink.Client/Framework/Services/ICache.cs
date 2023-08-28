using System.Net.WebSockets;

namespace ZiraLink.Client.Framework.Services
{
    public interface ICache
    {
        IWebSocket SetWebSocket(string host, IWebSocket value);
        bool TryGetWebSocket(string host, out IWebSocket value);
        void RemoveWebSocket(string host);
    }
}
