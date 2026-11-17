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
/// 操作用オブジェクトの使い方(モード)。構造体の型引数として渡し、モードごとの違いを JIT が型ごとに決める(操作ごとの判定は残らない)。
/// </summary>
public interface IAccessMode {
    /// <summary>
    /// true ならシングル用。データ本体に同時に 1 つだけ作り、件数をデータ本体で管理し、索引が混んだら自動で広げる。
    /// false ならマルチ用。並列区間の中でスレッドごとに作り、件数を管理せず、索引を広げない。
    /// </summary>
    static abstract bool IsExclusive { get; }
}

/// <summary>シングル用の操作用オブジェクト(<c>データ本体.Create()</c>)。</summary>
public readonly struct ExclusiveAccess:IAccessMode {
    public static bool IsExclusive => true;
}

/// <summary>マルチ用の操作用オブジェクト(並列区間の <c>Create()</c>)。</summary>
public readonly struct ParallelAccess:IAccessMode {
    public static bool IsExclusive => false;
}
