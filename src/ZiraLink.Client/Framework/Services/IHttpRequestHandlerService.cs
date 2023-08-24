namespace ZiraLink.Client.Framework.Services
{
    public interface IHttpRequestHandlerService
    {
        Task InitializeHttpRequestConsumerAsync(string username);
    }
}
