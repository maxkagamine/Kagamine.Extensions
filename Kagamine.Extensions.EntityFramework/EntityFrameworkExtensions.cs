// Copyright (c) Max Kagamine
// Licensed under the Apache License, Version 2.0

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kagamine.Extensions.EntityFramework;

public static class EntityFrameworkExtensions
{
#if NET8_0
    /// <summary>
    /// Asynchronously creates a <see cref="HashSet{T}"/> from an <see cref="IQueryable{T}"/> by enumerating it
    /// asynchronously.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
    /// <param name="source">A <see cref="IQueryable{T}"/> from which to create a set.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing values in the
    /// set, or <see langword="null"/> to use the default <see cref="IEqualityComparer{T}"/> implementation for the set
    /// type.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to
    /// complete.</param>
    public static async Task<HashSet<TSource>> ToHashSetAsync<TSource>(this IQueryable<TSource> source, IEqualityComparer<TSource>? comparer, CancellationToken cancellationToken = default)
    {
        var set = new HashSet<TSource>(comparer);
        await foreach (var element in source.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            set.Add(element);
        }

        return set;
    }

    /// <inheritdoc cref="ToHashSetAsync{TSource}(IQueryable{TSource}, IEqualityComparer{TSource}?, CancellationToken)"/>
    public static Task<HashSet<TSource>> ToHashSetAsync<TSource>(this IQueryable<TSource> source, CancellationToken cancellationToken = default)
        => ToHashSetAsync(source, null, cancellationToken);
#endif

    /// <summary>
    /// Sets the values and navigation properties of this entity by copying from the given object.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entry">The change tracking entry for the tracked entity whose values and navigation properties to replace.</param>
    /// <param name="obj">The detached entity whose values and navigation properties to copy.</param>
    public static void SetValuesAndNavigations<T>(this EntityEntry<T> entry, T obj) where T : class
    {
        entry.CurrentValues.SetValues(obj);

        foreach (var navigation in entry.Navigations)
        {
            navigation.CurrentValue = entry.Context.Entry(obj).Navigations
                .Single(n => n.Metadata.Name == navigation.Metadata.Name)
                .CurrentValue;
        }
    }

    /// <summary>
    /// Updates <paramref name="entity"/> by setting its values and navigation properties to those of <paramref
    /// name="valuesFrom"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="set">The <see cref="DbSet{TEntity}"/>.</param>
    /// <param name="entity">The tracked entity whose values and navigation properties to replace.</param>
    /// <param name="valuesFrom">The detached entity whose values and navigation properties to copy.</param>
    public static void Update<T>(this DbSet<T> set, T entity, T valuesFrom) where T : class
    {
        set.Entry(entity).SetValuesAndNavigations(valuesFrom);
    }
}
