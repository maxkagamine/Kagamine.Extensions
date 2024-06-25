// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kagamine.Extensions.Collections;

public static class ValueArray
{
    /// <summary>
    /// Creates a <see cref="ValueArray{T}"/> with the specified elements.
    /// </summary>
    /// <remarks>
    /// This method is used by the compiler for collection expressions.
    /// </remarks>
    /// <typeparam name="T">The array element type.</typeparam>
    /// <param name="items">The elements to store in the array.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0301:Simplify collection initialization", Justification = "Changing ValueArray<T>.Empty to [] would result in the method calling itself in a loop. IDE is being silly.")]
    public static ValueArray<T> Create<T>(params ReadOnlySpan<T> items) => items.IsEmpty ? ValueArray<T>.Empty : new(items.ToArray());

    /// <summary>
    /// Marshals a read-only span of <see langword="byte"/> as type <typeparamref name="T"/> and copies its contents
    /// into a <see cref="ValueArray{T}"/>.
    /// </summary>
    /// <typeparam name="T">The array element type. Cannot contain reference types.</typeparam>
    /// <param name="bytes">The byte span to reinterpret as an array of <typeparamref name="T"/>. Its length must be a
    /// multiple of <see langword="sizeof"/>(<typeparamref name="T"/>).</param>
    /// <exception cref="ArgumentException"/>
    public static ValueArray<T> FromBytes<T>(ReadOnlySpan<byte> bytes)
        where T : unmanaged
    {
        if (bytes.Length % Unsafe.SizeOf<T>() != 0)
        {
            throw new ArgumentException($"Cannot convert bytes to {typeof(T).Name}[]: invalid length.", nameof(bytes));
        }

        return new ValueArray<T>(MemoryMarshal.Cast<byte, T>(bytes).ToArray());
    }

    /// <summary>
    /// Creates a span over the array and marshals it as a read-only span of <see langword="byte"/>.
    /// </summary>
    /// <remarks>
    /// A byte array can be converted back into <see cref="ValueArray{T}"/> by calling <see
    /// cref="FromBytes{T}(ReadOnlySpan{byte})"/>.
    /// </remarks>
    /// <typeparam name="T">The array element type. Cannot contain reference types.</typeparam>
    /// <param name="array">The array to marshal as bytes.</param>
    /// <exception cref="ArgumentException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsBytes<T>(this ValueArray<T> array)
        where T : unmanaged
    {
        return MemoryMarshal.AsBytes(array.AsSpan());
    }
}
