using System.Text.Json;
using Seamless.Execution;
namespace Seamless.Collections.Tsavorite;
/// <summary>
/// 共通ストアから決めた、Tsavorite データ本体 1 つ分の配置(デバイス・大きさ・予算の予約)。
/// データ本体が破棄されるときに、デバイスと予約も一緒に解放する。
/// </summary>
internal sealed class TsavoriteLayout:IDisposable{
    /// <summary>集合演算の作業リストを置くフォルダ名(データ本体のフォルダの中)。</summary>
    internal const string WorkListDirectoryName="worklists";
    private const long MinimumIndexSize=64;
    private TsavoriteLayout(global::Seamless.Execution.Store owner,string name,StoreKind kind,long indexSize,TsavoriteTuning tuning,long logMemorySize,IDevice logDevice,DeferredTruncationDevice? deferredLogDevice,IDevice objectLogDevice,string? checkpointDirectory,ResourceReservation memory,ResourceReservation? disk){
        this.Owner=owner;
        this.Name=name;
        this.Kind=kind;
        this.IndexSize=indexSize;
        this.PageSize=tuning.PageSize;
        this.SegmentSize=tuning.SegmentSize;
        this.MutableFraction=tuning.MutableFraction;
        this.Growth=(tuning.Growth??owner.GetGrowth(name)).CreateController();
        this.LogMemorySize=logMemorySize;
        this.LogDevice=logDevice;
        this.DeferredLogDevice=deferredLogDevice;
        this.ObjectLogDevice=objectLogDevice;
        this.CheckpointDirectory=checkpointDirectory;
        this.Memory=memory;
        this.Disk=disk;
    }
    /// <summary>このデータ本体を子資源として登録している共通ストア。</summary>
    public global::Seamless.Execution.Store Owner{get;}
    public string Name{get;}
    public StoreKind Kind{get;}
    public long IndexSize{get;}
    public long PageSize{get;}
    public long SegmentSize{get;}
    public double MutableFraction{get;}
    public global::Seamless.Execution.Growth.Controller Growth{get;}
    public long LogMemorySize{get;}
    public IDevice LogDevice{get;}
    /// <summary>ログのデバイスを包んで、ファイルの切り捨てを保存が確定するまで保留する。メモリ種では null。<see cref="LogDevice"/> はこれ自身。</summary>
    public DeferredTruncationDevice? DeferredLogDevice{get;}
    public IDevice ObjectLogDevice{get;}
    public string? CheckpointDirectory{get;}
    /// <summary>メモリの予約。索引 + メモリ上のログ。</summary>
    public ResourceReservation Memory{get;}
    /// <summary>ディスクの予約。ログ全体。メモリ種では null。</summary>
    public ResourceReservation? Disk{get;}
    public static TsavoriteLayout Resolve(global::Seamless.Execution.Store store,string name,long expectedCount,TsavoriteTuning tuning,string elementType="*"){
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedCount);
        ArgumentNullException.ThrowIfNull(tuning);
        var profile=store.ResolveProfile(name,"Tsavorite",elementType,expectedCount,tuning.Overrides,tuning.Growth);
        tuning=tuning with{IndexSize=profile.Settings.IndexBytes,PageSize=profile.Settings.PageBytes!.Value,SegmentSize=profile.Settings.SegmentBytes!.Value,LogMemorySize=profile.Settings.LogMemoryBytes,MutableFraction=profile.Settings.MutableFraction!.Value};
        tuning.Validate(store.Kind);
        TsavoriteSavedState? saved=null;
        if(store.TryGetSavedState(name,out var state)){
            saved=TsavoriteSavedState.Parse(state);
        }
        long indexSize;
        if(tuning.IndexSize is long explicitIndexSize){
            if((tuning.Growth??store.GetGrowth(name)).Descriptor.ID=="fixed"&&saved is not null&&saved.IndexSize!=explicitIndexSize){
                throw new InvalidOperationException("The saved index size differs from the explicitly configured fixed index size.");
            }
            indexSize=explicitIndexSize;
        }else{
            indexSize=MinimumIndexSize;
        }
        if(saved is not null&&saved.IndexSize>indexSize){
            indexSize=saved.IndexSize;
        }
        long logMemorySize;
        if(tuning.LogMemorySize is long explicitLogMemorySize){
            logMemorySize=explicitLogMemorySize;
        }else{
            logMemorySize=8192;
        }
        if(logMemorySize<2*tuning.PageSize){
            throw new ResourceLimitException(ResourceLimitKind.Memory,checked(indexSize+2*tuning.PageSize),store.Limits.MemoryBytes);
        }
        var memory=store.Budget.ReserveMemory(indexSize);
        ResourceReservation? disk=null;
        IDevice? logDevice=null;
        DeferredTruncationDevice? deferredLogDevice=null;
        IDevice? objectLogDevice=null;
        try{
            string? checkpointDirectory=null;
            if(store.Kind==StoreKind.Memory){
                logDevice=new NullDevice();
                objectLogDevice=new NullDevice();
            }else{
                disk=store.Budget.ReserveTemporaryStorage(0);
                var directory=store.GetComponentDirectory(name);
                if(saved is null){
                    // 保存されていない状態は確定していないので、前回の書きかけは捨てて新しく始める。
                    foreach(var child in Directory.EnumerateDirectories(directory)){
                        Directory.Delete(child,true);
                    }
                    foreach(var file in Directory.EnumerateFiles(directory)){
                        File.Delete(file);
                    }
                }
                // 異常終了で残った作業リストは中身に意味が無いので、開くたびに消す。
                var workLists=Path.Combine(directory,WorkListDirectoryName);
                if(Directory.Exists(workLists)){
                    Directory.Delete(workLists,true);
                }
                checkpointDirectory=Path.Combine(directory,"checkpoints");
                // 保存した世代のファイル以外のログ(保存されなかった新しい世代・確定後に消し残した古い世代)は、記録に無いので消す。
                var generation=saved?.Generation??0;
                if(saved is not null){
                    DeleteOtherGenerations(directory,generation);
                }
                // SpanByte のレコードは値をログに直接持つのでオブジェクトログは使わない。切り捨てを保留するのはログのデバイスだけ。
                if(tuning.Storage is StorageTuning storage){
                    deferredLogDevice=new(g=>TsavoriteStorageDevices.Create(Path.Combine(directory,LogName(g)),storage),generation);
                    logDevice=deferredLogDevice;
                    objectLogDevice=TsavoriteStorageDevices.Create(Path.Combine(directory,"obj"),storage);
                }else{
                    deferredLogDevice=new(g=>global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(directory,LogName(g))),generation);
                    logDevice=deferredLogDevice;
                    objectLogDevice=global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(directory,"obj"));
                }
            }
            return new(store,name,store.Kind,indexSize,tuning,logMemorySize,logDevice,deferredLogDevice,objectLogDevice,checkpointDirectory,memory,disk);
        }catch{
            objectLogDevice?.Dispose();
            logDevice?.Dispose();
            disk?.Dispose();
            memory.Dispose();
            throw;
        }
    }
    /// <summary>ログのファイルの名前。世代 0 は従来の hlog、全削除のたびに世代が上がる(gen1-hlog、gen2-hlog…)。名前が hlog で始まらないのは、Tsavorite が接頭辞で別の世代のファイルを拾わないようにするため。</summary>
    internal static string LogName(int generation){
        if(generation==0){
            return "hlog";
        }
        return "gen"+generation+"-hlog";
    }
    private static void DeleteOtherGenerations(string directory,int keep){
        foreach(var file in Directory.EnumerateFiles(directory)){
            var name=Path.GetFileName(file);
            var dot=name.LastIndexOf('.');
            if(dot<0||name.AsSpan(dot+1).IndexOfAnyExceptInRange('0','9')>=0){
                continue;
            }
            var stem=name[..dot];
            int generation;
            if(stem=="hlog"){
                generation=0;
            }else if(!stem.StartsWith("gen",StringComparison.Ordinal)||!stem.EndsWith("-hlog",StringComparison.Ordinal)||!int.TryParse(stem.AsSpan(3,stem.Length-8),out generation)){
                continue;
            }
            if(generation!=keep){
                File.Delete(file);
            }
        }
    }
    /// <summary>予想件数から索引の大きさを決める。1 バケット(64 バイト)あたり平均 4 件を目安にする。</summary>
    internal static long GetIndexSize(long expectedCount){
        var buckets=Math.Max(1L,(expectedCount+3)/4);
        var size=CeilingPowerOfTwo(checked(buckets*64));
        return Math.Max(MinimumIndexSize,size);
    }
    private static long CeilingPowerOfTwo(long value){
        long size=1;
        while(size<value){
            size<<=1;
        }
        return size;
    }
    private static long FloorPowerOfTwo(long value){
        if(value<=0){
            return 0;
        }
        return 1L<<(63-(int)long.LeadingZeroCount(value));
    }
    public void Dispose(){
        List<Exception>? errors=null;
        foreach(var resource in new IDisposable?[]{this.ObjectLogDevice,this.LogDevice,this.Disk,this.Memory}){
            try{
                resource?.Dispose();
            }catch(Exception error){
                (errors??=[]).Add(error);
            }
        }
        if(errors is not null){
            throw new AggregateException("Tsavorite layout cleanup failed.",errors);
        }
    }
}
/// <summary>共通ストアの保存に記録する Tsavorite データ本体の状態。Count は保存時に件数が分かっていたときだけ入る。</summary>
internal sealed record TsavoriteSavedState(Guid Token,long IndexSize,long? Count,int Generation=0){
    public static TsavoriteSavedState Parse(string state)=>JsonSerializer.Deserialize<TsavoriteSavedState>(state)??throw new InvalidDataException("Tsavorite saved state is empty.");
    public string Serialize()=>JsonSerializer.Serialize(this);
}
