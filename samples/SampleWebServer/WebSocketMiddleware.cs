using System.Net.WebSockets;
using System.Text;

namespace SampleWebServer
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await Echo(webSocket);
            }
            else
            {
                await _next(context);
            }
        }

        private static async Task Echo(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var message = default(string);
            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            int index = 0;

            while (!receiveResult.CloseStatus.HasValue)
            {
                var bytes = Encoding.Default.GetBytes($"{++index}: {message}");
                var arraySegment = new ArraySegment<byte>(bytes);

                await webSocket.SendAsync(arraySegment,
                    receiveResult.MessageType,
                    true,
                    CancellationToken.None);

                receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                message = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            }

            await webSocket.CloseAsync(
                receiveResult.CloseStatus.Value,
                receiveResult.CloseStatusDescription,
                CancellationToken.None);
        }
    }
}
