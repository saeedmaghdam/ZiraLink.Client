using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Client.IntegrationTests.Fixtures;
using ZiraLink.Client.Models;

namespace ZiraLink.Client.IntegrationTests
{
    [ExcludeFromCodeCoverage]
    [Collection("Infrastructure Collection")]
    public class ClientTests
    {
        private InfrastructureFixture _fixture;

        public ClientTests(InfrastructureFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task TestClientFunctionality()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await using var application = new WebApplicationFactory<ProgramMock>();
            using var client = application.CreateClient();

            var requestUrl = new Uri("https://sample-web-server.app.ziralink.local:7001/");
            var internalUrl = new Uri("https://localhost:9443");
            var requestExchange = "request";
            var requestQueue = "logon_request_bus";
            var responseExchange = "response";
            var responseQueue = "response_bus";

            var channel = _fixture.CreateChannel();

            channel.ExchangeDeclare(requestExchange, "direct", false, false, null);
            channel.QueueDeclare(requestQueue, false, false, false, null);
            channel.QueueBind(requestQueue, requestExchange, requestQueue, null);

            channel.ExchangeDeclare(responseExchange, "direct", false, false, null);
            channel.QueueDeclare(responseQueue, false, false, false, null);
            channel.QueueBind(responseQueue, responseExchange, "", null);

            // Act
            var properties = channel.CreateBasicProperties();
            properties.MessageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", internalUrl.ToString());
            headers.Add("Host", requestUrl.Authority);
            properties.Headers = headers;

            var requestModel = new HttpRequestModel
            {
                RequestUrl = requestUrl.ToString(),
                Method = "GET",
                Headers = new Dictionary<string, IEnumerable<string>>(),
                Bytes = null
            };
            channel.BasicPublish(requestExchange, requestQueue, properties, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestModel)));

            var response = default(string);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                response = Encoding.UTF8.GetString(body);
            };
            channel.BasicConsume(queue: responseQueue,
                                 autoAck: true,
                                 consumer: consumer);

            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert
            var responseModel = JsonSerializer.Deserialize<HttpResponseModel>(response);
            Assert.True(responseModel.IsSuccessStatusCode);

            var stringContent = System.Text.Encoding.UTF8.GetString(responseModel.Bytes, 0, responseModel.Bytes.Length);
            var weatherForecast = JsonSerializer.Deserialize<WeatherForecast[]>(stringContent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            Assert.NotNull(weatherForecast);
            Assert.Equal(5, weatherForecast!.Length);
        }
    }
}
