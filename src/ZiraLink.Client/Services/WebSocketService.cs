using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using ZiraLink.Client.Models;
using ZiraLink.Client.Framework.Services;

namespace ZiraLink.Client.Services
{
    public class WebSocketService: IWebSocketService
    {
        private readonly IWebSocketFactory _webSocketFactory;
        private readonly ICache _cache;
        private readonly IModel _channel;

        public WebSocketService(IWebSocketFactory webSocketFactory, ICache cache, IModel channel)
        {
            _webSocketFactory = webSocketFactory;
            _cache = cache;
            _channel = channel;
        }

        public async Task<IWebSocket> InitializeWebSocketAsync(string host, Uri internalUri)
        {
            if (_cache.TryGetWebSocket(host, out IWebSocket webSocket))
                return webSocket;

            webSocket = _webSocketFactory.CreateClientWebSocket();

            var webSocketUri = internalUri;
            var webSocketUriBuilder = default(UriBuilder);
            if (internalUri.Scheme == "https")
            {
                webSocketUriBuilder = new UriBuilder(internalUri)
                {
                    Scheme = "wss"
                };
            }
            else
            {
                webSocketUriBuilder = new UriBuilder(internalUri)
                {
                    Scheme = "ws"
                };
            }

            await webSocket.ConnectAsync(webSocketUriBuilder.Uri, default);
            _cache.SetWebSocket(host, webSocket);

            var task = Task.Run(async () => await InitializeWebSocketReceiverAsync(webSocket, host));

            return webSocket;
        }

        private async Task InitializeWebSocketReceiverAsync(IWebSocket webSocket, string host)
        {
            try
            {
                var queueName = "websocket_client_bus";
                var exchangeName = "websocket_bus";

                var buffer = new byte[1024 * 4];
                do
                {
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (receiveResult.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                        break;

                    var webSocketData = new WebSocketData
                    {
                        Payload = buffer,
                        PayloadCount = receiveResult.Count,
                        MessageType = receiveResult.MessageType,
                        EndOfMessage = receiveResult.EndOfMessage
                    };

                    var properties = _channel.CreateBasicProperties();
                    properties.MessageId = "";
                    var headers = new Dictionary<string, object>();
                    headers.Add("IntUrl", "");
                    headers.Add("Host", host);
                    properties.Headers = headers;
                    var message = JsonSerializer.Serialize(webSocketData);

                    _channel.BasicPublish(exchange: exchangeName, routingKey: queueName, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
                } while (true);
            }
            finally
            {
                _cache.RemoveWebSocket(host);
            }
        }
    }
}
