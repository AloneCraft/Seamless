using System.Security.Cryptography;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Native;

/// <summary>一つの公開DB表現の準備済み実行物。世代と権限を実行前に確認する。</summary>
public sealed class NativeLambdaDatabase<TDatabase>:IDisposable{
    private readonly object gate=new();
    private readonly NativeLambdaCompiler<TDatabase> compiler;
    private readonly Dictionary<string,Entry> cached=new(StringComparer.Ordinal);
    private readonly Dictionary<ulong,Entry> handles=[];
    private readonly LinkedList<Entry> recent=[];
    private readonly HashSet<Entry> retired=[];
    private ulong nextHandle;
    private bool disposed;
    private int maximumPreparedQueries=256;
    private int activeLeases;
    private int evictionCount;
    private readonly Guid epoch=Guid.NewGuid();
    public int CompilationCount{get;private set;}
    public int ExecutionCount{get;private set;}
    public int MaximumPreparedQueries{get=>this.maximumPreparedQueries;init{if(value<=0){throw new ArgumentOutOfRangeException(nameof(MaximumPreparedQueries));}this.maximumPreparedQueries=value;}}
    public NativeLambdaCacheStatistics CacheStatistics{get{lock(this.gate){return new(this.cached.Count,this.retired.Count,this.activeLeases,this.CompilationCount,this.evictionCount);}}}
    public NativeLambdaDatabase(NativeLambdaSchema<TDatabase> schema)=>this.compiler=new(schema);

    public NativePreparedLambda Prepare(NativeLambdaProgram program,string generation){
        ArgumentException.ThrowIfNullOrWhiteSpace(generation);
        program=NativeLambdaProgram.Decode(program.Encode());
        var bytes=program.Encode();var key=generation+"|"+Convert.ToHexString(SHA256.HashData(bytes));
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.cached.TryGetValue(key,out var existing)){this.Authorize(existing);this.Touch(existing);return new(this.epoch,existing.Handle);}
            if(this.nextHandle==ulong.MaxValue||this.cached.Count+this.retired.Count>=this.MaximumPreparedQueries&&this.OldestIdle() is null){throw Full();}
            var compiled=this.compiler.Compile(program);
            try{
                while(this.cached.Count+this.retired.Count>=this.MaximumPreparedQueries){var victim=this.OldestIdle()??throw Full();this.Retire(victim);this.evictionCount++;}
                var entry=new Entry(++this.nextHandle,key,generation,compiled);this.cached.Add(key,entry);this.handles.Add(entry.Handle,entry);this.recent.AddLast(entry.Recent);this.CompilationCount++;
                return new(this.epoch,entry.Handle);
            }catch{compiled.Dispose();throw;}
        }
    }

    internal (INativeQueryModule Module,NativeQueryDefinition Definition) Resolve(NativePreparedLambda handle,string database,string generation){
        lock(this.gate){
            var entry=this.Find(handle,generation);this.Authorize(entry);
            var definition=new NativeQueryDefinition(entry.Handle.ToString(global::System.Globalization.CultureInfo.InvariantCulture),database,generation,"prepared","prepared",entry.Compiled.IsReadOnly){ArgumentsContract=entry.Compiled.ArgumentsContract,ResultContract=entry.Compiled.ResultContract};
            return(new Module(this,entry),definition);
        }
    }

    public INativeLambdaTransport Local(DatabaseContainer database,Func<string> generation)=>new LocalTransport(this,database,generation);

    /// <summary>新しい実行から失効させる。実行中のコードは最後のリース解放まで保持する。</summary>
    public void InvalidatePreparedQueries(string? generation=null){
        lock(this.gate){ObjectDisposedException.ThrowIf(this.disposed,this);foreach(var entry in this.cached.Values.ToArray()){if(generation is null||entry.Generation==generation){this.Retire(entry);}}}
    }

    private Entry Find(NativePreparedLambda handle,string generation){
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(handle.Epoch!=this.epoch||!this.handles.TryGetValue(handle.Handle,out var entry)||entry.Generation!=generation){throw new NativeLambdaException("Stale","The prepared lambda has expired before execution.",true);}
        return entry;
    }
    private void Authorize(Entry entry){foreach(var demand in entry.Compiled.Access){if(!this.compiler.Schema.Authorize(demand)){throw new NativeLambdaException("Permission","The lambda operation is no longer permitted.",true);}}}
    private Entry? OldestIdle(){for(var node=this.recent.First;node is not null;node=node.Next){if(node.Value.Leases==0){return node.Value;}}return null;}
    private void Touch(Entry entry){this.recent.Remove(entry.Recent);this.recent.AddLast(entry.Recent);}
    private void Retire(Entry entry){
        if(entry.Retired){return;}entry.Retired=true;this.cached.Remove(entry.Key);this.handles.Remove(entry.Handle);this.recent.Remove(entry.Recent);
        if(entry.Leases==0){entry.Compiled.Dispose();}else{this.retired.Add(entry);}
    }
    private Lease Acquire(Entry entry){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(entry.Retired||!this.handles.ContainsKey(entry.Handle)){throw new NativeLambdaException("Stale","The prepared lambda was reclaimed before execution.",true);}
            this.Authorize(entry);this.Touch(entry);entry.Leases++;this.activeLeases++;return new(this,entry);
        }
    }
    private void Release(Entry entry){
        lock(this.gate){entry.Leases--;this.activeLeases--;if(entry.Retired&&entry.Leases==0){this.retired.Remove(entry);entry.Compiled.Dispose();}}
    }
    private byte[] Invoke(Entry entry,DatabaseContainer database,byte[] arguments,CancellationToken cancellation){
        cancellation.ThrowIfCancellationRequested();entry.Compiled.ValidateArguments(arguments);lock(this.gate){this.ExecutionCount++;}
        cancellation.ThrowIfCancellationRequested();return entry.Compiled.Invoke(this.compiler.Schema.Bind(database),arguments,cancellation);
    }

    private static NativeLambdaException Full()=>new("Capacity","All prepared lambda slots are in use.",true);
    private sealed class Entry{
        public Entry(ulong handle,string key,string generation,NativeCompiledLambda compiled){this.Handle=handle;this.Key=key;this.Generation=generation;this.Compiled=compiled;this.Recent=new(this);}
        public ulong Handle{get;}
        public string Key{get;}
        public string Generation{get;}
        public NativeCompiledLambda Compiled{get;}
        public LinkedListNode<Entry> Recent{get;}
        public int Leases{get;set;}
        public bool Retired{get;set;}
    }
    private sealed class Lease(NativeLambdaDatabase<TDatabase> owner,Entry entry):IDisposable{
        private NativeLambdaDatabase<TDatabase>? owner=owner;
        public void Dispose()=>Interlocked.Exchange(ref this.owner,null)?.Release(entry);
    }
    private sealed class Module(NativeLambdaDatabase<TDatabase> owner,Entry entry):INativeQueryModule{
        public IEnumerable<byte[]> Execute(string queryId,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
            context.CancellationToken.ThrowIfCancellationRequested();using var lease=owner.Acquire(entry);
            yield return owner.Invoke(entry,(DatabaseContainer)context.Database,arguments.ToArray(),context.CancellationToken);
        }
    }
    private sealed class LocalTransport(NativeLambdaDatabase<TDatabase> owner,DatabaseContainer database,Func<string> generation):INativeLambdaTransport{
        public Task<NativePreparedLambda> PrepareAsync(NativeLambdaProgram program,CancellationToken cancellationToken){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(owner.Prepare(program,generation()));}
        public Task<byte[]> ExecuteAsync(NativePreparedLambda query,ReadOnlyMemory<byte> arguments,CancellationToken cancellationToken){
            cancellationToken.ThrowIfCancellationRequested();Entry entry;Lease lease;lock(owner.gate){entry=owner.Find(query,generation());lease=owner.Acquire(entry);}
            using var execution=lease;
            IDisposable? read=null;DatabaseTransaction? transaction=null;
            try{
                if(entry.Compiled.IsReadOnly){read=database.BeginReadSession(cancellationToken);}else{transaction=database.BeginTransaction(cancellationToken);}
                var result=owner.Invoke(entry,database,arguments.ToArray(),cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();transaction?.Commit();transaction?.Dispose();transaction=null;
                return Task.FromResult(result);
            }finally{read?.Dispose();transaction?.Dispose();}
        }
    }
    public void Dispose(){lock(this.gate){if(this.disposed){return;}this.disposed=true;foreach(var entry in this.cached.Values.ToArray()){this.Retire(entry);}}}
}
