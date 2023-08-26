using System.Diagnostics.CodeAnalysis;

namespace ZiraLink.Client.IntegrationTests.Fixtures
{
    [ExcludeFromCodeCoverage]
    [CollectionDefinition("Infrastructure Collection")]
    public class InfrastructureCollection : ICollectionFixture<InfrastructureFixture> { }
}
