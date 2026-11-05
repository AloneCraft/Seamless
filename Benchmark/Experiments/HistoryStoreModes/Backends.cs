using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Store=Seamless.Execution.Store;
namespace HistoryStoreModes;

internal readonly record struct Observation(long Version,bool Deleted,byte[] Row);
[StructLayout(LayoutKind.Sequential,Pack=1)]internal readonly record struct WideKey(long Id,long A,long B,long C);
internal sealed record Settings(bool Persistent,long IndexBytes,int LogBytes,int RowBytes,int Keys,int Versions,bool Buffered=false){
    internal TsavoriteTuning Tuning{
        get{
            var tuning=new TsavoriteTuning{IndexSize=this.IndexBytes,PageSize=4096,SegmentSize=1<<20,LogMemorySize=this.LogBytes};
            if(this.Persistent&&this.Buffered){tuning=tuning with{Storage=new Seamless.Collections.StorageTuning(FileAccess.ReadWrite,Seamless.Collections.StorageAccessPattern.Random,Seamless.Collections.StorageBufferingMode.Buffered)};}
            return tuning;
        }
    }
}
internal interface IBackend:IDisposable{
    long MemoryBytes{get;}long StorageBytes{get;}
    void Append(long id,long version,bool deleted,ReadOnlySpan<byte> row);
    Observation? Read(long id,long version);
    void Save();void Evict();void Reclaim();object Metrics();
}
internal abstract class Backend<TKey>:IBackend where TKey:unmanaged{
    protected readonly Store owner;
    protected readonly Settings settings;
    protected bool faulted;
    protected Backend(string root,Settings settings){
        this.settings=settings;var limits=new StoreLimits{MemoryBytes=64L<<20,DiskBytes=256L<<20};var options=new StoreOptions{Growth=new global::Seamless.Execution.Growth.Policy<global::Seamless.Execution.Growth.Fixed>()};
        if(settings.Persistent){this.owner=Store.Open(root,limits,options);}else{this.owner=Store.CreateMemory(limits,options);}
    }
    protected void Check(){if(this.faulted){throw new InvalidOperationException("Prototype owner is faulted; reopen its checkpoint.");}}
    public long MemoryBytes=>this.owner.Budget.MemoryBytes;
    public long StorageBytes=>this.owner.Budget.TemporaryStorageBytes;
    protected static TKey Key(long id){
        Span<byte> bytes=stackalloc byte[Unsafe.SizeOf<TKey>()];bytes.Clear();BinaryPrimitives.WriteInt64LittleEndian(bytes,id);
        if(bytes.Length==32){BinaryPrimitives.WriteInt64LittleEndian(bytes[8..],123456789);BinaryPrimitives.WriteInt64LittleEndian(bytes[16..],987654321);BinaryPrimitives.WriteInt64LittleEndian(bytes[24..],id^0x123456789AB);}
        return MemoryMarshal.Read<TKey>(bytes);
    }
    public abstract void Append(long id,long version,bool deleted,ReadOnlySpan<byte> row);
    public abstract Observation? Read(long id,long version);
    public virtual void Save(){this.Check();if(this.settings.Persistent){this.owner.Save();}}
    public abstract void Evict();public abstract void Reclaim();public abstract object Metrics();
    public abstract void Dispose();
    protected object Budget()=>new{this.owner.Budget.MemoryBytes,this.owner.Budget.PeakMemoryBytes,this.owner.Budget.TemporaryStorageBytes,this.owner.Budget.PeakTemporaryStorageBytes};
}
internal sealed class NativeHistory<TKey>:Backend<TKey> where TKey:unmanaged{
    private readonly HistoryStore history;
    private HistoryAccessor access;
    internal NativeHistory(string root,Settings settings):base(root,settings){
        try{this.history=this.owner.CreateTsavoriteHistory("history",settings.Tuning,"prototype|key"+Unsafe.SizeOf<TKey>()+"|row"+settings.RowBytes);this.access=this.history.Create();}
        catch{this.owner.Dispose();throw;}
    }
    public override void Append(long id,long version,bool deleted,ReadOnlySpan<byte> row){this.Check();var key=Key(id);this.access.AppendCommittedVersion(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref key,1)),row,version,deleted);}
    public override Observation? Read(long id,long version){
        this.Check();var key=Key(id);var record=this.access.ReadVisibleRecord(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref key,1)),version);
        if(record is{} value){return new(value.CommitVersion,value.Deleted,value.Row);}return null;
    }
    public override void Evict(){this.Check();if(this.settings.Persistent){this.history.Store.Log.FlushAndEvict(wait:true);}}
    public override void Reclaim(){
        this.Check();this.access.Dispose();
        try{this.history.Reclaim();if(this.settings.Persistent){this.owner.Save();}this.access=this.history.Create();}catch{this.faulted=true;throw;}
    }
    public override object Metrics()=>new{Budget=this.Budget(),Native=this.history.GetStatistics(),NativeIndexPayloadBytes=this.history.Store.IndexSize*64+this.history.Store.IndexOverflowSizeBytes,NativeLogAllocatedBytes=this.history.Store.Log.MemorySizeBytes};
    public override void Dispose(){try{this.access?.Dispose();}finally{this.owner.Dispose();}}
}
internal sealed class DedicatedHistory<TKey>:Backend<TKey> where TKey:unmanaged{
    private readonly DictionaryStore<TKey,long> heads;
    private Seamless.Collections.Tsavorite.IDictionary<TKey,long> access;
    private readonly ArenaComponent arena;
    internal DedicatedHistory(string root,Settings settings):base(root,settings){
        try{
            string? arenaDirectory=null;if(settings.Persistent){arenaDirectory=Path.Combine(root,"arena");}
            var candidate=new ArenaComponent(this.owner.Budget,arenaDirectory,Unsafe.SizeOf<TKey>(),settings.RowBytes,settings.LogBytes,8L<<20);
            try{this.arena=this.owner.Register(candidate);}catch{candidate.Dispose();throw;}
            this.heads=this.owner.CreateTsavorite<TKey,long>("head",settings.Tuning,settings.Keys);this.access=this.heads.Create();
        }catch{this.owner.Dispose();throw;}
    }
    public override void Append(long id,long version,bool deleted,ReadOnlySpan<byte> row){
        this.Check();var key=Key(id);var previous=-1L;if(this.access.TryGetValue(key,out var found)){previous=found;}
        var offset=this.arena.Current.Append(id,version,previous,deleted,row);try{this.access[key]=offset;}catch{this.faulted=true;throw;}
    }
    public override Observation? Read(long id,long version){
        this.Check();if(!this.access.TryGetValue(Key(id),out var offset)){return null;}
        while(offset>=0){var row=this.arena.Current.Read(offset,id,out var found,out var previous,out var deleted);if(found<=version){return new(found,deleted,row.ToArray());}offset=previous;}
        return null;
    }
    internal ArenaAddress HeadAddress(long id)=>this.arena.Address(this.access[Key(id)]);
    internal void ValidateAddress(ArenaAddress address)=>this.arena.Validate(address);
    public override void Evict(){this.Check();if(this.settings.Persistent){this.heads.Store.Log.FlushAndEvict(wait:true);this.arena.Current.Evict();}}
    public override void Reclaim(){
        this.Check();var candidate=this.arena.CreateCandidate();var switched=false;
        try{
            // 静止した実験ownerのみ。全行を配列にせず、一行ずつ新世代へ移しheadを更新する。
            for(var id=0;id<this.settings.Keys;id++){
                var key=Key(id);if(!this.access.TryGetValue(key,out var previous)){continue;}
                var row=this.arena.Current.Read(previous,id,out var version,out _,out var deleted);
                if(deleted){this.access.Remove(key);}else{this.access[key]=candidate.Append(id,version,-1,false,row);}
            }
            this.arena.Switch(candidate);switched=true;this.access.Dispose();this.heads.Compact();
            if(this.settings.Persistent){this.owner.Save();}else{this.arena.SaveCommitted();}
            this.access=this.heads.Create();
        }catch{this.faulted=true;if(!switched){candidate.Dispose();}throw;}
    }
    public override object Metrics()=>new{Budget=this.Budget(),Native=this.heads.GetStatistics(),NativeIndexPayloadBytes=this.heads.Store.IndexSize*64+this.heads.Store.IndexOverflowSizeBytes,NativeLogAllocatedBytes=this.heads.Store.Log.MemorySizeBytes,
        Arena=new{this.arena.Current.Tail,this.arena.Current.BufferBytes,this.arena.Current.Generation,this.arena.Current.ReadCalls,this.arena.Current.ReadBytes,this.arena.Current.WriteCalls,this.arena.Current.WriteBytes}};
    public override void Dispose(){try{this.access?.Dispose();}finally{this.owner.Dispose();}}
}
