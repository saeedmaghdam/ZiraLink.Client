using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Helpers;
using ZiraLink.Client.Framework.Services;
using ZiraLink.Client.Services;

namespace ZiraLink.Client.UnitTests
{
    public class WebSocketHandlerServiceTests
    {
        [Fact]
        public void InitializeWebSocketConsumer_ShouldInitializeQueues()
        {
            // Arrange
            var username = "logon";
            var loggerMock = new Mock<ILogger<WebSocketHandlerService>>();
            var channelMock = new Mock<IModel>();
            var webSocketService = new Mock<IWebSocketService>();

            var httpRequestHandlerService = new WebSocketHandlerService(loggerMock.Object, channelMock.Object, webSocketService.Object);

            // Act
            httpRequestHandlerService.InitializeWebSocketConsumer(username);

            // Assert
            var serverBusQueueName = $"{username}_websocket_server_bus";
            var queueName = "websocket_client_bus";
            var exchangeName = "websocket_bus";

            channelMock.Verify(m => m.QueueDeclare(serverBusQueueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.ExchangeDeclare(exchangeName, "direct", false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(queueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueBind(queueName, exchangeName, queueName, null), Times.Once);
            channelMock.Verify(m => m.BasicConsume(serverBusQueueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
