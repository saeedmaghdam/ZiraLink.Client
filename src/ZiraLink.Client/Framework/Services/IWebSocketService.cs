using RabbitMQ.Client;
using System.Net.WebSockets;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketService
    {
        Task<ClientWebSocket> InitializeWebSocketAsync(string host, Uri internalUri);
    }
}
