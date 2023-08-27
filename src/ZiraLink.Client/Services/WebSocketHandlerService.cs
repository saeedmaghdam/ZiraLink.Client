using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Models;

namespace ZiraLink.Client.Services
{
    public class WebSocketHandlerService : IWebSocketHandlerService
    {
        private readonly ILogger<WebSocketHandlerService> _logger;
        private readonly IModel _channel;
        private readonly IWebSocketService _webSocketService;

        public WebSocketHandlerService(ILogger<WebSocketHandlerService> logger, IModel channel, IWebSocketService webSocketService)
        {
            _logger = logger;
            _channel = channel;
            _webSocketService = webSocketService;
        }

        public void InitializeWebSocketConsumer(string username)
        {
            _logger.LogInformation("Starting websocket request handler ...");

            var serverBusQueueName = $"{username}_websocket_server_bus";
            _channel.QueueDeclare(queue: serverBusQueueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            // Start consuming requests
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var requestID = ea.BasicProperties.MessageId;
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var requestModel = JsonSerializer.Deserialize<WebSocketData>(body);

                    if (!ea.BasicProperties.Headers.TryGetValue("IntUrl", out var internalUrlByteArray))
                        throw new ApplicationException("Internal url not found");
                    if (!ea.BasicProperties.Headers.TryGetValue("Host", out var hostByteArray))
                        throw new ApplicationException("Host not found");
                    var internalUri = new Uri(Encoding.UTF8.GetString((byte[])internalUrlByteArray));
                    var host = Encoding.UTF8.GetString((byte[])hostByteArray);

                    var webSocket = await _webSocketService.InitializeWebSocketAsync(host, internalUri);
                    var arraySegment = new ArraySegment<byte>(requestModel.Payload, 0, requestModel.PayloadCount);
                    await webSocket.SendAsync(arraySegment,
                        requestModel.MessageType,
                        requestModel.EndOfMessage,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
                finally
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            };

            // Initialize publisher
            var queueName = "websocket_client_bus";
            var exchangeName = "websocket_bus";

            _channel.ExchangeDeclare(exchange: exchangeName,
                type: "direct",
                durable: false,
                autoDelete: false,
                arguments: null);

            _channel.QueueDeclare(queue: queueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            _channel.QueueBind(queue: queueName,
                exchange: exchangeName,
                routingKey: queueName,
                arguments: null);

            // Start consumer
            _channel.BasicConsume(queue: serverBusQueueName, autoAck: false, consumer: consumer);
        }
    }
}
