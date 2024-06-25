// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Kagamine.Extensions.Collections;

/// <summary>
/// Represents a read-only array with value type semantics suitable for use in immutable records.
/// </summary>
/// <remarks>
/// An array can be implicity cast to <see cref="ValueArray{T}"/>, which can be implicitly cast to <see
/// cref="ReadOnlySpan{T}"/>. For use with APIs that do not support spans, the underlying array may be retrieved via an
/// explicit cast to <typeparamref name="T"/>[]. For performance, this type does not create a defensive copy.
/// </remarks>
/// <typeparam name="T">The array element type.</typeparam>
[DebuggerDisplay("Length = {Length}")]
[DebuggerTypeProxy(typeof(ValueArrayDebugView<>))]
[CollectionBuilder(typeof(ValueArray), nameof(ValueArray.Create))]
public readonly struct ValueArray<T> : IReadOnlyList<T>, IEquatable<ValueArray<T>>, IStructuralEquatable, IComparable<ValueArray<T>>, IStructuralComparable
{
    public static readonly ValueArray<T> Empty = default;

    readonly T[]? array;

    public ValueArray(T[] array)
    {
        this.array = array;
    }

    private T[] Array => array ?? [];

    public int Length => Array.Length;

    /// <summary>
    /// Creates a new span over the array.
    /// </summary>
    public ReadOnlySpan<T> AsSpan() => Array.AsSpan();

    public static implicit operator ReadOnlySpan<T>(ValueArray<T> array) => array.AsSpan();

    public static implicit operator ValueArray<T>(T[] array) => new(array);

    public static explicit operator T[](ValueArray<T> array) => array.Array;

    #region IReadOnlyList
    public T this[int index] => Array[index];

    int IReadOnlyCollection<T>.Count => Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Array).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Array.GetEnumerator();
    #endregion

    #region IEquatable
    public static bool operator ==(ValueArray<T> left, ValueArray<T> right) => left.Equals(right);

    public static bool operator !=(ValueArray<T> left, ValueArray<T> right) => !(left == right);

    public override bool Equals(object? obj) => Equals(obj, EqualityComparer<T>.Default);

    public bool Equals(ValueArray<T> other) => Equals(other, EqualityComparer<T>.Default);

    public bool Equals(object? other, IEqualityComparer comparer) =>
        other is ValueArray<T> obj && ((IStructuralEquatable)Array).Equals(obj.Array, comparer);

    public override int GetHashCode() => GetHashCode(EqualityComparer<T>.Default);

    public int GetHashCode(IEqualityComparer comparer) => ((IStructuralEquatable)Array).GetHashCode(comparer);
    #endregion

    #region IComparable
    public static bool operator <(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) < 0;

    public static bool operator <=(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) <= 0;

    public static bool operator >(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) > 0;

    public static bool operator >=(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) >= 0;

    public int CompareTo(ValueArray<T> other) => CompareTo(other, Comparer<T>.Default);

    public int CompareTo(object? other, IComparer comparer) =>
        ((IStructuralComparable)Array).CompareTo(other is ValueArray<T> array ? array.Array : null, comparer);
    #endregion
}

internal class ValueArrayDebugView<T>(ValueArray<T> array)
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => (T[])array;
}
