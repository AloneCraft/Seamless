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

/// <summary>
/// Tsavorite バックエンドを持つ HashSet の共通インターフェース。
/// HashSet と HashSet の両実装が共有する操作を公開する。
/// 引数は ITsavoriteHashSet&lt;T&gt; を受け取る既定実装を持つ。
/// </summary>
public interface IHashSet<T>:IMemoryCollection<T>, Seamless.Collections.ISet<T>, IDisposable {
    /// <summary>このインスタンスが既に破棄されているかどうかを取得する。</summary>
    bool IsDisposed { get; }
    /// <summary>
    /// 内部 session を張り替えて、Tsavorite が保持している古い epoch 世代参照を解放する。
    /// 既定実装は no-op で、session を明示管理する実装が必要に応じて上書きする。
    /// </summary>
    void Refresh() {
    }
    new int Count { get; }
    new bool Contains(T item);
    new bool Add(T item);

}
