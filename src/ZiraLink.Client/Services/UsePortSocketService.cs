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
    public class UsePortSocketService : IUsePortSocketService
    {
        private readonly IModel _channel;
        private readonly ICache _cache;

        public UsePortSocketService(IModel channel, ICache cache)
        {
            _channel = channel;
            _cache = cache;
        }

        public void Initialize(string username, List<AppProjectDto> appProjects, CancellationToken cancellationToken)
        {
            foreach (var appProject in appProjects.Where(x => x.AppProjectType == AppProjectType.UsePort))
            {
                if (_cache.TryGetTcpListener(appProject.InternalPort, out var tcpListener))
                    continue;

                tcpListener = new TcpListener(IPAddress.Any, appProject.InternalPort);
                tcpListener.Server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                tcpListener.Start();

                _cache.SetTcpListener(appProject.InternalPort, tcpListener);

                var task = Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var client = await tcpListener.AcceptTcpClientAsync(cancellationToken);
                        var clientStream = client.GetStream();
                        var connectionId = Guid.NewGuid().ToString();

                        var handleIncommingRequestsTask = Task.Run(async () => await HandleTcpClientIncommingRequestsAsync(username, appProject.InternalPort, connectionId, clientStream, cancellationToken));

                        _cache.SetUsePortModel(appProject.InternalPort, connectionId, new(client, handleIncommingRequestsTask));
                    }
                });
            }

            HandleSocketResponses(username, cancellationToken);
        }

        private async Task HandleTcpClientIncommingRequestsAsync(string username, int port, string connectionId, NetworkStream clientStream, CancellationToken cancellationToken)
        {
            var queueName = "server_network_requests";
            var exchangeName = "server_network_requests";

            _channel.ExchangeDeclare(exchangeName, "direct", false, false, null);
            _channel.QueueDeclare(queueName, false, false, false, null);
            _channel.QueueBind(queueName, exchangeName, string.Empty, null);

            byte[] buffer = new byte[1024];
            int bytesRead;
            while (true)
            {
                while ((bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    var packetModel = new PacketModel();
                    packetModel.Buffer = buffer;
                    packetModel.Count = bytesRead;
                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packetModel));

                    var properties = _channel.CreateBasicProperties();
                    properties.Headers = new Dictionary<string, object>()
                    {
                        { "useport_username", username },
                        { "useport_port", port },
                        { "useport_connectionid", connectionId }
                    };

                    _channel.BasicPublish(exchange: exchangeName, routingKey: string.Empty, basicProperties: properties, body: body);
                }
            }
        }

        private void HandleSocketResponses(string username, CancellationToken cancellationToken)
        {
            var queueName = $"{username}_client_useport_network_packets";

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


                if (!_cache.TryGetUsePortModel(useportPort, useportConnectionId, out var useportModel))
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                if (!useportModel.TcpClient.Connected)
                {
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                try
                {
                    await useportModel.TcpClient.GetStream().WriteAsync(packetModel.Buffer, 0, packetModel.Count, cancellationToken);
                }
                catch (Exception ex)
                {
                    // ignored
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }
    }
}
