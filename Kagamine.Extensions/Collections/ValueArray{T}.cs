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

    private readonly T[]? inner;

    public ValueArray(T[] array)
    {
        inner = array;
    }

    /// <summary>
    /// Gets the number of elements in the array.
    /// </summary>
    public int Length => inner?.Length ?? 0;

    /// <summary>
    /// Creates a new span over the array.
    /// </summary>
    public ReadOnlySpan<T> AsSpan() => inner is null ? ReadOnlySpan<T>.Empty : inner.AsSpan();

    public static implicit operator ReadOnlySpan<T>(ValueArray<T> array) => array.AsSpan();

    public static implicit operator ValueArray<T>(T[] array) => new(array);

    public static explicit operator T[](ValueArray<T> array) => array.inner ?? [];

    #region IReadOnlyList
    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The index of the element to get.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is less than 0 or greater than or equal to
    /// <see cref="Length"/>.</exception>
    public T this[int index] => (inner ?? [])[index];

    int IReadOnlyCollection<T>.Count => Length;

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(inner ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (inner ?? []).GetEnumerator();
    #endregion

    #region IEquatable
    public static bool operator ==(ValueArray<T> left, ValueArray<T> right) => left.Equals(right);

    public static bool operator !=(ValueArray<T> left, ValueArray<T> right) => !(left == right);

    public bool Equals(ValueArray<T> other) => Equals(other, EqualityComparer<T>.Default);

    /// <inheritdoc cref="Equals(ValueArray{T})"/>
    /// <param name="other">An object to compare with this object.</param>
    /// <param name="comparer">An object that determines whether the current instance and <paramref name="other"/> are
    /// equal.</param>
    public bool Equals(ValueArray<T> other, IEqualityComparer comparer) =>
        ((IStructuralEquatable)(inner ?? [])).Equals(other.inner ?? [], comparer);

    public override bool Equals(object? obj) =>
        obj is ValueArray<T> array && Equals(array, EqualityComparer<T>.Default);

    bool IStructuralEquatable.Equals(object? other, IEqualityComparer comparer) =>
        other is ValueArray<T> array && Equals(array, comparer);

    public override int GetHashCode() => GetHashCode(EqualityComparer<T>.Default);

    public int GetHashCode(IEqualityComparer comparer) => ((IStructuralEquatable)(inner ?? [])).GetHashCode(comparer);
    #endregion

    #region IComparable
    public static bool operator <(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) < 0;

    public static bool operator <=(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) <= 0;

    public static bool operator >(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) > 0;

    public static bool operator >=(ValueArray<T> left, ValueArray<T> right) => left.CompareTo(right) >= 0;

    public int CompareTo(ValueArray<T> other) => CompareTo(other, Comparer<T>.Default);

    /// <inheritdoc cref="CompareTo(ValueArray{T})"/>
    /// <param name="other">An object to compare with this instance.</param>
    /// <param name="comparer">An object that compares members of the current collection object with the corresponding
    /// members of <paramref name="other"/>.</param>
    public int CompareTo(ValueArray<T> other, IComparer comparer) =>
        ((IStructuralComparable)(inner ?? [])).CompareTo(other.inner ?? [], comparer);

    int IStructuralComparable.CompareTo(object? other, IComparer comparer)
    {
        if (other is not ValueArray<T> array || array.Length != Length)
        {
            // Consistent with Array's IStructuralComparable.CompareTo(). Note that you can still do
            // `valueArray.CompareTo(actualArray, comparer)`, as actualArray will get implicitly cast to ValueArray<T>.
            throw new ArgumentException($"The object is not a {nameof(ValueArray<>)} with the same number of elements as the array to compare it to.", nameof(other));
        }

        return CompareTo(array, comparer);
    }
    #endregion
}

internal sealed class ValueArrayDebugView<T>(ValueArray<T> array)
{
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => (T[])array;
}
