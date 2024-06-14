// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Kagamine.Extensions.Hosting;

public sealed class ConsoleApplicationBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder builder;

    internal ConsoleApplicationBuilder() : this(args: null)
    { }

    internal ConsoleApplicationBuilder(string[]? args) : this(new HostApplicationBuilderSettings() { Args = args })
    { }

    internal ConsoleApplicationBuilder(HostApplicationBuilderSettings? settings)
    {
        builder = Host.CreateApplicationBuilder(settings);

        // Replace the default ConsoleLifetime with one that sets appropriate exit codes
        Services.RemoveAll<IHostLifetime>();
        Services.AddSingleton<IHostLifetime, ConsoleLifetime>();

        // Make sure Japanese text, special chars, etc. don't get mangled on Windows if "Use Unicode UTF-8 for worldwide
        // language support" in the Region control panel is not checked (no-op on Linux)
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
    }

    public ConsoleApplication Build() => new(builder.Build());

    #region IHostApplicationBuilder
    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)builder).Properties;

    public IConfigurationManager Configuration => ((IHostApplicationBuilder)builder).Configuration;

    public IHostEnvironment Environment => builder.Environment;

    public ILoggingBuilder Logging => builder.Logging;

    public IMetricsBuilder Metrics => builder.Metrics;

    public IServiceCollection Services => builder.Services;

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull
    {
        ((IHostApplicationBuilder)builder).ConfigureContainer(factory, configure);
    }
    #endregion
}
