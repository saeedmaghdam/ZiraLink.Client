using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using DotNet.Testcontainers.Builders;

namespace ZiraLink.Client.IntegrationTests.Fixtures
{
    [ExcludeFromCodeCoverage]
    public class InfrastructureFixture
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _caCertificatePath;
        private readonly string _certificatePath;
        private readonly string _certificatePassword;

        public string CACertificatePath => _caCertificatePath;
        public string CertificatePath => _certificatePath;
        public string CertificatePassword => _certificatePassword;

        public InfrastructureFixture()
        {
            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            _caCertificatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "certs", "localhost", "ca.crt");
            _certificatePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "certs", "localhost", "server.pfx");
            _certificatePassword = "son";

            InitializeRabbitMq();
            InitializeSampleWebServer();
        }

        public HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                string expectedThumbprint = "10CE57B0083EBF09ED8E53CF6AC33D49B3A76414";
                if (certificate!.GetCertHashString() == expectedThumbprint)
                    return true;

                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                return false;
            };
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.SslProtocols = SslProtocols.Tls12;
            handler.ClientCertificates.Add(new X509Certificate2(CACertificatePath));
            handler.ClientCertificates.Add(new X509Certificate2(CertificatePath, CertificatePassword));
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost:9443/")
            };

            return httpClient;
        }

        private void InitializeRabbitMq()
        {
            var container = new ContainerBuilder()
              .WithImage("bitnami/rabbitmq:latest")
              .WithPortBinding(5872, 5672)
              .WithEnvironment("RABBITMQ_USERNAME", "user")
              .WithEnvironment("RABBITMQ_PASSWORD", "Pass123$")
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672).UntilPortIsAvailable(15672))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeSampleWebServer()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/samplewebserver:main")
              .WithPortBinding(9080, 80)
              .WithPortBinding(9443, 443)
              .WithEnvironment("ASPNETCORE_URLS", "http://+:80;https://+:443")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "443")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }
    }
}
