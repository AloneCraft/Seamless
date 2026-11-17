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
/// <summary>シングル用ハンドルで共有する読み取りセッションへの同期アクセスを保護する。</summary>
internal readonly struct ScanScope:IDisposable{
    private readonly object? gate;
#if DEBUG
    private readonly CollectionStore? store;
#endif
    internal ScanScope(object gate,CollectionStore? store){
        global::System.Threading.Monitor.Enter(gate);
        this.gate=gate;
#if DEBUG
        this.store=store;
        try{this.store?.EnterScan();}
        catch{global::System.Threading.Monitor.Exit(gate);throw;}
#endif
    }
    public void Dispose(){
        if(this.gate is null){return;}
        try{
#if DEBUG
            this.store?.ExitScan();
#endif
        }finally{global::System.Threading.Monitor.Exit(this.gate);}
    }
}
