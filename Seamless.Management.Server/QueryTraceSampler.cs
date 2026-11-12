using Seamless.Execution;

namespace Seamless.Management.Server;

/// <summary>ホスト内の明示サンプリングだけを処理する。DB・保存storeへは触れない。</summary>
internal sealed class QueryTraceSampler:IAsyncDisposable{
    private readonly object gate=new();
    private readonly Dictionary<QueryTraceCapture,ResourceReservation> active=new();
    private readonly ResourceBudget budget;
    private readonly TimeProvider clock;
    private readonly Action failure;
    private readonly int maximum;
    private ITimer? timer;
    private bool closed;
    private int sampling;
    internal QueryTraceSampler(ResourceBudget budget,Action failure,int maximum=32,TimeProvider? clock=null){this.budget=budget;this.failure=failure;this.maximum=maximum;this.clock=clock??TimeProvider.System;}
    internal IDisposable? Register(QueryTraceCapture capture){
        lock(this.gate){
            if(this.closed||this.active.Count>=this.maximum){this.failure();return null;}
            ResourceReservation reservation;
            try{reservation=this.budget.ReserveMemory(4096);}catch(ResourceLimitException){this.failure();return null;}
            if(this.active.ContainsKey(capture)){reservation.Dispose();throw new InvalidOperationException("同じトレースが登録済みです。");}
            this.active.Add(capture,reservation);
            try{this.timer??=this.clock.CreateTimer(static value=>((QueryTraceSampler)value!).Sample(),this,TimeSpan.FromMilliseconds(100),TimeSpan.FromMilliseconds(100));}
            catch(Exception){this.active.Remove(capture);reservation.Dispose();this.failure();return null;}
            return new Registration(this,capture,reservation);
        }
    }
    private void Sample(){
        if(Interlocked.Exchange(ref this.sampling,1)!=0){return;}
        try{
            QueryTraceCapture[] captures;lock(this.gate){captures=this.active.Keys.ToArray();}
            foreach(var capture in captures){try{capture.Sample();}catch(Exception){this.failure();}}
        }finally{Volatile.Write(ref this.sampling,0);}
    }
    private void Remove(QueryTraceCapture capture,ResourceReservation reservation){
        lock(this.gate){this.active.Remove(capture);reservation.Dispose();if(this.active.Count==0){this.timer?.Dispose();this.timer=null;}}
    }
    public ValueTask DisposeAsync(){
        KeyValuePair<QueryTraceCapture,ResourceReservation>[] captures;
        lock(this.gate){this.closed=true;this.timer?.Dispose();this.timer=null;captures=this.active.ToArray();this.active.Clear();}
        foreach(var capture in captures){capture.Key.StopSampling();capture.Value.Dispose();}return ValueTask.CompletedTask;
    }
    private sealed class Registration(QueryTraceSampler owner,QueryTraceCapture capture,ResourceReservation reservation):IDisposable{
        private int disposed;
        public void Dispose(){if(Interlocked.Exchange(ref this.disposed,1)==0){owner.Remove(capture,reservation);}}
    }
}
