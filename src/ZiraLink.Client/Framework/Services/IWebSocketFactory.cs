using ZiraLink.Client.Services;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketFactory
    {
        IWebSocket CreateClientWebSocket();
    }
}
