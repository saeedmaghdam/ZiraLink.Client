using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using ZiraLink.Client.Framework.Helpers;

namespace ZiraLink.Client.Helpers
{
    public class CertificateHelper : ICertificateHelper
    {
        private readonly string _pfxPath;
        private readonly string _pfxPassword;

        public CertificateHelper(IConfiguration configuration)
        {
            _pfxPath = configuration["ASPNETCORE_Kestrel__Certificates__Default__Path"];
            _pfxPassword = configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"];
        }

        public void InstallCertificate()
        {
            try
            {
                X509Certificate2Collection certificates = new X509Certificate2Collection();
                certificates.Import(_pfxPath, _pfxPassword, X509KeyStorageFlags.Exportable);

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    InstallCertificateOnWindows(certificates);
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    InstallCertificateOnLinux(certificates);
                }
                else
                {
                    Console.WriteLine("Unsupported platform.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static bool CertificateExists(X509Store store, X509Certificate2 certificate)
        {
            return store.Certificates.Cast<X509Certificate2>().Any(cert => cert.Thumbprint == certificate.Thumbprint);
        }

        private static void InstallCertificateOnWindows(X509Certificate2Collection certificates)
        {
            try
            {
                using (X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                {
                    store.Open(OpenFlags.ReadWrite);

                    foreach (X509Certificate2 certificate in certificates)
                    {
                        if (!CertificateExists(store, certificate))
                        {
                            store.Add(certificate);
                            Console.WriteLine($"Certificate '{certificate.Subject}' installed as a trusted root authority on Windows.");
                        }
                        else
                        {
                            Console.WriteLine($"Certificate '{certificate.Subject}' already exists as a trusted root authority on Windows.");
                        }
                    }

                    store.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing certificates on Windows: {ex.Message}");
            }
        }

        private static void InstallCertificateOnLinux(X509Certificate2Collection certificates)
        {
            try
            {
                foreach (X509Certificate2 certificate in certificates)
                {
                    if (!CertificateExistsInTrustedRoot(certificate))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo("sudo", $"cp \"{certificate}\" /usr/local/share/ca-certificates/{certificate.Thumbprint}.crt");
                        Process process = new Process { StartInfo = psi };
                        process.Start();
                        process.WaitForExit();

                        Process.Start("sudo", "update-ca-certificates");
                        Console.WriteLine($"Certificate '{certificate.Subject}' installed as a trusted root authority on Linux.");
                    }
                    else
                    {
                        Console.WriteLine($"Certificate '{certificate.Subject}' already exists as a trusted root authority on Linux.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing certificates on Linux: {ex.Message}");
            }
        }

        private static bool CertificateExistsInTrustedRoot(X509Certificate2 certificate)
        {
            return File.Exists($"/usr/local/share/ca-certificates/{certificate.Thumbprint}.crt");
        }
    }
}
