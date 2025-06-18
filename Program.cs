using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
  // Configure all logs to go to stderr
  consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<NuGetApiService>(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("NUGET_API_KEY");
    return new NuGetApiService(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ILogger<NuGetApiService>>(),
        apiKey);
});

await builder.Build().RunAsync();