using System.Runtime.ExceptionServices;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Seamless.Execution;
namespace Seamless.Linq;

/// <summary>生成器用の有界な分割Lookup。selectorはproducer上、比較/格納はworker専用scope上で行う。</summary>
/// <remarks>allowParallelは比較器と値のcodec処理を並列化してよいと確認した生成計画だけが指定する。</remarks>
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public sealed class PartitionedLookupBuilder<TKey,TElement>:IDisposable{
    private const int MinimumBatchRows=16;
    private const int MaximumBatchRows=128;
    private const int PreferredBatchPayloadBytes=4096;
    private const long WorkerMetadataBytes=8L<<10;
    private const long CoordinatorMetadataBytes=16L<<10;
    private static readonly bool inlineValues=!RuntimeHelpers.IsReferenceOrContainsReferences<TKey>()&&!RuntimeHelpers.IsReferenceOrContainsReferences<TElement>();
    private static readonly int inputSize=Unsafe.SizeOf<Input>();
    private static readonly int preferredBatchRows=GetPreferredBatchRows();
    private readonly ExecutionScope scope;
    private readonly IEqualityComparer<TKey> comparer;
    private readonly CancellationTokenSource stop;
    private readonly ResourceReservation memory;
    private readonly Worker[] workers;
    private readonly Batch?[] pending;
    private readonly int batchRows=MinimumBatchRows;
    private readonly global::Seamless.Execution.Codecs.IAdapter<TKey>? keys;
    private readonly global::Seamless.Execution.Codecs.IAdapter<TElement>? elements;
    private ExceptionDispatchInfo? failure;
    private long ordinal;
    private bool building,complete,disposed;
    public PartitionedLookupBuilder(ExecutionScope scope,IEqualityComparer<TKey>? comparer=null,bool allowParallel=false)
        :this(scope,comparer,allowParallel?scope.MaximumQueryWorkers:1){}

    public PartitionedLookupBuilder(ExecutionScope scope,IEqualityComparer<TKey>? comparer,int maxExecutionSlots){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExecutionSlots);
        this.scope=scope;scope.ThrowIfUnavailable();this.comparer=comparer??EqualityComparer<TKey>.Default;
        if(scope.Codecs.CanEncode<TKey>()){this.keys=scope.Codecs.Get<TKey>();}if(scope.Codecs.CanEncode<TElement>()){this.elements=scope.Codecs.Get<TElement>();}
        var valueCodec=scope.Codecs.CanUseParallelCodec<TKey>()&&scope.Codecs.CanUseParallelCodec<TElement>();
        // 退避で参照を持つ要素を複製しない。文字列キーだけは最初のキーを別途予算付きで保持する。
        var spill=valueCodec&&!RuntimeHelpers.IsReferenceOrContainsReferences<TElement>()&&(!RuntimeHelpers.IsReferenceOrContainsReferences<TKey>()||typeof(TKey)==typeof(string));
        var executionSlots=Math.Min(maxExecutionSlots,Math.Min(scope.MaximumQueryWorkers,CpuScheduler.Shared.Usage.Capacity));
        this.IsParallel=valueCodec&&this.keys is not null&&this.elements is not null&&executionSlots>1;
        var count=1;if(this.IsParallel){count=executionSlots-1;}
        while(count>1&&CoordinatorMetadataBytes+count*(WorkerMetadataBytes+4096L)>scope.Budget.AvailableMemoryBytes){count--;}
        // scope/codec cache、Task、channelの固定費を、batch/Lookupの動的予約と別に確保する。
        this.memory=scope.Budget.ReserveMemory(checked(CoordinatorMetadataBytes+WorkerMetadataBytes*count));this.stop=CancellationTokenSource.CreateLinkedTokenSource(scope.CancellationToken);
        // 各workerは処理中・キュー・producer保留の最大3バッチを持つ。Lookupと他のクエリのために親予算の余裕を残す。
        if(this.IsParallel&&preferredBatchRows>MinimumBatchRows
            &&3L*count*Batch.ReservedBytes(preferredBatchRows)<=scope.Budget.AvailableMemoryBytes/8){
            this.batchRows=preferredBatchRows;
        }
        this.workers=new Worker[count];this.pending=new Batch?[count];
        try{
            var available=scope.Budget.AvailableMemoryBytes;
            var quota=Math.Max(256,available/(count+2));
            // 実予約の階層は維持し、インライン値のLookup本体に限って退避閾値の合計を共有する。
            var retentionPool=inlineValues&&spill&&count>1?scope.Budget.CreateMemoryPool((quota/3)*count):null;
            var maxRetention=retentionPool is null?0:Math.Min(quota*2/3,available/9);
            for(var index=0;index<count;index++){
                var child=scope.CreateChild(memoryLimitBytes:quota);
                try{this.workers[index]=new Worker(child,new ScopedLookup<TKey,(long Ordinal,TElement Value)>(child,this.comparer,spill,retentionPool,maxRetention),this.IsParallel,spill&&typeof(TKey)==typeof(string),this.comparer);}
                catch{child.Dispose();throw;}
            }
            scope.Own(this);
        }catch{this.Dispose();throw;}
    }
    public bool IsParallel{get;}
    public int WorkerCount=>this.workers.Length;
    public long LongCount=>this.ordinal;
    private static int GetPreferredBatchRows(){
        if(inlineValues){
            return Math.Clamp(PreferredBatchPayloadBytes/inputSize,MinimumBatchRows,MaximumBatchRows);
        }else{
            return MinimumBatchRows;
        }
    }
    public void Build(Action<PartitionedLookupBuilder<TKey,TElement>> producer){
        ArgumentNullException.ThrowIfNull(producer);this.Check();if(this.building||this.complete){throw new InvalidOperationException("The lookup can only be built once.");}
        this.building=true;
        try{
            if(this.IsParallel){foreach(var worker in this.workers){worker.Task=Task.Run(()=>this.RunWorker(worker));}}
            CpuScheduler.Shared.Run(()=>{producer(this);this.Complete();return true;},this.stop.Token);
        }catch(OperationCanceledException){this.stop.Cancel();this.CloseWriters();this.WaitWorkers();this.scope.ThrowIfUnavailable();this.failure?.Throw();throw;}
        catch{this.stop.Cancel();this.CloseWriters();this.WaitWorkers();throw;}
        finally{this.building=false;}
    }
    public void Add(TKey key,TElement value){
        this.Check();if(!this.building||this.complete){throw new InvalidOperationException("Add rows from the Build producer.");}
        var ordinal=this.ordinal;this.ordinal=checked(ordinal+1);
        if(!this.IsParallel){using var operation=this.workers[0].Scope.Enter();this.workers[0].Add(key,(ordinal,value));return;}
        var hash=0;if(key is not null){hash=this.comparer.GetHashCode(key);}
        var index=(int)((uint)hash%(uint)this.workers.Length);var batch=this.pending[index]??=new Batch(this.scope,this.batchRows);
        // Input[]の固定予約がインライン値とordinalを含む。参照先graphがある場合だけ追加予約する。
        long bytes=0;if(!inlineValues){bytes=checked(128L+4L*this.keys!.GetEncodedLength(key)+4L*this.elements!.GetEncodedLength(value));}
        batch.Add(new(key,value,ordinal),bytes);
        if(batch.Count==this.batchRows){this.Send(index,batch);this.pending[index]=null;}
    }
    private void Send(int index,Batch batch){
        var write=this.workers[index].Input!.Writer.WriteAsync(batch,this.stop.Token);
        if(!write.IsCompletedSuccessfully){using var cpu=CpuScheduler.SuspendForIo();write.AsTask().GetAwaiter().GetResult();}
    }
    private void Complete(){
        for(var index=0;index<this.workers.Length;index++){
            if(this.pending[index] is{} batch){this.Send(index,batch);this.pending[index]=null;}
        }
        this.CloseWriters();this.WaitWorkers();this.Check();this.complete=true;
    }
    private async Task RunWorker(Worker worker){
        try{
            await foreach(var batch in worker.Input!.Reader.ReadAllAsync(this.stop.Token).ConfigureAwait(false)){
                using(batch){
                    await CpuScheduler.Shared.RunAsync(()=>{
                        using var operation=worker.Scope.Enter();
                        for(var index=0;index<batch.Count;index++){this.Check();var row=batch.Rows[index];worker.Add(row.Key,(row.Ordinal,row.Value));}
                        return true;
                    },this.stop.Token).ConfigureAwait(false);
                }
            }
        }catch(Exception error){Interlocked.CompareExchange(ref this.failure,ExceptionDispatchInfo.Capture(error),null);this.stop.Cancel();}
        finally{while(worker.Input!.Reader.TryRead(out var batch)){batch.Dispose();}}
    }
    private void CloseWriters(){foreach(var worker in this.workers){worker?.Input?.Writer.TryComplete();}}
    private void WaitWorkers(){using var cpu=CpuScheduler.SuspendForIo();Task.WhenAll(this.workers.Where(static worker=>worker?.Task is not null).Select(static worker=>worker.Task!)).GetAwaiter().GetResult();}
    public IEnumerable<IGrouping<TKey,TElement>> Groups()=>this.Groups(preserveOrder:true);
    internal IEnumerable<IGrouping<TKey,TElement>> Groups(bool preserveOrder){
        this.Check();if(!this.complete){throw new InvalidOperationException("Build must finish before reading groups.");}
        return this.ReadGroups(preserveOrder);
    }
    public IEnumerable<TElement> Match(TKey key){
        this.Check();if(!this.complete){throw new InvalidOperationException("Build must finish before probing.");}
        var hash=0;if(key is not null){hash=this.comparer.GetHashCode(key);}
        var index=(int)((uint)hash%(uint)this.workers.Length);
        return this.ReadMatches(this.workers[index],key);
    }
    /// <summary>構築済みLookupから、query内で安定した識別子と借用groupを取得する。</summary>
    public IGrouping<TKey,TElement>? FindGroup(TKey key,out (int Worker,long Group) identity){
        this.Check();
        if(!this.complete){
            throw new InvalidOperationException("Build must finish before probing.");
        }
        var hash=0;
        if(this.workers.Length>1&&key is not null){
            hash=this.comparer.GetHashCode(key);
        }
        var index=(int)((uint)hash%(uint)this.workers.Length);
        var worker=this.workers[index];
        var source=worker.Lookup.FindGroup(key,out var groupId);
        identity=(index,groupId);
        if(source is null){
            return null;
        }
        return new Group(this,source,worker.Keys);
    }
    private IEnumerable<TElement> ReadMatches(Worker worker,TKey key){
        foreach(var row in worker.Lookup.Match(key)){this.Check();yield return row.Value;}
    }
    private IEnumerable<IGrouping<TKey,TElement>> ReadGroups(bool preserveOrder){
        // 1 worker の Groups は既に初出順。merge と順序取得用の追加列挙を省く。
        if(!preserveOrder||this.workers.Length==1){
            foreach(var worker in this.workers){
                foreach(var group in worker.Lookup.Groups()){
                    this.Check();yield return new Group(this,group,worker.Keys);
                }
            }
            yield break;
        }
        var cursors=new IEnumerator<IGrouping<TKey,(long Ordinal,TElement Value)>>[this.workers.Length];
        var heap=new PriorityQueue<(int Worker,IGrouping<TKey,(long Ordinal,TElement Value)> Group),long>();
        try{
            for(var index=0;index<cursors.Length;index++){
                cursors[index]=this.workers[index].Lookup.Groups().GetEnumerator();
                if(cursors[index].MoveNext()){var group=cursors[index].Current;heap.Enqueue((index,group),System.Linq.Enumerable.First(group).Ordinal);}
            }
            while(heap.TryDequeue(out var next,out _)){
                this.Check();yield return new Group(this,next.Group,this.workers[next.Worker].Keys);
                var cursor=cursors[next.Worker];if(cursor.MoveNext()){var group=cursor.Current;heap.Enqueue((next.Worker,group),System.Linq.Enumerable.First(group).Ordinal);}
            }
        }finally{foreach(var cursor in cursors){cursor?.Dispose();}}
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();this.failure?.Throw();}
    public void Dispose(){
        if(this.disposed){return;}this.disposed=true;this.stop.Cancel();this.CloseWriters();
        try{this.WaitWorkers();}
        finally{
            foreach(var batch in this.pending){batch?.Dispose();}
            foreach(var worker in this.workers){worker?.Scope.Dispose();}
            this.stop.Dispose();this.memory.Dispose();this.scope.Release(this);
        }
    }
    private readonly record struct Input(TKey Key,TElement Value,long Ordinal);
    private sealed class Batch:IDisposable{
        internal readonly Input[] Rows;
        internal int Count;
        private readonly ResourceReservation memory;
        internal static long ReservedBytes(int rows)=>64L+rows*(long)inputSize;
        internal Batch(ExecutionScope scope,int rows){this.memory=scope.Budget.ReserveMemory(ReservedBytes(rows));try{this.Rows=new Input[rows];}catch{this.memory.Dispose();throw;}}
        internal void Add(Input row,long bytes){if(bytes!=0){this.memory.Resize(checked(this.memory.Bytes+bytes));}this.Rows[this.Count++]=row;}
        public void Dispose(){Array.Clear(this.Rows);this.memory.Dispose();}
    }
    private sealed class Worker{
        internal readonly ExecutionScope Scope;
        internal readonly ScopedLookup<TKey,(long Ordinal,TElement Value)> Lookup;
        internal readonly Channel<Batch>? Input;
        internal readonly StringKeys? Keys;
        internal Task? Task;
        internal Worker(ExecutionScope scope,ScopedLookup<TKey,(long Ordinal,TElement Value)> lookup,bool parallel,bool preserveStringKeys,IEqualityComparer<TKey> comparer){
            this.Scope=scope;this.Lookup=lookup;if(parallel){this.Input=Channel.CreateBounded<Batch>(new BoundedChannelOptions(1){SingleReader=true,SingleWriter=true,FullMode=BoundedChannelFullMode.Wait});}
            if(preserveStringKeys){this.Keys=new(scope,(IEqualityComparer<string>)(object)comparer);}
        }
        internal void Add(TKey key,(long Ordinal,TElement Value) value){this.Keys?.Record(key);this.Lookup.Add(key,value);}
    }
    private sealed class StringKeys:IDisposable{
        private readonly ExecutionScope scope;
        private readonly ResourceReservation memory;
        private readonly Dictionary<string,string> keys;
        private long bytes=64;
        internal StringKeys(ExecutionScope scope,IEqualityComparer<string> comparer){
            this.scope=scope;this.memory=scope.Budget.ReserveMemory(64);
            try{this.keys=new(comparer);scope.Own(this);}catch{this.memory.Dispose();throw;}
        }
        internal void Record(TKey key){if(key is not string text||this.keys.ContainsKey(text)){return;}var bytes=checked(this.bytes+256L+text.Length*8L);this.memory.Resize(bytes);this.keys.Add(text,text);this.bytes=bytes;}
        internal TKey Restore(TKey key){if(key is not string text){return key;}return(TKey)(object)this.keys[text];}
        public void Dispose(){this.keys.Clear();this.memory.Dispose();this.scope.Release(this);}
    }
    private sealed class Group(PartitionedLookupBuilder<TKey,TElement> owner,IGrouping<TKey,(long Ordinal,TElement Value)> source,StringKeys? keys):IGrouping<TKey,TElement>,IKnownGroupingCount,ICollection<TElement>{
        long IKnownGroupingCount.Count{get{owner.Check();if(source is IKnownGroupingCount counted){return counted.Count;}return -1;}}
        int ICollection<TElement>.Count=>checked((int)((IKnownGroupingCount)this).Count);
        bool ICollection<TElement>.IsReadOnly=>true;
        bool ICollection<TElement>.Contains(TElement value){
            foreach(var element in this){
                if(EqualityComparer<TElement>.Default.Equals(element,value)){
                    return true;
                }
            }
            return false;
        }
        void ICollection<TElement>.CopyTo(TElement[] array,int index){
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            if(index>array.Length-((ICollection<TElement>)this).Count){
                throw new ArgumentException("The destination is too short.",nameof(array));
            }
            foreach(var element in this){
                array[index++]=element;
            }
        }
        void ICollection<TElement>.Add(TElement value)=>throw new NotSupportedException();
        bool ICollection<TElement>.Remove(TElement value)=>throw new NotSupportedException();
        void ICollection<TElement>.Clear()=>throw new NotSupportedException();
        public TKey Key{get{owner.Check();if(keys is not null){return keys.Restore(source.Key);}return source.Key;}}
        public IEnumerator<TElement> GetEnumerator()=>this.Read().GetEnumerator();
        private IEnumerable<TElement> Read(){owner.Check();foreach(var row in source){owner.Check();yield return row.Value;}}
        IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
