using ZiraLink.Client.Framework.Helpers;

namespace ZiraLink.Client.Helpers
{
    public class HostsHelper : IHostsHelper
    {
        public void ConfigureDns()
        {
            string hostFile = "";
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                hostFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers\\etc\\hosts");
            }
            else if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                hostFile = "/etc/hosts";
            }
            else
            {
                Console.WriteLine("Unsupported platform.");
                return;
            }

            string newHostEntry = "127.0.0.1 client.ziralink.aghdam.nl";

            if (!HostEntryExists(hostFile, "client.ziralink.aghdam.nl"))
            {
                try
                {
                    using (StreamWriter writer = File.AppendText(hostFile))
                    {
                        writer.WriteLine(newHostEntry);
                    }
                    Console.WriteLine("Host entry added successfully.");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine("Insufficient privileges. Run the application as an administrator/root.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Host entry already exists.");
            }
        }

        private static bool HostEntryExists(string hostFilePath, string hostname)
        {
            string[] lines = File.ReadAllLines(hostFilePath);
            foreach (string line in lines)
            {
                if (line.Contains(hostname))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
