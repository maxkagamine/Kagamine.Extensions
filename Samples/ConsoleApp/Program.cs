// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Hosting;
using Kagamine.Extensions.Utilities;
using Serilog;
using Serilog.Events;

var app = ConsoleApplication.CreateBuilder();

app.Services.AddSerilog(config => config
    .MinimumLevel.Debug()
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console());

app.Run(async (ILogger logger, CancellationToken cancellationToken) =>
{
    using var progress = new TerminalProgressBar();

    for (int i = 1; i <= 10; i++)
    {
        logger.Information("Doing thing {Thing}", i);
        progress.SetProgress(i, 10);
        await Task.Delay(500, cancellationToken);
    }
});
