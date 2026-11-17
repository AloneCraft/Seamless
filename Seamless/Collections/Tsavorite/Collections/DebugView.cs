using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
namespace Seamless.Collections.Tsavorite;

public abstract class DebugView<TKey> {
    /// <summary>
    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
    /// </summary>
    private const int MaxItems = 10_000;
    private readonly IMemoryCollection<TKey> Collection;
    private protected DebugView(IMemoryCollection<TKey> collection) {
        ArgumentNullException.ThrowIfNull(collection);
        this.Collection=collection;
    }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public string[] Items {
        get {
            var items = new List<string?>(Math.Min(64,MaxItems));
            foreach(var item in this.Collection) {
                if(items.Count>=MaxItems)
                    break;
                Debug.Assert(item!=null,nameof(item)+" != null");
                items.Add(item.ToString());
            }
            //items.Add("abc");
            return items.ToArray()!;
        }
    }
    // public TKey[] Items {
    //     get {
    //         var items = new List<TKey>(Math.Min(64,MaxItems));
    //         // foreach(var item in this.Collection) {
    //         //     if(items.Count>=MaxItems)
    //         //         break;
    //         //     items.Add(item);
    //         // }
    //         //items.Add(default!);
    //         return items.ToArray();
    //     }
    // }
}
public abstract class DebugView<TKey, TValue>:DebugView<KeyValuePair<TKey,TValue>> {
    private protected DebugView(IMemoryCollection<KeyValuePair<TKey,TValue>> collection) : base(collection) {
    }
}
public class HashSetDebugView<TKey>(IMemoryCollection<TKey> collection):DebugView<TKey>(collection) {
}
/// <summary>
/// DebuggerTypeProxy の型引数数を Dictionary の 4 型引数に合わせるためのラッパー。
/// デバッガーはオープンジェネリック型を使う際に型引数数が一致している必要がある。
/// </summary>
public class DictionaryDebugView<TKey, TValue, TKeySerializer, TValueSerializer>(IMemoryCollection<KeyValuePair<TKey,TValue>> collection):DebugView<TKey,TValue>(collection) where TKeySerializer : global::Seamless.Collections.Serializers.IStrategy<TKey>
    where TValueSerializer : global::Seamless.Collections.Serializers.IStrategy<TValue> {
}
