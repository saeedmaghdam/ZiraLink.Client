using ZiraLink.Client.Services;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketService
    {
        Task<IWebSocket> InitializeWebSocketAsync(string host, Uri internalUri);
    }
}
