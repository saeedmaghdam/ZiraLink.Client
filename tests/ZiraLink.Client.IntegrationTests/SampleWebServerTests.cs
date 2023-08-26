using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ZiraLink.Client.IntegrationTests.Fixtures;

namespace ZiraLink.Client.IntegrationTests
{
    [ExcludeFromCodeCoverage]
    [Collection("Infrastructure Collection")]
    public class SampleWebServerTests
    {
        private readonly InfrastructureFixture _fixture;

        public SampleWebServerTests(InfrastructureFixture fixture2) => _fixture = fixture2;

        [Fact]
        public async Task SendGetRequestToSampleWebServerShouldReturnWeatherForecasts()
        {
            // Arrange
            using var httpClient = _fixture.CreateHttpClient();

            // Act
            var response = await httpClient.GetStringAsync("/").ConfigureAwait(false);

            // Assert
            var result = JsonSerializer.Deserialize<WeatherForecast[]>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            Assert.NotNull(result);
            Assert.Equal(5, result!.Length);
        }

        [Fact]
        public async Task SampleWebServerShouldBeAbleToAnswerWebSocketRequests()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            using var webSocketClient = await _fixture.CreateWebSocketClientAsync();

            // Act & Assert
            for(int i = 0; i < 1000; i++)
            {
                await SendMessageToWebSocket(webSocketClient, "ZiraLink", cancellationTokenSource.Token);
                var response = await ReceiveMessageFromWebSocket(webSocketClient, cancellationTokenSource.Token);

                Assert.Equal($"{i + 1}: ZiraLink", response);
            }
        }

        private static async Task SendMessageToWebSocket(ClientWebSocket ws, string data, CancellationToken cancellationToken)
        {
            var encoded = Encoding.UTF8.GetBytes(data);
            var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            await ws.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
        }

        private static async Task<string> ReceiveMessageFromWebSocket(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new Byte[8192]);
            var result = default(WebSocketReceiveResult);

            using (var ms = new MemoryStream())
            {
                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken);
                    ms.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                ms.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(ms, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }
    }

    internal record WeatherForecast(DateTime Date, int TemperatureC, int TemperatureF, string? Summary);
}
