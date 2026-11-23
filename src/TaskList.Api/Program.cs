using MongoDB.Driver;
using TaskList.Api.Constants;
using TaskList.Api.Middleware;
using TaskList.Application.Interfaces;
using TaskList.Application.Services;
using TaskList.Infrastructure.MongoDB;

namespace TaskList.Api;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSwaggerGen();
        }

        builder.Services.AddScoped<ITaskListRepository, MongoTaskListRepository>();
        builder.Services.AddScoped<TaskListService>();

        var mongoDb = builder.Configuration.GetRequiredSection(MongoConfigurationConstants.Section);
        builder.Services.AddSingleton<IMongoClient>(_ =>
            new MongoClient(mongoDb[MongoConfigurationConstants.ConnectionString]
                ?? throw new InvalidOperationException(ExceptionMessageConstants.MissingMongoConnectionString)));
        builder.Services.AddSingleton(services =>
            MongoTaskLists.GetCollection(
                services.GetRequiredService<IMongoClient>(),
                mongoDb[MongoConfigurationConstants.DatabaseName]
                    ?? throw new InvalidOperationException(ExceptionMessageConstants.MissingMongoDatabaseName)));

        var app = builder.Build();
        await MongoTaskLists.EnsureIndexesAsync(
            app.Services.GetRequiredService<IMongoCollection<TaskListDocument>>(),
            app.Lifetime.ApplicationStopping);

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        await app.RunAsync();
    }
}
