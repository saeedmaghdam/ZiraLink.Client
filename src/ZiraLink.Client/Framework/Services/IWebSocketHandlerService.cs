namespace ZiraLink.Client.Framework.Services
{
    public interface IWebSocketHandlerService
    {
        Task InitializeWebSocketConsumerAsync(string username);
    }
}
