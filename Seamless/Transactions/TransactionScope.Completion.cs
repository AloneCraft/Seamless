using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;
namespace Seamless.Transactions;

public sealed partial class TransactionScope{
    /// <summary>rootの排出後に外部所有資源を返す内部hook。Disposeはcallbackを解除する。</summary>
    internal IDisposable OnCompletion(Action callback){
        ArgumentNullException.ThrowIfNull(callback);using var access=this.Enter(allowQuery:true);var memory=this.state.Context.Domain?.ReserveMetadata(this.state.Context,128);
        try{
            var registration=new CompletionRegistration(this.state,callback,memory);
            lock(this.state.Completions){ObjectDisposedException.ThrowIf(Volatile.Read(ref this.state.Retired)!=0,this);this.state.Completions.Add(registration);}return registration;
        }catch{memory?.Dispose();throw;}
    }
    private sealed class CompletionRegistration(State owner,Action callback,ResourceReservation? memory):IDisposable{
        private State? owner=owner;
        private Action? callback=callback;
        internal void Run()=>this.Close(true);
        public void Dispose()=>this.Close(false);
        private void Close(bool run){
            var state=Interlocked.Exchange(ref this.owner,null);if(state is null){return;}lock(state.Completions){state.Completions.Remove(this);}var action=Interlocked.Exchange(ref this.callback,null);
            try{if(run){action?.Invoke();}}finally{memory?.Dispose();}
        }
    }
}
