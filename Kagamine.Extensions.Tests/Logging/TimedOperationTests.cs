// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Kagamine.Extensions.Tests.Logging;

public class TimedOperationTests
{
    private sealed class Sink(List<LogEvent> logEvents) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => logEvents.Add(logEvent);
    }

    [Fact]
    public void WritesLogEvents()
    {
        List<LogEvent> logEvents = [];

        ILogger logger = new LoggerConfiguration()
            .WriteTo.Sink(new Sink(logEvents))
            .CreateLogger();

        var disposable = logger.BeginTimedOperation("Do a {Thing:l}", "foo");

        Assert.Single(logEvents);
        Assert.Equal("Do a foo: Starting", logEvents[0].RenderMessage());
        Assert.Contains("TimedOperationId", logEvents[0].Properties);

        disposable.Dispose();

        Assert.Equal(2, logEvents.Count);
        Assert.Matches(@"^Do a foo: Completed in [\d\.]+ ms$", logEvents[1].RenderMessage());
        Assert.Equal(
            logEvents[0].Properties["TimedOperationId"].ToString(),
            logEvents[1].Properties["TimedOperationId"].ToString());
    }
}
