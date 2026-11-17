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

public static class AnchorExtensions {
    extension<T>(T value) {
        public T Anchor {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value;
        }
    }
}

public enum QueryOrder {
    Auto,
    Preserve,
    AllowUnordered
}

partial class Enumerable {
    /// <summary>直前のJoinまたはUnionに対するFLinqの順序ヒント。</summary>
    public static IEnumerable<T> WithOrder<T>(this IEnumerable<T> source,QueryOrder order){
        ArgumentNullException.ThrowIfNull(source);
        if(order is not (QueryOrder.Auto or QueryOrder.Preserve or QueryOrder.AllowUnordered)){
            throw new ArgumentOutOfRangeException(nameof(order));
        }
        return source;
    }

    /// <summary>直前の対応するDB演算子に、producerを含む実行枠の上限を指定する。</summary>
    /// <remarks>FLinqが対応するDBクエリを展開するときに適用する。拡張メソッド自体は引数の検証後に入力列を返す。</remarks>
    public static IEnumerable<T> Parallel<T>(this IEnumerable<T> source,int maxExecutionSlots){
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExecutionSlots);
        return source;
    }
}
