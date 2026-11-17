using Seamless.Execution;

namespace Seamless.Management.Server;

/// <summary>記録の到着だけを通知する有界hub。DB・認証情報は保持しない。</summary>
internal sealed class QueryTraceNotifications{
    private sealed class Target(ResourceReservation memory){
        internal readonly ResourceReservation Memory=memory;
        internal long Revision;
        internal readonly List<TaskCompletionSource<QueryTraceWatchResult>> Waiting=[];
    }
    private readonly object gate=new();
    private readonly ResourceBudget budget;
    private readonly Dictionary<(string,string),Target> targets=new();
    private readonly Guid session=Guid.NewGuid();
    private readonly int maximumWatches,maximumTargets;
    private int watches;
    private long revision;
    private bool closed;
    internal QueryTraceNotifications(ResourceBudget budget,int maximumWatches=128,int maximumTargets=256){this.budget=budget;this.maximumWatches=maximumWatches;this.maximumTargets=maximumTargets;}
    internal QueryTraceWatchTicket Watch(QueryTraceWatchQuery query){
        if(string.IsNullOrWhiteSpace(query.DefinitionId)||query.DefinitionId.Length>4096||string.IsNullOrWhiteSpace(query.ObjectId)||query.ObjectId.Length>4096||query.AfterRevision<0||query.TimeoutMilliseconds is <1 or >30000){throw new ManagementException("InvalidRequest","トレース通知の条件が不正です。");}
        lock(this.gate){
            if(this.closed){throw new ManagementException("Stopped","トレース通知を停止しています。");}
            var key=(query.DefinitionId,query.ObjectId);
            var target=this.GetTarget(key);
            if(query.HubSession!=this.session||target.Revision>query.AfterRevision){return new(Task.FromResult(new QueryTraceWatchResult(this.session,target.Revision,true)),static()=>{},query.TimeoutMilliseconds,()=>new(this.session,target.Revision,false));}
            if(this.watches>=this.maximumWatches){throw new ManagementException("Limit","トレース通知の待機上限に達しました。");}
            ResourceReservation memory;
            try{memory=this.budget.ReserveMemory(512);}catch(ResourceLimitException){throw new ManagementException("Limit","トレース通知のメモリ予算がありません。");}
            var source=new TaskCompletionSource<QueryTraceWatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);target.Waiting.Add(source);this.watches++;
            return new(source.Task,()=>{lock(this.gate){if(target.Waiting.Remove(source)){this.watches--;}memory.Dispose();}},query.TimeoutMilliseconds,()=>{lock(this.gate){return new(this.session,target.Revision,false);}});
        }
    }
    private Target GetTarget((string,string) key){
        if(this.targets.TryGetValue(key,out var target)){return target;}
        if(this.targets.Count==this.maximumTargets){
            var available=this.targets.FirstOrDefault(pair=>pair.Value.Waiting.Count==0);
            if(available.Value is null){throw new ManagementException("Limit","トレース通知の対象上限に達しました。");}
            this.targets.Remove(available.Key);available.Value.Memory.Dispose();
        }
        ResourceReservation memory;
        try{memory=this.budget.ReserveMemory(512+4L*(key.Item1.Length+key.Item2.Length));}catch(ResourceLimitException){throw new ManagementException("Limit","トレース通知の対象メモリ予算がありません。");}
        target=new(memory){Revision=++this.revision};this.targets.Add(key,target);return target;
    }
    internal void Pulse(string definitionId,string objectId){
        TaskCompletionSource<QueryTraceWatchResult>[] waiting;QueryTraceWatchResult result;
        lock(this.gate){
            if(this.closed){return;}
            var key=(definitionId,objectId);
            if(!this.targets.ContainsKey(key)&&this.targets.Count==this.maximumTargets&&this.targets.All(pair=>pair.Value.Waiting.Count>0)){return;}
            var target=this.GetTarget(key);target.Revision=++this.revision;
            waiting=target.Waiting.ToArray();target.Waiting.Clear();this.watches-=waiting.Length;result=new(this.session,target.Revision,true);
        }
        foreach(var source in waiting){source.TrySetResult(result);}
    }
    internal void Close(){
        TaskCompletionSource<QueryTraceWatchResult>[] waiting;
        lock(this.gate){this.closed=true;waiting=this.targets.Values.SelectMany(target=>target.Waiting).ToArray();foreach(var target in this.targets.Values){target.Waiting.Clear();target.Memory.Dispose();}this.targets.Clear();this.watches=0;}
        foreach(var source in waiting){source.TrySetException(new ManagementException("Stopped","トレース通知を停止しています。"));}
    }
}
internal sealed class QueryTraceWatchTicket(Task<QueryTraceWatchResult> pending,Action release,int timeout,Func<QueryTraceWatchResult> unchanged):IDisposable{
    internal async Task<QueryTraceWatchResult> WaitAsync(CancellationToken cancellation){
        try{return await pending.WaitAsync(TimeSpan.FromMilliseconds(timeout),cancellation).ConfigureAwait(false);}
        catch(TimeoutException){return unchanged();}
        finally{this.Dispose();}
    }
    public void Dispose()=>release();
}
