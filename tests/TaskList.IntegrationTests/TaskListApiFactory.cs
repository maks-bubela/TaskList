using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TaskList.Api;
using TaskList.IntegrationTests.Constants;

namespace TaskList.IntegrationTests;

internal sealed class TaskListApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    internal string DatabaseName { get; } = $"{MongoTestConstants.DatabasePrefix}{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MongoTestConstants.ConnectionStringKey] = connectionString,
                [MongoTestConstants.DatabaseNameKey] = DatabaseName
            });
        });
    }
}
