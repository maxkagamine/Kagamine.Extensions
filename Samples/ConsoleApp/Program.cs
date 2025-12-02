// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Hosting;
using Kagamine.Extensions.Http;
using Kagamine.Extensions.IO;
using Kagamine.Extensions.Logging;
using Kagamine.Extensions.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using System.Text.Json;

// Kagamine.Extensions.Hosting.ConsoleApplicationBuilder
var builder = ConsoleApplication.CreateBuilder();

builder.Services.AddSerilog(config => config
    .MinimumLevel.Debug()
    .WriteTo.Console(new ExpressionTemplate(
        "{#if SourceContext is not null}[{SourceContext}] {#end}{@m}\n{@x}", theme: TemplateTheme.Code)));

// Kagamine.Extensions.Utilities.RateLimitingHttpHandler(Factory)
builder.Services.AddHttpClient(Options.DefaultName).AddRateLimiting();

// Kagamine.Extensions.IO.TemporaryFileProvider
builder.Services.AddTemporaryFileProvider();

// Kagamine.Extensions.Hosting.ConsoleApplicationExtensions.Run() (see Kagamine.Extensions.Generator)
//  -> Kagamine.Extensions.Hosting.ConsoleApplication.Run()
builder.Run(async (
    ILogger logger,
    HttpClient httpClient,
    ITemporaryFileProvider tempFileProvider,
    CancellationToken cancellationToken) =>
{
    // Kagamine.Extensions.Utilities.TerminalProgressBar
    using var progress = new TerminalProgressBar();

    int completed = 0;
    int maxRequests = 10;

    var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
    {
        string url = i > 4 ? $"http://example.com/?i={i}" : $"https://httpbin.org/delay/5?i={i}";

        // Kagamine.Extensions.Logging.TimedOperationExtensions
        using (logger.BeginTimedOperation("Request to {Url}", url))
        {
            // Requests rate limited by hostname using Kagamine.Extensions.Utilities.RateLimitingHttpHandler
            //
            // Notice that the example.com requests complete 3s apart from each other, while the httpbin.org requests
            // finish independently, spaced 8s apart (5s delay + 3s rate limit between response and request)
            await httpClient.GetAsync(url, cancellationToken);
        }

        // Kagamine.Extensions.Utilities.TerminalProgressBar
        progress.SetProgress(++completed, maxRequests);
    }));

    await Task.WhenAll(tasks);

    // Kagamine.Extensions.Utilities.TerminalProgressBar
    progress.SetIndeterminate();
    await Task.Delay(1000, cancellationToken);

    // Kagamine.Extensions.IO.TemporaryFileProvider
    using StreamReader reader = new(CreateTempFileStream("foo"));
    logger.Information("Temp file contents: {Json}", reader.ReadToEnd());

    Stream CreateTempFileStream(string data)
    {
        // Kagamine.Extensions.IO.TemporaryFile
        using TemporaryFile tempFile = tempFileProvider.Create(".json");

        logger.Information("Created temp file at {TempFilePath}", tempFile.Path);

        using (var tempFileStream = tempFile.OpenWrite())
        {
            // Temp file will be automatically deleted if this were to throw
            JsonSerializer.Serialize(tempFileStream, new { data });
        }

        return tempFile.OpenRead();
    }
});

