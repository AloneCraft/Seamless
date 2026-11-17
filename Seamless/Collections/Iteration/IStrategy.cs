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

/// <summary>要素を処理し、falseで走査を打ち切る。可変状態は呼び出しごとのstructに保持する。</summary>
public interface IStrategy<TSelf,in T> where TSelf:struct,IStrategy<TSelf,T>{
    static abstract bool Invoke(ref TSelf self,T item);
}
