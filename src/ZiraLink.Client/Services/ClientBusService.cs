using System.Text;
using System.Text.Json;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Models;

namespace ZiraLink.Client.Services
{
    public class ClientBusService : IClientBusService
    {
        private readonly IModel _channel;
        private readonly ICache _cache;
        private readonly IUsePortSocketService _usePortSocketService;
        private readonly ISharePortSocketService _sharePortSocketService;

        public ClientBusService(IModel channel, ICache cache, IUsePortSocketService usePortSocketService, ISharePortSocketService sharePortSocketService)
        {
            _channel = channel;
            _cache = cache;
            _usePortSocketService = usePortSocketService;
            _sharePortSocketService = sharePortSocketService;
        }

        public void InitializeConsumer(string username, CancellationToken cancellationToken)
        {
            var queueName = $"{username}_client_bus";

            _channel.QueueDeclare(queueName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var appProjects = JsonSerializer.Deserialize<List<AppProjectDto>>(message);
                _cache.SetAppProjects(appProjects);
                await _usePortSocketService.InitializeAsync(username, appProjects, cancellationToken);
                await _sharePortSocketService.InitializeAsync(username, appProjects, cancellationToken);

                _channel.BasicAck(ea.DeliveryTag, false);
            };

            _channel.BasicConsume(queueName, false, consumer);
        }
    }
}
