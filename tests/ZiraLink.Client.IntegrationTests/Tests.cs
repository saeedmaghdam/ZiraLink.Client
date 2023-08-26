using System.Text.Json;

namespace ZiraLink.Client.IntegrationTests
{
    [Collection("Infrastructure Collection")]
    public class Tests
    {
        [Fact]
        public async Task TestContainer()
        {        
            var httpClient = new HttpClient();

            var response = await httpClient.GetStringAsync("http://localhost:9080/")
              .ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<WeatherForecast[]>(response);

            Assert.NotNull(result);
            Assert.Equal(5, result!.Length);
        }
    }

    internal record WeatherForecast(DateTime Date, int TemperatureC, string? Summary)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
