using System.Net.Sockets;

namespace ZiraLink.Client.Models.CacheModels
{
    public record SharePortCacheModel(TcpClient TcpClient, Task HandleTcpClientResponsesTask);
}
