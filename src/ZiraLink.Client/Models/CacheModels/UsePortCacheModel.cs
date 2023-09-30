using System.Net.Sockets;

namespace ZiraLink.Client.Models.CacheModels
{
    public record UsePortCacheModel (TcpClient TcpClient, Task HandleSocketResponsesTask, Task HandleIncommingRequestsTask);
}
