using System.Text.Json;
using ZiraLink.Client.Models;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Framework.Application;

namespace ZiraLink.Client
{
    public class Worker : BackgroundService
    {
        private readonly ISignalService _signalService;
        private readonly IHttpRequestHandlerService _httpRequestHandlerService;
        private readonly IWebSocketHandlerService _webSocketHandlerService;

        public Worker(ISignalService signalService, IHttpRequestHandlerService httpRequestHandlerService, IWebSocketHandlerService webSocketHandlerService)
        {
            _signalService = signalService;
            _httpRequestHandlerService = httpRequestHandlerService;
            _webSocketHandlerService = webSocketHandlerService;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var _ = Task.Run(async () =>
            {
                if (!System.IO.File.Exists("profile"))
                    _signalService.WaitOne();

                var content = System.IO.File.ReadAllText("profile");
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
