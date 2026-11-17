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

// 結合のうち、Set.cs の Join・LeftJoin 以外のもの。
public static partial class Set {
    /// <summary>
    /// 右外部結合。内側の要素ごとに、キーが等しい外側の要素との組を写す。等しい外側の要素が無い内側の要素は、外側を既定値にして写す。
    /// null のキーはどの要素とも結び付けない(System.Linq.Enumerable.RightJoin と同じ)。結果の重複は除く。
    /// 外側が構造体なら、対応が無いことを区別できるよう Nullable にして渡す。
    /// </summary>
    public static SeamlessSet.ISet<TResult> RightJoin<TOuter, TInner, TKey, TResult>(this SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector) {
        RequireSet(outer,nameof(outer));
        RequireSet(inner,nameof(inner));
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => RightJoinRows(outer,inner,outerKeySelector,innerKeySelector,resultSelector));
    }

    /// <summary>
    /// 外側の要素ごとに、キーが等しい内側の要素の並び(無ければ空)を渡して写す。null のキーの外側には空の並びを渡す。
    /// 内側の並びは重複を除かない(集約に使えるよう、内側の要素をそのまま渡す)。結果の重複は除く。
    /// </summary>
    public static SeamlessSet.ISet<TResult> GroupJoin<TOuter, TInner, TKey, TResult>(this SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector) {
        RequireSet(outer,nameof(outer));
        RequireSet(inner,nameof(inner));
        ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        return new DistinctSequenceSet<TResult>(() => GroupJoinRows(outer,inner,outerKeySelector,innerKeySelector,resultSelector));
    }

    private static IEnumerable<TResult> RightJoinRows<TOuter, TInner, TKey, TResult>(SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter?,TInner,TResult> resultSelector) {
        var lookup = Lookup(outer,outerKeySelector);
        foreach(var row in inner) {
            var key = innerKeySelector(row);
            if(key is null||!lookup.TryGetValue(new(key),out var matches)) {
                yield return resultSelector(default,row);
                continue;
            }
            foreach(var match in matches) {
                yield return resultSelector(match,row);
            }
        }
    }

    private static IEnumerable<TResult> GroupJoinRows<TOuter, TInner, TKey, TResult>(SeamlessSet.ISet<TOuter> outer,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,IEnumerable<TInner>,TResult> resultSelector) {
        var lookup = Lookup(inner,innerKeySelector);
        foreach(var item in outer) {
            var key = outerKeySelector(item);
            if(key is null||!lookup.TryGetValue(new(key),out var matches)) {
                yield return resultSelector(item,[]);
                continue;
            }
            yield return resultSelector(item,matches);
        }
    }

    // キーごとの要素の一覧。null のキーの要素は入れない(どの要素とも結び付けないため)。
    private static Dictionary<JoinKey<TKey>,List<TSource>> Lookup<TSource, TKey>(IEnumerable<TSource> source,Func<TSource,TKey> keySelector) {
        var lookup = new Dictionary<JoinKey<TKey>,List<TSource>>();
        foreach(var item in source) {
            var key = keySelector(item);
            if(key is null) {
                continue;
            }
            if(!lookup.TryGetValue(new(key),out var list)) {
                list=[];
                lookup.Add(new(key),list);
            }
            list.Add(item);
        }
        return lookup;
    }
}
