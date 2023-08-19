using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace ZiraLink.Client.Services
{
    public class WebSocketService
    {
        private ConcurrentDictionary<string, WebSocket> _webSockets = new ConcurrentDictionary<string, WebSocket>();
        private ConcurrentDictionary<string, Task> _webSocketReceiverTasks = new ConcurrentDictionary<string, Task>();

        public async Task<WebSocket> InitializeWebSocketAsync(IModel channel, string host, Uri internalUri)
        {
            if (_webSockets.ContainsKey(host))
                return _webSockets[host];

            var webSocket = new ClientWebSocket();

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
            _webSockets.TryAdd(host, webSocket);

            var task = Task.Run(async () => await InitializeWebSocketReceiverAsync(webSocket, channel, host));
            _webSocketReceiverTasks.TryAdd(host, task);

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
                _webSockets.TryRemove(host, out var _);
                _webSocketReceiverTasks.TryRemove(host, out var _);
            }
        }
    }
}
