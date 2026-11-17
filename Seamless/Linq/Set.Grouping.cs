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
using SeamlessSet = Seamless.Collections;

namespace Seamless.Linq;

// キーでまとめる演算。結果のキーは重複しないので、組(またはキーと値の組)の集合になる。
public static partial class Set {
    /// <summary>
    /// キーごとの組の集合。元の集合を1回だけ読み、キーの辞書で組を作る。null のキーの要素は1つの組にまとめる(System.Linq・SQL の GROUP BY と同じ)。
    /// 組の中の要素は重複を除かない。組どうしの等しさはキーで決める。
    /// </summary>
    public static SeamlessSet.ISet<IGrouping<TKey,TSource>> GroupBy<TSource, TKey>(this SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(keySelector);
        return new DistinctSequenceSet<IGrouping<TKey,TSource>>(() => Groups(source,keySelector,static item => item));
    }

    /// <summary>キーごとの組の集合。組の中身は elementSelector で写した値(重複を除かない)。</summary>
    public static SeamlessSet.ISet<IGrouping<TKey,TElement>> GroupBy<TSource, TKey, TElement>(this SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        return new DistinctSequenceSet<IGrouping<TKey,TElement>>(() => Groups(source,keySelector,elementSelector));
    }

    /// <summary>キーと組の要素から写した値の集合。写した結果の重複は除く。</summary>
    public static SeamlessSet.ISet<TResult> GroupBy<TSource, TKey, TResult>(this SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector,Func<TKey,IEnumerable<TSource>,TResult> resultSelector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => Project(Groups(source,keySelector,static item => item),resultSelector));
    }

    /// <summary>キーと、写した組の要素から写した値の集合。写した結果の重複は除く。</summary>
    public static SeamlessSet.ISet<TResult> GroupBy<TSource, TKey, TElement, TResult>(this SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector,Func<TKey,IEnumerable<TElement>,TResult> resultSelector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(keySelector);
        ArgumentNullException.ThrowIfNull(elementSelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => Project(Groups(source,keySelector,elementSelector),resultSelector));
    }

    /// <summary>キーごとの要素の数(キーと数の組の集合)。null のキーも1つのキーとして数える。</summary>
    public static SeamlessSet.ISet<KeyValuePair<TKey,int>> CountBy<TSource, TKey>(this SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector) {
        RequireSet(source,nameof(source));
        ArgumentNullException.ThrowIfNull(keySelector);
        return new DistinctSequenceSet<KeyValuePair<TKey,int>>(() => Counts(source,keySelector));
    }

    private static IEnumerable<IGrouping<TKey,TElement>> Groups<TSource, TKey, TElement>(SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector,Func<TSource,TElement> elementSelector) {
        var groups = new Dictionary<JoinKey<TKey>,KeyedGroup<TKey,TElement>>();
        KeyedGroup<TKey,TElement>? nullGroup = null;
        foreach(var item in source) {
            var key = keySelector(item);
            KeyedGroup<TKey,TElement>? group;
            if(key is null) {
                nullGroup??=new(key);
                group=nullGroup;
            } else if(!groups.TryGetValue(new(key),out group)) {
                group=new(key);
                groups.Add(new(key),group);
            }
            group.Items.Add(elementSelector(item));
        }
        foreach(var group in groups.Values) {
            yield return group;
        }
        if(nullGroup is not null) {
            yield return nullGroup;
        }
    }

    private static IEnumerable<TResult> Project<TKey, TElement, TResult>(IEnumerable<IGrouping<TKey,TElement>> groups,Func<TKey,IEnumerable<TElement>,TResult> resultSelector) {
        foreach(var group in groups) {
            yield return resultSelector(group.Key,group);
        }
    }

    private static IEnumerable<KeyValuePair<TKey,int>> Counts<TSource, TKey>(SeamlessSet.ISet<TSource> source,Func<TSource,TKey> keySelector) {
        foreach(var group in Groups(source,keySelector,static item => item)) {
            yield return new(group.Key,((KeyedGroup<TKey,TSource>)group).Items.Count);
        }
    }

    // キーで等しさを決める組。中身は重複を除かずに持つ。
    private sealed class KeyedGroup<TKey, TElement>(TKey key):IGrouping<TKey,TElement> {
        public TKey Key { get; } = key;
        public List<TElement> Items { get; } = [];
        public IEnumerator<TElement> GetEnumerator() => this.Items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        public override bool Equals(object? obj) => obj is KeyedGroup<TKey,TElement> other&&EqualityComparer<TKey>.Default.Equals(this.Key,other.Key);
        public override int GetHashCode() {
            if(this.Key is null) {
                return 0;
            }
            return EqualityComparer<TKey>.Default.GetHashCode(this.Key);
        }
    }
}
