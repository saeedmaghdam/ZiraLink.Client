using DotNet.Testcontainers.Builders;

namespace ZiraLink.Client.IntegrationTests
{
    public class Tests
    {
        [Fact]
        [Trait("type", "integration")]
        public async Task TestContainer()
        {
            // Create a new instance of a container.
            var container = new ContainerBuilder()
              // Set the image for the container to "testcontainers/helloworld:1.1.0".
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/samplewebserver:main")
              // Bind port 8080 of the container to a random port on the host.
              .WithPortBinding(9080, 80)
              .WithPortBinding(9443, 443)
              // Wait until the HTTP endpoint of the container is available.
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)))
              // Build the container configuration.
              .Build();

            // Start the container.
            await container.StartAsync()
              .ConfigureAwait(false);

            // Create a new instance of HttpClient to send HTTP requests.
            var httpClient = new HttpClient();

            // Send an HTTP GET request to the specified URI and retrieve the response as a string.
            var result = await httpClient.GetStringAsync("http://localhost:9080/")
              .ConfigureAwait(false);

            Assert.NotNull(result);
        }
    }
}
