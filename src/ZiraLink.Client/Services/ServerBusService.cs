using System.Text;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Services;

namespace ZiraLink.Client.Services
{
    public class ServerBusService : IServerBusService
    {
        private readonly IModel _channel;

        public ServerBusService(IModel channel)
        {
            _channel = channel;
        }

        public void RequestAppProjects(string username)
        {
            var exchangeName = "server_bus";
            var queueName = "server_bus";

            _channel.ExchangeDeclare(exchangeName, "direct", false, false, null);
            _channel.QueueDeclare(queueName, false, false, false, null);
            _channel.QueueBind(queueName, exchangeName, string.Empty, null);

            var body = Encoding.UTF8.GetBytes("GET_APPPROJECTS");
            var properties = _channel.CreateBasicProperties();
            properties.Headers = new Dictionary<string, object>()
            {
                { "username", username }
            };
            _channel.BasicPublish(exchangeName, string.Empty, properties, body);
        }
    }
}
