using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Services;
using System.Text;
using System.Text.Json;
using ZiraLink.Client.Models;
using ZiraLink.Client.Framework.Helpers;

namespace ZiraLink.Client.Services
{
    public class HttpRequestHandlerService : IHttpRequestHandlerService
    {
        private readonly ILogger<HttpRequestHandlerService> _logger;
        private readonly IModel _channel;
        private readonly IHttpHelper _httpHelper;

        public HttpRequestHandlerService(ILogger<HttpRequestHandlerService> logger, IModel channel, IHttpHelper httpHelper)
        {
            _logger = logger;
            _channel = channel;
            _httpHelper = httpHelper;
        }

        public void InitializeHttpRequestConsumer(string username)
        {
            _logger.LogInformation("Starting http request handler ...");

            var responseExchangeName = "response";
            var responseQueueName = "response_bus";
            var requestQueueName = $"{username}_request_bus";

            _channel.ExchangeDeclare(exchange: responseExchangeName,
                type: "direct",
                durable: false,
                autoDelete: false,
                arguments: null);

            _channel.QueueDeclare(queue: responseQueueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            _channel.QueueBind(queue: responseQueueName,
               exchange: responseExchangeName,
               routingKey: "",
               arguments: null);

            _channel.QueueDeclare(queue: requestQueueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            // Start consuming requests
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                _channel.BasicAck(ea.DeliveryTag, false);

                try
                {
                    var requestID = ea.BasicProperties.MessageId;
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var requestModel = JsonSerializer.Deserialize<HttpRequestModel>(body);

                    if (!ea.BasicProperties.Headers.TryGetValue("IntUrl", out var internalUrlByteArray))
                        throw new ApplicationException("Internal url not found");
                    if (!ea.BasicProperties.Headers.TryGetValue("Host", out var hostByteArray))
                        throw new ApplicationException("Host not found");
                    var internalUri = new Uri(Encoding.UTF8.GetString((byte[])internalUrlByteArray));
                    var host = Encoding.UTF8.GetString((byte[])hostByteArray);

                    var response = await _httpHelper.CreateAndSendRequestAsync(requestModel.RequestUrl, requestModel.Method, requestModel.Headers, requestModel.Bytes, internalUri);

                    var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));

                    var responseProperties = _channel.CreateBasicProperties();
                    responseProperties.MessageId = requestID;

                    _channel.BasicPublish(exchange: responseExchangeName, routingKey: "", basicProperties: responseProperties, body: responseBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
            };

            _channel.BasicConsume(queue: requestQueueName, autoAck: false, consumer: consumer);
        }
    }
}
