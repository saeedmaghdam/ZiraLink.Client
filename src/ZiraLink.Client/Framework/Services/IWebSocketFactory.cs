using ZiraLink.Client.Services;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketFactory
    {
        WebsocketAdapter CreateClientWebSocket();
    }
}
