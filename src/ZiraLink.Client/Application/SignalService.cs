namespace ZiraLink.Client.Application
{
    public class SignalService
    {
        private ManualResetEvent mre = new ManualResetEvent(false);

        public void WaitOne() => mre.WaitOne();

        public void Set() => mre.Set();
    }
}
