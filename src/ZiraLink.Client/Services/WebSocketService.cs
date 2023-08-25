using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using ZiraLink.Client.Models;
using ZiraLink.Client.Framework.Services;
using Microsoft.Extensions.Caching.Memory;

namespace ZiraLink.Client.Services
{
    public class WebSocketService: IWebSocketService
    {
        private readonly IWebSocketFactory _webSocketFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IModel _channel;

        public WebSocketService(IWebSocketFactory webSocketFactory, IMemoryCache memoryCache, IModel channel)
        {
            _webSocketFactory = webSocketFactory;
            _memoryCache = memoryCache;
            _channel = channel;
        }

        public async Task<IWebSocket> InitializeWebSocketAsync(string host, Uri internalUri)
        {
            if (_memoryCache.TryGetValue(host, out IWebSocket webSocket))
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
            _memoryCache.Set(host, webSocket);

            var task = Task.Run(async () => await InitializeWebSocketReceiverAsync(webSocket, host));
            _memoryCache.Set($"task_{host}", task);

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
                    if (receiveResult.CloseStatus.HasValue)
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
                _memoryCache.Remove(host);
                _memoryCache.Remove($"task_{host}");
            }
        }
    }
}
