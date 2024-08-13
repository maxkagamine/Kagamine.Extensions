// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kagamine.Extensions.Collections;

/// <summary>
/// Provides support for deserializing arrays as <see cref="ValueArray{T}"/>.
/// </summary>
/// <remarks>
/// Cannot be used in conjunction with <see cref="JsonBase64ValueArrayConverter"/>.
/// </remarks>
public class JsonValueArrayConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(ValueArray<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(JsonValueArrayConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;
}

/// <summary>
/// Provides support for deserializing arrays as <see cref="ValueArray{T}"/>.
/// </summary>
/// <typeparam name="T">The array element type.</typeparam>
public class JsonValueArrayConverter<T> : JsonConverter<ValueArray<T>>
{
    public override ValueArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        T[]? array = JsonSerializer.Deserialize<T[]>(ref reader, options);
        return array ?? ValueArray<T>.Empty;
    }

    public override void Write(Utf8JsonWriter writer, ValueArray<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (T[])value, options);
    }
}
