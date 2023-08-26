namespace ZiraLink.Client.IntegrationTests
{
    [Collection("Infrastructure Collection")]
    public class Tests
    {
        [Fact]
        public async Task TestContainer()
        {        
            var httpClient = new HttpClient();

            var result = await httpClient.GetStringAsync("http://localhost:9080/")
              .ConfigureAwait(false);

            Assert.NotNull(result);
        }
    }
}
