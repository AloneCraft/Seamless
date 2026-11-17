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

// キーで絞る演算と、空の集合・空のときの既定値。
public static partial class Set {
    /// <summary>キーが keys に含まれる要素の部分集合。</summary>
    public static SeamlessSet.ISet<TSource> IntersectBy<TSource, TKey>(this SeamlessSet.ISet<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector) {
        RequireSet(first,nameof(first));
        RequireSet(second,nameof(second));
        ArgumentNullException.ThrowIfNull(keySelector);
        return new KeyFilteredSet<TSource,TKey>(first,second,keySelector,keepContained: true);
    }

    /// <summary>キーが keys に含まれない要素の部分集合。</summary>
    public static SeamlessSet.ISet<TSource> ExceptBy<TSource, TKey>(this SeamlessSet.ISet<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector) {
        RequireSet(first,nameof(first));
        RequireSet(second,nameof(second));
        ArgumentNullException.ThrowIfNull(keySelector);
        return new KeyFilteredSet<TSource,TKey>(first,second,keySelector,keepContained: false);
    }

    /// <summary>空なら既定値1つだけの集合、空でなければ元の集合。</summary>
    public static SeamlessSet.ISet<TSource?> DefaultIfEmpty<TSource>(this SeamlessSet.ISet<TSource> source) {
        RequireSet(source,nameof(source));
        return new DefaultSet<TSource?>(source!,default);
    }

    /// <summary>空なら defaultValue 1つだけの集合、空でなければ元の集合。</summary>
    public static SeamlessSet.ISet<TSource> DefaultIfEmpty<TSource>(this SeamlessSet.ISet<TSource> source,TSource defaultValue) {
        RequireSet(source,nameof(source));
        return new DefaultSet<TSource>(source,defaultValue);
    }

    /// <summary>空の集合。</summary>
    public static SeamlessSet.ISet<T> Empty<T>() => EmptySet<T>.Instance;

    private sealed class KeyFilteredSet<TSource, TKey>(SeamlessSet.ISet<TSource> first,IEnumerable<TKey> second,Func<TSource,TKey> keySelector,bool keepContained):ComputedSet<TSource> {
        public override bool Contains(TSource item) => first.Contains(item)&&this.Keys().Contains(keySelector(item))==keepContained;

        public override IEnumerator<TSource> GetEnumerator() {
            var keys = this.Keys();
            foreach(var item in first) {
                if(keys.Contains(keySelector(item))==keepContained) {
                    yield return item;
                }
            }
        }

        // 読むたびに作り直す(元の並びが変われば結果も変わる)。
        private HashSet<TKey> Keys() => [.. second];
    }

    private sealed class DefaultSet<T>(SeamlessSet.ISet<T> source,T defaultValue):ComputedSet<T> {
        public override bool Contains(T item) {
            if(source.Count==0) {
                return EqualityComparer<T>.Default.Equals(defaultValue,item);
            }
            return source.Contains(item);
        }

        public override IEnumerator<T> GetEnumerator() {
            var any = false;
            foreach(var item in source) {
                any=true;
                yield return item;
            }
            if(!any) {
                yield return defaultValue;
            }
        }
    }

    private sealed class EmptySet<T>:ComputedSet<T> {
        public static EmptySet<T> Instance { get; } = new();
        public override bool Contains(T item) => false;
        public override IEnumerator<T> GetEnumerator() {
            yield break;
        }
    }
}
