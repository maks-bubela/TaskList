using System.Net;
using System.Net.Http.Json;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TaskList.Api.Middleware;
using Xunit;

namespace TaskList.IntegrationTests;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnexpectedExceptionReturnsSafeProblemDetails()
    {
        const string internalMessage = "Internal failure details";
        var failure = new InvalidOperationException(internalMessage);
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services => services.AddControllers())
            .Configure(app =>
            {
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.Run(_ => throw failure);
            }));
        using var client = server.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(MediaTypeNames.Application.ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = Assert.IsType<ProblemDetails>(await response.Content.ReadFromJsonAsync<ProblemDetails>());
        Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
        Assert.Equal("An unexpected error occurred.", problem.Detail);
        Assert.NotNull(problem.Type);
        Assert.NotNull(problem.Title);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(internalMessage, body);
        Assert.DoesNotContain("stackTrace", body);
    }
}
