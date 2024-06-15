// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Hosting;
using Kagamine.Extensions.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

var builder = ConsoleApplication.CreateBuilder();

builder.Services.AddSerilog(config => config
    .MinimumLevel.Debug()
    //.MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console());

builder.Services.AddSingleton<RateLimitingHttpHandler>();
builder.Services.AddHttpClient(Options.DefaultName).AddHttpMessageHandler<RateLimitingHttpHandler>();

builder.Run(async (ILogger logger, HttpClient httpClient, CancellationToken cancellationToken) =>
{
    using var progress = new TerminalProgressBar();

    int completed = 0;
    int maxRequests = 10;

    var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
    {
        string url = i > 4 ? $"http://example.com/?i={i}" : $"https://httpbin.org/delay/5?i={i}";

        await httpClient.GetAsync(url, cancellationToken);

        logger.Information("Request to {Url} completed.", url);
        progress.SetProgress(++completed, maxRequests);
    }));

    await Task.WhenAll(tasks);
});
