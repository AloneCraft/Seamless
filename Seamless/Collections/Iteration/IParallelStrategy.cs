using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Collections.Iteration;

/// <summary>
/// 独立した部分結果を任意の順序で結合できる全件走査。
/// Invokeは常にtrueを返し、順序依存・外部への副作用・共有可変状態を持たないこと。
/// CreatePartialは独立状態、Mergeは結合則と交換則を満たす演算を実装する。
/// 通常のIStrategyや読取り専用という情報だけではこの契約を推定しない。
/// </summary>
public interface IParallelStrategy<TSelf,in T>:IStrategy<TSelf,T> where TSelf:struct,IParallelStrategy<TSelf,T>{
    static abstract TSelf CreatePartial(in TSelf seed);
    static abstract void Merge(ref TSelf result,in TSelf partial);
}
