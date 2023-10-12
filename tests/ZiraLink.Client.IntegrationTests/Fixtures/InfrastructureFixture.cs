using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace ZiraLink.Client.IntegrationTests.Fixtures
{
    [ExcludeFromCodeCoverage]
    public class InfrastructureFixture
    {
        private readonly IConfiguration _configuration;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly string _certificatePath;
        private readonly string _certificatePassword;

        public string CertificatePath => _certificatePath;
        public string CertificatePassword => _certificatePassword;
        public const string RabbitMqHost = "localhost";
        public const int RabbitMqPort = 5872;
        public const string RabbitMqUsername = "user";
        public const string RabbitMqPassword = "Pass123$";

        public InfrastructureFixture()
        {
            var pathToExe = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables()
                .Build();

            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            _certificatePath = Path.Combine(pathToExe, "certs", "s3d-localhost-server.pfx");
            _certificatePassword = _configuration["ASPNETCORE_Kestrel__Certificates__Default__Password"]!;

            InitializeRabbitMq();
            InitializeSampleWebServer();
        }

        public HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = RemoteCertificateValidationCallback;
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.SslProtocols = SslProtocols.Tls12;
            handler.ClientCertificates.Add(new X509Certificate2(CertificatePath, CertificatePassword));
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost:9443/")
            };

            return httpClient;
        }

        public IModel CreateChannel()
        {
            var factory = new ConnectionFactory { HostName = RabbitMqHost, Port = RabbitMqPort, UserName = RabbitMqUsername, Password = RabbitMqPassword };
            var connection = factory.CreateConnection();
            return connection.CreateModel();
        }

        public async Task<ClientWebSocket> CreateWebSocketClientAsync()
        {
            var webSocketClient = new ClientWebSocket();
            webSocketClient.Options.ClientCertificates.Add(new X509Certificate2(CertificatePath, CertificatePassword));
            webSocketClient.Options.RemoteCertificateValidationCallback = RemoteCertificateValidationCallback;

            await webSocketClient.ConnectAsync(new Uri("wss://localhost:9443"), _cancellationTokenSource.Token);

            return webSocketClient;
        }

        private void InitializeRabbitMq()
        {
            var container = new ContainerBuilder()
              .WithImage("bitnami/rabbitmq:latest")
              .WithPortBinding(RabbitMqPort, 5672)
              .WithPortBinding(15872, 15672)
              .WithEnvironment("RABBITMQ_USERNAME", RabbitMqUsername)
              .WithEnvironment("RABBITMQ_PASSWORD", RabbitMqPassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672).UntilHttpRequestIsSucceeded(r => r.ForPort(15672)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private void InitializeSampleWebServer()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/sample-web-application:main")
              .WithPortBinding(9080, 80)
              .WithPortBinding(9443, 443)
              .WithEnvironment("ASPNETCORE_URLS", "http://+:80;https://+:443")
              .WithEnvironment("ASPNETCORE_HTTPS_PORT", "443")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/app/certs/s3d-localhost-server.pfx")
              .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", CertificatePassword)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)))
              .Build();

            container.StartAsync().Wait(_cancellationTokenSource.Token);
        }

        private bool RemoteCertificateValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            string expectedThumbprint = _configuration["ZIRALINK_CERT_THUMBPRINT_LOCALHOST"]!;
            if (certificate!.GetCertHashString() == expectedThumbprint)
                return true;

            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            return false;
        }
    }
}
