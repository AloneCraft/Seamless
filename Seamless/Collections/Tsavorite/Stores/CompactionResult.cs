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
/// <summary>詰め直し(<see cref="CollectionStore{TKey,TValue,TStoreFunctions,TAllocator}.Compact"/>)の前後のログの位置。</summary>
/// <param name="BeginAddressBefore">詰め直す前の先頭アドレス。</param>
/// <param name="BeginAddressAfter">詰め直したあとの先頭アドレス。</param>
/// <param name="TailAddressBefore">詰め直す前の末尾アドレス。</param>
/// <param name="TailAddressAfter">詰め直したあとの末尾アドレス。生きているレコードの写しが末尾に書かれるので、増える。</param>
public readonly record struct CompactionResult(long BeginAddressBefore,long BeginAddressAfter,long TailAddressBefore,long TailAddressAfter) {
    /// <summary>詰め直す前の、先頭から末尾までの大きさ。</summary>
    public long LogBytesBefore => this.TailAddressBefore-this.BeginAddressBefore;
    /// <summary>詰め直したあとの、先頭から末尾までの大きさ。</summary>
    public long LogBytesAfter => this.TailAddressAfter-this.BeginAddressAfter;
    /// <summary>空きへ戻った大きさ。</summary>
    public long ReclaimedBytes => this.LogBytesBefore-this.LogBytesAfter;
}
