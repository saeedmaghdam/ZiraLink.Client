﻿using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
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
            var cacheMock = new Mock<ICache>();
            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();
            var webSocketMock = new Mock<IWebSocket>();

            var host = "aghdam.nl";

            cacheMock.Setup(m => m.TryGetWebSocket(host, out It.Ref<IWebSocket>.IsAny)).Returns(true);

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, cacheMock.Object, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, null);

            // Assert
            cacheMock.Verify(m => m.TryGetWebSocket(host, out It.Ref<IWebSocket>.IsAny), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task InitializeWebSocketAsync_InternalAddressIsHttp_ShouldInitializeWsConnection()
        {
            // Arrange
            var cacheMock = new Mock<ICache>();
            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();
            var webSocketMock = new Mock<IWebSocket>();

            var host = "aghdam.nl";
            var internalUri = new Uri("http://localhost:5000/");

            webSocketFactoryMock.Setup(m => m.CreateClientWebSocket()).Returns(webSocketMock.Object);
            var webSocketReceiveResult = new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>())).Callback(() => Thread.Sleep(TimeSpan.FromDays(1))).ReturnsAsync(webSocketReceiveResult);
            
            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, cacheMock.Object, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, internalUri);

            // Assert
            Assert.Equal(webSocketMock.Object, result);
            webSocketMock.Verify(m => m.ConnectAsync(new Uri("ws://localhost:5000/"), It.IsAny<CancellationToken>()), Times.Once);
            cacheMock.Verify(m => m.TryGetWebSocket(host, out It.Ref<IWebSocket>.IsAny), Times.Once);
            cacheMock.Verify(m => m.SetWebSocket(host, It.IsAny<IWebSocket>()), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task InitializeWebSocketAsync_InternalAddressIsHttps_ShouldInitializeWssConnection()
        {
            // Arrange
            var cacheMock = new Mock<ICache>();
            var webSocketFactoryMock = new Mock<IWebSocketFactory>();
            var channelMock = new Mock<IModel>();
            var webSocketMock = new Mock<IWebSocket>();

            var host = "aghdam.nl";
            var internalUri = new Uri("https://localhost:5000/");

            webSocketFactoryMock.Setup(m => m.CreateClientWebSocket()).Returns(webSocketMock.Object);
            var webSocketReceiveResult = new WebSocketReceiveResult(0, WebSocketMessageType.Text, false);
            webSocketMock.Setup(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>())).Callback(() => Thread.Sleep(TimeSpan.FromDays(1))).ReturnsAsync(webSocketReceiveResult);

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, cacheMock.Object, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, internalUri);

            // Assert
            Assert.Equal(webSocketMock.Object, result);
            webSocketMock.Verify(m => m.ConnectAsync(new Uri("wss://localhost:5000/"), It.IsAny<CancellationToken>()), Times.Once);
            cacheMock.Verify(m => m.TryGetWebSocket(host, out It.Ref<IWebSocket>.IsAny), Times.Once);
            cacheMock.Verify(m => m.SetWebSocket(host, It.IsAny<IWebSocket>()), Times.Once);
            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task InitializeWebSocketAsync_ReceiveMessageFromWebSocket_ShouldPublishUsingRabbitMq()
        {
            // Arrange
            var cacheMock = new Mock<ICache>();
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

            var webSocketService = new WebSocketService(webSocketFactoryMock.Object, cacheMock.Object, channelMock.Object);

            // Act
            var result = await webSocketService.InitializeWebSocketAsync(host, internalUri);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert
            Assert.Equal(webSocketMock.Object, result);
            webSocketMock.Verify(m => m.ConnectAsync(new Uri("wss://localhost:5000/"), It.IsAny<CancellationToken>()), Times.Once);
            webSocketMock.Verify(m => m.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            cacheMock.Verify(m => m.TryGetWebSocket(host, out It.Ref<IWebSocket>.IsAny), Times.Once);
            cacheMock.Verify(m => m.SetWebSocket(host, It.IsAny<IWebSocket>()), Times.Once);
            cacheMock.Verify(m => m.RemoveWebSocket(host), Times.Once);
            cacheMock.VerifyNoOtherCalls();
            channelMock.Verify(m => m.BasicPublish(exchangeName, queueName, false, basicPropertiesMock.Object, It.IsAny<ReadOnlyMemory<byte>>()), Times.Once);
        }
    }
}
