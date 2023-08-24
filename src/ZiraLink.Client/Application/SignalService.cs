using ZiraLink.Client.Framework.Application;

namespace ZiraLink.Client.Application
{
    public class SignalService : ISignalService
    {
        private ManualResetEvent mre = new ManualResetEvent(false);

        public void WaitOne() => mre.WaitOne();

        public void Set() => mre.Set();
    }
}
