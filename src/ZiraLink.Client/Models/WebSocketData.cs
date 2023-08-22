using System.Net.WebSockets;

namespace ZiraLink.Client.Models
{
    public class WebSocketData
    {
        public byte[] Payload { get; set; }
        public int PayloadCount { get; set; }
        public WebSocketMessageType MessageType { get; set; }
        public bool EndOfMessage { get; set; }
    }
}
