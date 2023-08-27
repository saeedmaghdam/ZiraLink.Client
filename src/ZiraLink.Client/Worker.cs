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

        public Worker(ILogger<Worker> logger, ISignalService signalService, IHttpRequestHandlerService httpRequestHandlerService, IWebSocketHandlerService webSocketHandlerService)
        {
            _logger = logger;
            _signalService = signalService;
            _httpRequestHandlerService = httpRequestHandlerService;
            _webSocketHandlerService = webSocketHandlerService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
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

                // Wait for the cancellation token to be triggered
                await Task.Delay(Timeout.Infinite, stoppingToken);
            });

            return Task.CompletedTask;
        }
    }
}
