using RabbitMQ.Client;
using System.Net.WebSockets;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketService
    {
        Task<WebSocket> InitializeWebSocketAsync(IModel channel, string host, Uri internalUri);
    }
}
