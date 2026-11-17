using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
namespace Seamless.Linq;

public static partial class Enumerable {
    [return: UnorderedEnumerable]
    public static ValueEnumerable<PersistedSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Collections.Persisted.Set<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteHashSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Collections.Tsavorite.HashSet<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteSetEnumerator<TKey,TRecord>,TRecord> AsValueEnumerable<TKey,TRecord>(
        this global::Seamless.Collections.Tsavorite.Set<TKey,TRecord> source)
        where TRecord : global::Seamless.Collections.IRecord<TKey> =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteParallelHashSetEnumerator<T>,T> AsValueEnumerable<T>(
        this global::Seamless.Collections.Tsavorite.ParallelHashSet<T> source) =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));

    [return: UnorderedEnumerable]
    public static ValueEnumerable<TsavoriteParallelSetEnumerator<TKey,TRecord>,TRecord> AsValueEnumerable<TKey,TRecord>(
        this global::Seamless.Collections.Tsavorite.ParallelSet<TKey,TRecord> source)
        where TRecord : global::Seamless.Collections.IRecord<TKey> =>
        new(new(source??throw new global::System.ArgumentNullException(nameof(source))));
}
