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
/// System.Collections.Generic.IEnumerable&lt;T&gt; を包んで、配列・List・SerializedList から
/// 暗黙変換で受け取れるようにする入力アダプター。Database の View/TableFunction 引数で使う。
/// </summary>
public sealed class EnumerableSource<T>:IEnumerable<T> {
    private readonly IEnumerable<T> source;

    public EnumerableSource(IEnumerable<T> source) {
        ArgumentNullException.ThrowIfNull(source);
        this.source=source;
    }

    public static implicit operator EnumerableSource<T>(T[] source) => new(source);
    public static implicit operator EnumerableSource<T>(List<T> source) => new(source);
    public static implicit operator EnumerableSource<T>(SerializedList<T> source) => new(source);

    public IEnumerator<T> GetEnumerator() => this.source.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}
