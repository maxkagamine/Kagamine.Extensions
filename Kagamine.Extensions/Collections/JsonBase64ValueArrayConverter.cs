// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kagamine.Extensions.Collections;

/// <summary>
/// Serializes all <see cref="ValueArray{T}"/> as base 64 strings. Array cannot contain reference types.
/// </summary>
public class JsonBase64ValueArrayConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ValueArray<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(JsonBase64ValueArrayConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
}

/// <summary>
/// Serializes a <see cref="ValueArray{T}"/> as a base 64 string. 
/// </summary>
/// <typeparam name="T">The array element type. Cannot contain reference types.</typeparam>
public class JsonBase64ValueArrayConverter<T> : JsonConverter<ValueArray<T>>
    where T : unmanaged
{
    private const int MaxStackByteSize = 1024;
    private static readonly string ErrorMessage = $"Cannot convert value to {typeof(T).Name}[]";

    public override ValueArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Utf8JsonReader contains a GetBytesFromBase64(), but it returns byte[] instead of Span<byte>, which would
        // require copying the array in order to get it into a T[]. To avoid unnecessary allocations, we'll use
        // System.Buffers.Text.Base64 directly, which operates on spans of UTF-8 bytes, and marshal it as Span<T>.

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"{ErrorMessage}: token type is {reader.TokenType}.");
        }

        ReadOnlySpan<byte> utf8 = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;

        if (reader.ValueIsEscaped) // Not likely, but we should handle it
        {
            Span<byte> unescaped = new byte[utf8.Length];
            utf8 = unescaped[..reader.CopyString(unescaped)];
        }

        int bufferSize = utf8.Length / 4 * 3;
        byte[]? pooledArray = null;
        Span<byte> buffer = bufferSize <= MaxStackByteSize ?
            stackalloc byte[bufferSize] : (pooledArray = ArrayPool<byte>.Shared.Rent(bufferSize));

        try
        {
            if (Base64.DecodeFromUtf8(utf8, buffer, out _, out int bytesWritten) != OperationStatus.Done)
            {
                throw new JsonException($"{ErrorMessage}: not a valid base 64 string.");
            }

            return ValueArray.FromBytes<T>(buffer[..bytesWritten]);
        }
        finally
        {
            if (pooledArray is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledArray);
            }
        }
    }

    public override void Write(Utf8JsonWriter writer, ValueArray<T> value, JsonSerializerOptions options)
    {
        writer.WriteBase64StringValue(value.AsBytes());
    }
}
