using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using ZiraLink.Client.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Client.Framework.Services;

namespace ZiraLink.Client.Services
{
    public class SharePortSocketService : ISharePortSocketService
    {
        private readonly IModel _channel;
        private readonly ICache _cache;

        public SharePortSocketService(IModel channel, ICache cache)
        {
            _channel = channel;
            _cache = cache;
        }

        public void Initialize(string username, List<AppProjectDto> appProjects, CancellationToken cancellationToken)
        {
            var queueName = $"{username}_client_shareport_network_packets";

            _channel.QueueDeclare(queueName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var packetModel = JsonSerializer.Deserialize<PacketModel>(message);

                var useportUsername = Encoding.UTF8.GetString(ea.BasicProperties.Headers["useport_username"] as byte[]);
                var useportPort = int.Parse(ea.BasicProperties.Headers["useport_port"].ToString()!);
                var useportConnectionId = Encoding.UTF8.GetString(ea.BasicProperties.Headers["useport_connectionid"] as byte[]);

                if (!_cache.TryGetSharePortModel(useportUsername, useportPort, useportConnectionId, out var sharePortCacheModel))
                {
                    var client = new TcpClient();
                    await client.ConnectAsync(new IPEndPoint(IPAddress.Parse("127.0.0.1"), int.Parse(ea.BasicProperties.Headers["sharedport_port"].ToString()!)));
                    var handleTcpClientResponsesTask = Task.Run(async () => await HandleTcpClientResponsesAsync(client, useportUsername, useportPort, useportConnectionId));

                    sharePortCacheModel = new(client, handleTcpClientResponsesTask);
                    _cache.SetSharePortModel(useportUsername, useportPort, useportConnectionId, sharePortCacheModel);
                }

                await sharePortCacheModel.TcpClient.GetStream().WriteAsync(packetModel.Buffer, 0, packetModel.Count, cancellationToken);

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }

        private async Task HandleTcpClientResponsesAsync(TcpClient tcpClient, string useportUsername, int useportPort, string connectionId)
        {
            var queueName = "server_network_responses";
            var exchangeName = "server_network_responses";

            _channel.ExchangeDeclare(exchangeName, "direct", false, false, null);
            _channel.QueueDeclare(queueName, false, false, false, null);
            _channel.QueueBind(queueName, exchangeName, string.Empty, null);

            byte[] buffer = new byte[1024];
            int bytesRead;
            while (true)
            {
                while ((bytesRead = await tcpClient.GetStream().ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    var packetModel = new PacketModel();
                    packetModel.Buffer = buffer;
                    packetModel.Count = bytesRead;
                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packetModel));

                    var properties = _channel.CreateBasicProperties();
                    properties.Headers = new Dictionary<string, object>()
                    {
                        { "useport_username", useportUsername },
                        { "useport_port", useportPort },
                        { "useport_connectionid", connectionId }
                    };

                    _channel.BasicPublish(exchange: exchangeName, routingKey: string.Empty, basicProperties: properties, body: body);
                }
            }
        }
    }
}
