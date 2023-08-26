using DotNet.Testcontainers.Builders;

namespace ZiraLink.Client.IntegrationTests.Fixtures
{
    public class InfrastructureFixture
    {
        public InfrastructureFixture()
        {
            InitializeRabbitMq();
            InitializeSampleWebServer();
        }

        private void InitializeRabbitMq()
        {
            var container = new ContainerBuilder()
              .WithImage("bitnami/rabbitmq:latest")
              .WithPortBinding(5872, 5672)
              .WithEnvironment("RABBITMQ_USERNAME", "user")
              .WithEnvironment("RABBITMQ_PASSWORD", "Pass123$")
              .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5672).UntilPortIsAvailable(15672))
              .Build();

            container.StartAsync().Wait();
        }

        private void InitializeSampleWebServer()
        {
            var container = new ContainerBuilder()
              .WithImage("ghcr.io/saeedmaghdam/ziralink.client/samplewebserver:main")
              .WithPortBinding(9080, 80)
              .WithPortBinding(9443, 443)
              .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)))
              .Build();

            container.StartAsync().Wait();
        }
    }
}
