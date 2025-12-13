// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Kagamine.Extensions.Collections;
using System.Runtime.CompilerServices;

namespace Kagamine.Extensions.Tests.Collections;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Tests")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "Bug in IDE analyzer")]
public class ValueArrayTests
{
    private sealed record Person(string Surname, string GivenName);
    private readonly record struct Numbers(int A, int B);

    [Fact]
    public void ConvertsToFromArray()
    {
        ValueArray<int> foos = new[] { 1, 2, 3 };
        int[] bars = (int[])foos;

        Assert.Equal([1, 2, 3], bars);
    }

    [Fact]
    public void ConvertsToFromSpan()
    {
        ValueArray<int> foos = [1, 2, 3]; // Compiler uses a span and calls ValueArray.Create<T>(ReadOnlySpan<T>)
        ReadOnlySpan<int> bars = foos;

        Assert.Equal([1, 2, 3], bars);
    }

    [Fact]
    public void ConvertsToFromBytes()
    {
        ValueArray<Numbers> foos = [new(3, 9), new(8, 31), new(12, 27)]; // The numbers, Mason!
        ReadOnlySpan<byte> bytes = foos.AsBytes();
        ValueArray<Numbers> bars = ValueArray.FromBytes<Numbers>(bytes);

        Assert.Equal(new byte[] { 3, 0, 0, 0, 9, 0, 0, 0, 8, 0, 0, 0, 31, 0, 0, 0, 12, 0, 0, 0, 27, 0, 0, 0 }, bytes);
        Assert.Equal((IEnumerable<Numbers>)foos, bars);
    }

    [Fact]
    public void ValueTypeEquality()
    {
        ValueArray<Person> a = [new("Kagamine", "Rin"), new("Hatsune", "Miku")];
        ValueArray<Person> b = [new("Kagamine", "Rin"), new("Hatsune", "Miku")];
        ValueArray<Person> c = [new("Kagamine", "Rin"), new("Megurine", "Luka")];
        ValueArray<Person> d = [new("Megurine", "Luka"), new("Kagamine", "Rin")];

        Assert.NotSame(a[0], b[0]);
        Assert.True(a == b);
        Assert.True(b != c);
        Assert.True(c != d);
    }

    [Fact]
    public void HashCodeBasedOnElements()
    {
        ValueArray<Person> a = [new("Kagamine", "Rin"), new("Hatsune", "Miku")];
        ValueArray<Person> b = [new("Kagamine", "Rin"), new("Hatsune", "Miku")];

        Assert.NotEqual(RuntimeHelpers.GetHashCode(a), RuntimeHelpers.GetHashCode(b)); // Object.GetHashCode()
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void StructuralComparison()
    {
        ValueArray<int> foos = [10, 20, 999];
        ValueArray<int> bars = [10, 100, 0];

        Assert.True(bars > foos);
        Assert.True(foos < bars);
    }
}
