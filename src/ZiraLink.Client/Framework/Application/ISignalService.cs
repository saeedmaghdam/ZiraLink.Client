namespace ZiraLink.Client.Framework.Application
{
    public interface ISignalService
    {
        void WaitOne();
        void Set();
    }
}
