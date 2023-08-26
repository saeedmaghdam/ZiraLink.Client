using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using ZiraLink.Client.IntegrationTests.Fixtures;

namespace ZiraLink.Client.IntegrationTests
{
    [ExcludeFromCodeCoverage]
    [Collection("Infrastructure Collection")]
    public class Tests
    {
        private readonly InfrastructureFixture _fixture;

        public Tests(InfrastructureFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task TestContainer()
        {
            using var httpClient = _fixture.CreateHttpClient();
            var response = await httpClient.GetStringAsync("/").ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<WeatherForecast[]>(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            Assert.NotNull(result);
            Assert.Equal(5, result!.Length);
        }
    }

    internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
