using Seamless.Transactions;
namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    private readonly SemaphoreSlim versionedCollection=new(1,1);
    private readonly HashSet<string> retiringGenerations=new(StringComparer.Ordinal);
    private bool collectingVersioned;
    private Task? versionedCollectionTask;
    private GatewayGenerationStore.CollectionPlan? versionedGenerationPlan,versionedNativePlan;
    private void ScheduleVersionedCollection(){
        lock(this.gate){
            if(this.disposed||this.versionedCollectionTask is{IsCompleted:false}||this.commitCoordinator.Failure.Error is not null){return;}
            if(this.active.Count!=0){return;}
            var state=this.versioned!;
            foreach(var entry in state.Generations.Where(static pair=>pair.Value.Prepared is null&&pair.Value.Binding.IsDisposed).ToArray()){entry.Value.Memory.Dispose();state.Generations.Remove(entry.Key);}
            if(!this.HasPendingVersionedCollection()){return;}
            // 終了した接続のAsyncLocalを回収workerへ持ち込まない。
            if(ExecutionContext.IsFlowSuppressed()){Start();}else{using(ExecutionContext.SuppressFlow()){Start();}}
            void Start()=>this.versionedCollectionTask=Task.Run(()=>{
                try{this.CheckpointVersioned();}
                catch(ObjectDisposedException)when(this.disposed){}
                catch(Exception error){lock(this.gate){this.LastGenerationCollection=new([],[new(this.directory,error.Message)]);}}
            });
        }
    }
    private bool HasPendingVersionedCollection(){
        var state=this.versioned!;
        if(state.Generations.Values.Any(static handle=>handle.Retired||handle.Abandoned)){return true;}
        var current=this.nativePublished.Values.Select(static item=>item.Version.Reference.Id).ToHashSet(StringComparer.Ordinal);
        return state.NativeVersions.Keys.Any(id=>!current.Contains(id));
    }
    internal void CompleteStartupCollection(){
        lock(this.gate){
            if(this.versioned is null||!this.HasPendingVersionedCollection()){return;}
        }
        // 復旧で判明した回収待ちを、最初の要求から起動時へ移す。
        this.CheckpointVersioned();
    }
    private void WaitForVersionedCollection(){Task? pending;lock(this.gate){pending=this.versionedCollectionTask;}pending?.GetAwaiter().GetResult();}
    private void CheckpointVersioned(){
        this.versionedCollection.Wait();
        try{
            GenerationHandle[] generations;GatewayNativeQueryVersion[] natives;
            lock(this.gate){
                this.EnsureReady();var state=this.versioned!;
                generations=state.Generations.Values.Where(static entry=>entry.Retired||entry.Abandoned).ToArray();
                var current=this.nativePublished.Values.Select(static item=>item.Version.Reference.Id).ToHashSet(StringComparer.Ordinal);
                natives=state.NativeVersions.Values.Where(version=>!current.Contains(version.Reference.Id)).ToArray();
                foreach(var handle in generations){this.retiringGenerations.Add(handle.Binding.GenerationId.ToString("N"));}this.collectingVersioned=true;
            }
            // 共通domainが新規rootを待たせ、全scope・workerを排出して保存を終えるまで型を解放しない。
            GatewayVersionedBinding.RetireBatchAsync(generations.Select(static handle=>handle.Binding).ToArray()).GetAwaiter().GetResult();
            if(generations.Length==0){this.commitCoordinator.CheckpointVersionedAsync().AsTask().GetAwaiter().GetResult();}
            var errors=new List<Exception>();
            foreach(var native in natives){try{native.Dispose();lock(this.gate){this.versioned!.NativeVersions.Remove(native.Reference.Id);}}catch(Exception error){errors.Add(error);}}
            this.generationRegistry!.ReleaseObsoletePlans();
            foreach(var handle in generations){
                try{handle.Prepared?.Dispose();handle.Binding.Dispose();handle.Memory.Dispose();lock(this.gate){this.versioned!.Generations.Remove(handle.Binding.GenerationId.ToString("N"));}}
                catch(Exception error){errors.Add(error);}
            }
            // 対象パスは受付停止中に固定済み。再開後に構築された候補を追加列挙しない。
            var database=this.versionedGenerationPlan?.Collect();var nativeFiles=this.versionedNativePlan?.Collect();
            this.LastGenerationCollection=new((database?.Removed??[]).Concat(nativeFiles?.Removed??[]).ToArray(),(database?.Deferred??[]).Concat(nativeFiles?.Deferred??[]).ToArray());
            if(errors.Count!=0){throw new AggregateException("Versioned Gateway collection failed after its common checkpoint.",errors);}
        }finally{lock(this.gate){this.collectingVersioned=false;this.retiringGenerations.Clear();this.versionedGenerationPlan=null;this.versionedNativePlan=null;}this.versionedCollection.Release();}
    }
    private void CaptureVersionedCollectionPlans(IReadOnlyList<GatewayGenerationReference> retained){
        lock(this.gate){
            this.versionedGenerationPlan=this.generationStore!.PlanCollection(retained.Concat(this.published.Values.SelectMany(static item=>Previous(item.State))));
            this.versionedNativePlan=this.nativeStore!.PlanCollection(this.nativePublished.Values.SelectMany(static item=>NativeRetained(item.State)));
        }
        static IEnumerable<GatewayGenerationReference> Previous(PublishedGenerationState state){if(state.Previous is{} previous){yield return previous.Reference;}}
        static IEnumerable<GatewayNativeQueryReference> NativeRetained(NativePublishedState state){yield return state.Current;if(state.Previous is{} previous){yield return previous;}}
    }
}
