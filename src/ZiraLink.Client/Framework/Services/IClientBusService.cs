namespace ZiraLink.Client.Framework.Services
{
    public interface IClientBusService
    {
        void InitializeConsumer(string username, CancellationToken cancellationToken);
    }
}
