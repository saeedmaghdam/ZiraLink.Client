using System.Text.Json;
using ZiraLink.Client.Models;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Framework.Application;

namespace ZiraLink.Client
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ISignalService _signalService;
        private readonly IHttpRequestHandlerService _httpRequestHandlerService;
        private readonly IWebSocketHandlerService _webSocketHandlerService;
        private readonly IClientBusService _clientBusService;
        private readonly IServerBusService _serverBusService;

        public Worker(ILogger<Worker> logger, ISignalService signalService, IHttpRequestHandlerService httpRequestHandlerService, IWebSocketHandlerService webSocketHandlerService, IClientBusService clientBusService, IServerBusService serverBusService)
        {
            _logger = logger;
            _signalService = signalService;
            _httpRequestHandlerService = httpRequestHandlerService;
            _webSocketHandlerService = webSocketHandlerService;
            _clientBusService = clientBusService;
            _serverBusService = serverBusService;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting service ...");

            var _ = Task.Run(async () =>
            {
                var fileName = "profile";
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Test")
                    fileName = "profile.test";

                if (!System.IO.File.Exists(fileName))
                    _signalService.WaitOne();

                var content = System.IO.File.ReadAllText(fileName);
                var profile = JsonSerializer.Deserialize<ProfileViewModel>(content);

                // Set up RabbitMQ connection and channels
                _httpRequestHandlerService.InitializeHttpRequestConsumer(profile!.Username);
                _webSocketHandlerService.InitializeWebSocketConsumer(profile!.Username);
                _clientBusService.InitializeConsumer(profile.Username, cancellationToken);
                _serverBusService.RequestAppProjects(profile.Username);

                // Wait for the cancellation token to be triggered
                await Task.Delay(Timeout.Infinite, cancellationToken);
            });

            return Task.CompletedTask;
        }
    }
}
