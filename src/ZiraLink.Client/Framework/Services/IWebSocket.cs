using System.Net.WebSockets;

namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocket
    {
        ClientWebSocketOptions Options { get; }
        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
        Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
        Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
    }
}
