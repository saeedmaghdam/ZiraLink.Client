using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using ZiraLink.Client.Framework.Helpers;
using ZiraLink.Client.Services;

namespace ZiraLink.Client.UnitTests
{
    [ExcludeFromCodeCoverage]
    public class HttpRequestHandlerServiceTests
    {
        [Fact]
        public void InitializeHttpRequestConsumer_ShouldInitializeQueues()
        {
            // Arrange
            var username = "logon";
            var loggerMock = new Mock<ILogger<HttpRequestHandlerService>>();
            var channelMock = new Mock<IModel>();
            var httpHelperMock = new Mock<IHttpHelper>();

            var httpRequestHandlerService = new HttpRequestHandlerService(loggerMock.Object, channelMock.Object, httpHelperMock.Object);

            // Act
            httpRequestHandlerService.InitializeHttpRequestConsumer(username);

            // Assert
            var responseExchangeName = "response";
            var responseQueueName = "response_bus";
            var requestQueueName = $"{username}_request_bus";

            channelMock.Verify(m => m.ExchangeDeclare(responseExchangeName, "direct", false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(responseQueueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.QueueBind(responseQueueName, responseExchangeName, "", null), Times.Once);
            channelMock.Verify(m => m.QueueDeclare(requestQueueName, false, false, false, null), Times.Once);
            channelMock.Verify(m => m.BasicConsume(requestQueueName, false, "", false, false, null, It.IsAny<IBasicConsumer>()), Times.Once);
        }
    }
}
