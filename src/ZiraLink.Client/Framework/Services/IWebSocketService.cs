using ZiraLink.Client.Services;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketService
    {
        Task<WebsocketAdapter> InitializeWebSocketAsync(string host, Uri internalUri);
    }
}
