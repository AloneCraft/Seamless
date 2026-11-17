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

/// <summary>遅延列挙のMoveNext間でスレッドが変わっても返却できる構造保護。</summary>
internal sealed class HistoryStructureGate{
    private readonly object sync=new();
    private int readers;
    private bool writing;
    internal IDisposable Read(){lock(this.sync){while(this.writing){Monitor.Wait(this.sync);}this.readers++;return new Lease(this,false);}}
    internal IDisposable Write(){lock(this.sync){while(this.writing||this.readers!=0){Monitor.Wait(this.sync);}this.writing=true;return new Lease(this,true);}}
    private void Release(bool write){lock(this.sync){if(write){this.writing=false;}else{this.readers--;}Monitor.PulseAll(this.sync);}}
    private sealed class Lease(HistoryStructureGate owner,bool write):IDisposable{
        private HistoryStructureGate? owner=owner;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release(write);
    }
}
