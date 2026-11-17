using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Seamless.Execution;

/// <summary>Value を使う区間の保持予約を所有する。取得した参照もこの所有寿命内で使う。</summary>
public sealed class OwnedValue<T>:IDisposable{
    internal const int HeaderBytes=128;
    private T value;
    private ResourceReservation? reservation;
    private ExecutionScope? scope;
    internal OwnedValue(T value,ResourceReservation reservation){this.value=value;this.reservation=reservation;}
    public T Value{get{ObjectDisposedException.ThrowIf(Volatile.Read(ref this.reservation) is null,this);return this.value;}}
    public long MemoryBytes=>Volatile.Read(ref this.reservation)?.Bytes??0;
    internal void Attach(ExecutionScope scope)=>this.scope=scope;
    public void Dispose(){
        var reservation=Interlocked.Exchange(ref this.reservation,null);if(reservation is null){return;}
        this.value=default!;
        try{reservation.Dispose();}finally{this.scope?.Release(this);this.scope=null;}
    }
}
