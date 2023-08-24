using ZiraLink.Client.Services;

namespace ZiraLink.Client.UnitTests
{
    public class WebSocketFactoryTests
    {
        [Fact]
        public void CreateClientWebSocket_ShouldCreateAClientThatBypassHttpsValidation()
        {
            // Arrange
            var webSocketFactory = new WebSocketFactory();

            // Act
            var clientWebSocket = webSocketFactory.CreateClientWebSocket();
            var bypassCertificateValidation = clientWebSocket.Options.RemoteCertificateValidationCallback.Invoke(null, null, null, System.Net.Security.SslPolicyErrors.None);

            // Assert
            Assert.True(bypassCertificateValidation);
        }
    }
}
