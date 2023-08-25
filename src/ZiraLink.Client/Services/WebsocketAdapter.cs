using System.Net.WebSockets;

namespace ZiraLink.Client.Services
{
    public class WebsocketAdapter
    {
        private readonly ClientWebSocket _websocket;

        public ClientWebSocketOptions Options { get => _websocket.Options; }

        public WebsocketAdapter(ClientWebSocket websocket)
        {
            _websocket = websocket;
        }

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => await _websocket.ConnectAsync(uri, cancellationToken);
        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => await _websocket.ReceiveAsync(buffer, cancellationToken);
        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => await _websocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

    }
}
