using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZiraLink.Client.Models;

namespace ZiraLink.Client.IntegrationTests
{
    [ExcludeFromCodeCoverage]
    [Collection("Infrastructure Collection")]
    public class ClientTests
    {
        [Fact]
        public async Task TestClientFunctionality()
        {
            // Arrange
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            await using var application = new WebApplicationFactory<ProgramMock>();
            using var client = application.CreateClient();

            var factory = new ConnectionFactory { HostName = "localhost", Port = 5872, UserName = "user", Password = "Pass123$" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ExchangeDeclare("request", "direct", false, false, null);
            channel.QueueDeclare("logon_request_bus", false, false, false, null);
            channel.QueueBind("logon_request_bus", "request", "logon_request_bus", null);

            channel.ExchangeDeclare("response", "direct", false, false, null);
            channel.QueueDeclare("response_bus", false, false, false, null);
            channel.QueueBind("response_bus", "response", "", null);

            // Act
            var properties = channel.CreateBasicProperties();
            properties.MessageId = Guid.NewGuid().ToString();
            var headers = new Dictionary<string, object>();
            headers.Add("IntUrl", "https://localhost:9443");
            headers.Add("Host", "sample-web-server.app.ziralink.local:7001");
            properties.Headers = headers;

            var message = "{\"RequestUrl\":\"https://sample-web-server.app.ziralink.local:7001/\",\"Method\":\"GET\",\"Headers\":[],\"Bytes\":null}";
            channel.BasicPublish("request", "logon_request_bus", properties, Encoding.UTF8.GetBytes(message));

            var response = default(string);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                response = Encoding.UTF8.GetString(body);
            };
            channel.BasicConsume(queue: "response_bus",
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
