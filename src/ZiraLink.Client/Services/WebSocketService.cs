using System.Net.WebSockets;
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
        private readonly IMemoryCache _webSockets;
        private readonly IMemoryCache _webSocketReceiverTasks;

        public WebSocketService(IWebSocketFactory webSocketFactory, IMemoryCache webSockets, IMemoryCache webSocketReceiverTasks)
        {
            _webSocketFactory = webSocketFactory;
            _webSockets = webSockets;
            _webSocketReceiverTasks = webSocketReceiverTasks;
        }

        public async Task<WebSocket> InitializeWebSocketAsync(IModel channel, string host, Uri internalUri)
        {
            if (_webSockets.TryGetValue(host, out ClientWebSocket webSocket))
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
            _webSockets.Set(host, webSocket);

            var task = Task.Run(async () => await InitializeWebSocketReceiverAsync(webSocket, channel, host));
            _webSocketReceiverTasks.Set(host, task);

            return webSocket;
        }

        private async Task InitializeWebSocketReceiverAsync(WebSocket webSocket, IModel channel, string host)
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

                    var properties = channel.CreateBasicProperties();
                    properties.MessageId = "";
                    var headers = new Dictionary<string, object>();
                    headers.Add("IntUrl", "");
                    headers.Add("Host", host);
                    properties.Headers = headers;
                    var message = JsonSerializer.Serialize(webSocketData);

                    channel.BasicPublish(exchange: exchangeName, routingKey: queueName, basicProperties: properties, body: Encoding.UTF8.GetBytes(message));
                } while (true);
            }
            finally
            {
                _webSockets.Remove(host);
                _webSocketReceiverTasks.Remove(host);
            }
        }
    }
}
