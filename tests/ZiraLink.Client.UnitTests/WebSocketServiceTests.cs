using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Services;

namespace ZiraLink.Client.UnitTests
{
    [ExcludeFromCodeCoverage]
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
            var webSocketMock = new Mock<IWebSocket>();

            var host = "aghdam.nl";

            memoryCache.Set(host, webSocketMock.Object);

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, memoryCache!, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, null);

            // Assert
            Assert.Equal(webSocketMock.Object, result);
        }

        [Fact]
        public async Task InitializeWebSocketAsync_InternalAddressIsHttp_ShouldInitializeWsConnection()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();

            var memoryCache = serviceProvider.GetService<IMemoryCache>();

            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();
            var webSocketMock = new Mock<IWebSocket>();

            var host = "aghdam.nl";
            var internalUri = new Uri("http://localhost:5000/");

            webSocketFactoryMock.Setup(m => m.CreateClientWebSocket()).Returns(webSocketMock.Object);
            var webSocketReceiveResult = new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>())).Callback(() => Thread.Sleep(TimeSpan.FromDays(1))).ReturnsAsync(webSocketReceiveResult);
            
            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, memoryCache!, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, internalUri);

            // Assert
            Assert.Equal(webSocketMock.Object, result);
            webSocketMock.Verify(m => m.ConnectAsync(new Uri("ws://localhost:5000/"), It.IsAny<CancellationToken>()), Times.Once);

            var cacheWebSocket = memoryCache.Get(host);
            Assert.Equal(webSocketMock.Object, cacheWebSocket);

            var cacheTask = memoryCache.Get($"task_{host}");
            Assert.NotNull(cacheTask);
        }

        [Fact]
        public async Task InitializeWebSocketAsync_InternalAddressIsHttps_ShouldInitializeWssConnection()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();

            var memoryCache = serviceProvider.GetService<IMemoryCache>();

            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();
            var webSocketMock = new Mock<IWebSocket>();

            var host = "aghdam.nl";
            var internalUri = new Uri("https://localhost:5000/");

            webSocketFactoryMock.Setup(m => m.CreateClientWebSocket()).Returns(webSocketMock.Object);
            var webSocketReceiveResult = new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>())).Callback(() => Thread.Sleep(TimeSpan.FromDays(1))).ReturnsAsync(webSocketReceiveResult);

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, memoryCache!, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, internalUri);

            // Assert
            Assert.Equal(webSocketMock.Object, result);
            webSocketMock.Verify(m => m.ConnectAsync(new Uri("wss://localhost:5000/"), It.IsAny<CancellationToken>()), Times.Once);

            var cacheWebSocket = memoryCache.Get(host);
            Assert.Equal(webSocketMock.Object, cacheWebSocket);

            var cacheTask = memoryCache.Get($"task_{host}");
            Assert.NotNull(cacheTask);
        }

        [Fact]
        public async Task InitializeWebSocketAsync_ReceiveMessageFromWebSocket_ShouldPublishUsingRabbitMq()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();

            var memoryCache = serviceProvider.GetService<IMemoryCache>();

            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();
            var webSocketMock = new Mock<IWebSocket>();
            var basicPropertiesMock = new Mock<IBasicProperties>();

            var host = "aghdam.nl";
            var internalUri = new Uri("https://localhost:5000/");
            var queueName = "websocket_client_bus";
            var exchangeName = "websocket_bus";

            webSocketFactoryMock.Setup(m => m.CreateClientWebSocket()).Returns(webSocketMock.Object);
            channelMock.Setup(m => m.CreateBasicProperties()).Returns(basicPropertiesMock.Object);

            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", "");
            headers.Add("Host", host);
            basicPropertiesMock.SetupGet(m => m.Headers).Returns(headers);

            var messagesCount = 0;
            Func<WebSocketReceiveResult> getWebSocketReceiveResult = () =>
            {
                if (++messagesCount == 2)
                    return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
                else
                    return new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            };
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>())).ReturnsAsync(getWebSocketReceiveResult);

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, memoryCache!, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, internalUri);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            Assert.Equal(webSocketMock.Object, result);
            webSocketMock.Verify(m => m.ConnectAsync(new Uri("wss://localhost:5000/"), It.IsAny<CancellationToken>()), Times.Once);
            webSocketMock.Verify(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            var cacheWebSocket = memoryCache.Get(host);
            Assert.Null(cacheWebSocket);

            var cacheTask = memoryCache.Get($"task_{host}");
            Assert.Null(cacheTask);

            channelMock.Verify(m => m.BasicPublish(exchangeName, queueName, false, basicPropertiesMock.Object, It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
        }
    }
}
