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

public sealed class DelegateSet<TRecord>(
    Func<TRecord,bool>         add,
    Func<TRecord,bool>         remove,
    Func<TRecord,bool>         contains,
    Func<IEnumerator<TRecord>> getEnumerator,
    Func<int>                  getCount,
    Action?                    dispose=null)
    :IReadOnlyCollection<TRecord>,IDisposable {
    private readonly Action                     dispose      =dispose??(static () => { });
    public           int                        Count                  => getCount();
    public           bool                       Add(TRecord      item) => add(item);
    public           bool                       Remove(TRecord   item) => remove(item);
    public           bool                       Contains(TRecord item) => contains(item);
    public           IEnumerator<TRecord>       GetEnumerator()        => getEnumerator();
    IEnumerator IEnumerable.                    GetEnumerator()        => this.GetEnumerator();
    public void                                 Dispose()              => this.dispose();
}

public sealed class ReadSet<TRecord>(SerializedList<TRecord> source):IReadOnlyCollection<TRecord>,IDisposable {
    public int Count => checked((int)source.Count);
    public long LongCount => source.Count;
    public IEnumerator<TRecord> GetEnumerator() => source.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    public void Dispose() => source.Dispose();
}
