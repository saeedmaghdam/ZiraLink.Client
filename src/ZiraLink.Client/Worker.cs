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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var _ = Task.Run(async () =>
            {
                if (!System.IO.File.Exists("profile"))
                    _signalService.WaitOne();

                var content = System.IO.File.ReadAllText("profile");
                var profile = JsonSerializer.Deserialize<ProfileViewModel>(content);

                // Set up RabbitMQ connection and channels
                await _httpRequestHandlerService.InitializeHttpRequestConsumerAsync(profile!.Username);
                await _webSocketHandlerService.InitializeWebSocketConsumerAsync(profile!.Username);

                // Wait for the cancellation token to be triggered
                await Task.Delay(Timeout.Infinite, stoppingToken);
            });
        }
    }
}
