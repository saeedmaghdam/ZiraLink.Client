using System.Net.WebSockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Services;

namespace ZiraLink.Client.UnitTests
{
    public class WebSocketServiceTests
    {
        [Fact]
        public async Task InitializeWebSocketAsync_WebSocketHasInitialized_ShouldReturnIt()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();

            var memoryCache = serviceProvider.GetService<IMemoryCache>();

            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();

            var host = "aghdam.nl";

            var clientWebSocket = new ClientWebSocket();
            var webSocket = new WebsocketAdapter(clientWebSocket);
            memoryCache.Set(host, webSocket);

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, memoryCache!, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, null);

            // Assert
            Assert.Equal(webSocket, result);
        }
    }
}
