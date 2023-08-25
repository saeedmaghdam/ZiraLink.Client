using System.Net.WebSockets;
using ZiraLink.Client.Framework.Services;

namespace ZiraLink.Client.Services
{
    public class WebSocketFactory : IWebSocketFactory
    {
        public IWebSocket CreateClientWebSocket()
        {
            var webSocket = new ClientWebSocket();
            webSocket.Options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            return new WebsocketAdapter(webSocket);
        }
    }
}
