using Testcontainers.MongoDb;
using TaskList.IntegrationTests.Constants;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder(MongoTestConstants.Image).Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
