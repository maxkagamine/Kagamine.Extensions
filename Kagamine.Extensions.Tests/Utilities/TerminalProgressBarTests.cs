// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Utilities;
using System.Text;

namespace Kagamine.Extensions.Tests.Utilities;

public class TerminalProgressBarTests
{
    private readonly StringBuilder stdout = new();
    private readonly TerminalProgressBar progress;

    public TerminalProgressBarTests()
    {
        progress = new TerminalProgressBar(new StringWriter(stdout));
    }

    [Fact]
    public void SendsCorrectEscapeSequences()
    {
        Assert.Equal("\x1b]9;4;1;0\x07", stdout.ToString());

        stdout.Clear();
        progress.SetProgress(390, 1000);

        Assert.Equal("\x1b]9;4;1;39\x07", stdout.ToString());

        stdout.Clear();
        progress.Dispose();

        Assert.Equal("\x1b]9;4;0;0\x07", stdout.ToString());
    }
}
