// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Collections;
using System.Text.Json;

namespace Kagamine.Extensions.Tests.Collections;

public class JsonValueArrayConverterTests
{
    private sealed record Foo(string A, int B);

    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonValueArrayConverter() }
    };

    private static readonly ValueArray<Foo> ExpectedFoos = [
        new Foo("foo", 1),
        new Foo("bar", 2),
    ];

    private static readonly string ExpectedJson = """
        [{"A":"foo","B":1},{"A":"bar","B":2}]
        """;

    [Fact]
    public void Read()
    {
        var actualFoos = JsonSerializer.Deserialize<ValueArray<Foo>>(ExpectedJson, Options);
        Assert.Equal(ExpectedFoos, actualFoos);
    }

    [Fact]
    public void Write() // Converter is not needed for serialization
    {
        var actualJson = JsonSerializer.Serialize(ExpectedFoos, Options);
        Assert.Equal(ExpectedJson.Trim(), actualJson);
    }

    [Fact]
    public void DeserializesNullAsEmptyArray()
    {
        var result = JsonSerializer.Deserialize<ValueArray<string>>("null", Options);
        Assert.Empty(result);
    }
}
