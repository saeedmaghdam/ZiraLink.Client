using System.Net.WebSockets;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketFactory
    {
        ClientWebSocket CreateClientWebSocket();
    }
}
