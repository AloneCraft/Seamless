using System.Text;
using System.Text.Json;
namespace Seamless.Execution;
/// <summary>
/// 保存場所・寿命・メモリとディスクの上限を持つ共通ストア。
/// エンジン(Tsavorite / Persisted)は拡張メソッドでこのストアからデータ本体を作り、<see cref="Register{T}"/> で子資源として登録する。
/// 永続ストアは <see cref="Save"/> を呼んだ時点だけが確定し、破棄しただけでは確定しない。
/// </summary>
public sealed class Store:IDisposable{
    internal const string FormatName="seamless-store-v1";
    private const string ManifestFileName="store.json";
    private const string LockFileName="store.lock";
    private const string DataDirectoryName="data";
    private readonly object gate=new();
    private readonly List<IStoreComponent> components=[];
    private readonly List<Checkpoint> checkpoints;
    private readonly FileStream? lockStream;
    private readonly string? directoryPath;
    private Checkpoint? current;
    private bool disposed;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,ProfileResolution> profiles=new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,Growth.Event> growthDecisions=new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentQueue<Growth.Event> growthEvents=new();
    private Store(StoreKind kind,string? directoryPath,StoreLimits? limits,FileStream? lockStream,List<Checkpoint> checkpoints,StoreOptions? options=null){
        this.Kind=kind;
        this.directoryPath=directoryPath;
        this.Limits=limits??new();
        this.Options=options??new();
        this.Budget=new(this.Limits.MemoryBytes,this.Limits.DiskBytes);
        this.lockStream=lockStream;
        this.checkpoints=checkpoints;
        if(checkpoints.Count>0){
            this.current=checkpoints[^1];
        }
    }
    /// <summary>一時ストアを作る。フォルダは <paramref name="temporaryDirectory"/>(省略時は %TEMP%/Seamless/Store)の下に作り、破棄で全削除する。</summary>
    public static Store CreateTemporary(StoreLimits? limits=null,string? temporaryDirectory=null,StoreOptions? options=null){
        string root;
        if(string.IsNullOrWhiteSpace(temporaryDirectory)){
            root=Path.Combine(Path.GetTempPath(),"Seamless","Store");
        }else{
            root=temporaryDirectory;
        }
        var id="store-"+Guid.NewGuid().ToString("N");
        var directory=Path.Combine(Path.GetFullPath(root),id);
        Directory.CreateDirectory(directory);
        FileStream? lease=null;
        try{
            lease=new(Path.Combine(directory,"owner.lock"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
            File.WriteAllText(Path.Combine(directory,"owner.json"),JsonSerializer.Serialize(new{Format=FormatName,Id=id,ProcessId=Environment.ProcessId,CreatedAt=DateTimeOffset.UtcNow}));
            return new(StoreKind.Temporary,directory,limits,lease,[],options);
        }catch{
            lease?.Dispose();
            Directory.Delete(directory,true);
            throw;
        }
    }
    /// <summary>永続ストアを開く。フォルダが無ければ作る。開いている間は排他ロックを持ち、同じフォルダを二重に開くと <see cref="IOException"/>。</summary>
    public static Store Open(string directory,StoreLimits? limits=null,StoreOptions? options=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var fullPath=Path.GetFullPath(directory);
        Directory.CreateDirectory(fullPath);
        var lease=new FileStream(Path.Combine(fullPath,LockFileName),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try{
            return new(StoreKind.Persistent,fullPath,limits,lease,ReadManifest(fullPath),options);
        }catch{
            lease.Dispose();
            throw;
        }
    }
    /// <summary>ディスクを使わないストアを作る。保存はできない。</summary>
    public static Store CreateMemory(StoreLimits? limits=null,StoreOptions? options=null)=>new(StoreKind.Memory,null,limits,null,[],options);
    public StoreOptions Options{get;}
    public IReadOnlyDictionary<string,ProfileResolution> ResolvedProfiles=>new Dictionary<string,ProfileResolution>(this.profiles,StringComparer.Ordinal);
    public IReadOnlyList<Growth.Event> GrowthEvents=>this.growthEvents.ToArray();
    public IReadOnlyDictionary<string,Growth.Event> GrowthDecisions=>new Dictionary<string,Growth.Event>(this.growthDecisions,StringComparer.Ordinal);
    public ProfileResolution ResolveProfile(string component,string backend,string elementType,long expectedCount=0,ProfileSettings? explicitSettings=null,Growth.Policy? explicitGrowth=null){
        ValidateName(component);
        var resolution=ProfileResolver.Resolve(this.Options.Profile,
            new(backend,this.Kind,elementType,expectedCount,this.Options.Workload,this.Options.Environment),
            this.Limits,explicitSettings,this.Options.Profiles,explicitGrowth??this.Options.ExplicitGrowth);
        this.profiles[component]=resolution;
        return resolution;
    }
    public Growth.Policy GetGrowth(string component){
        if(this.Options.ExplicitGrowth is not null){return this.Options.ExplicitGrowth;}
        if(this.profiles.TryGetValue(component,out var profile)&&profile.Growth is not null){
            return Growth.Policy.FromDescriptor(profile.Growth)??throw new InvalidOperationException("The resolved growth strategy is unavailable.");
        }
        return this.Options.Growth;
    }
    public ProfileSettings GetCurrentSettings(string component){
        var settings=this.profiles[component].Settings;
        foreach(var entry in this.growthEvents.Where(entry=>entry.Component==component||entry.Component==component+".log")){
            if(entry.Context.Resource==Growth.Resource.Index){settings=settings with{IndexBytes=entry.Decision.TargetBytes};}
            if(entry.Context.Resource==Growth.Resource.LogMemory){settings=settings with{LogMemoryBytes=entry.Decision.TargetBytes};}
        }
        return settings;
    }
    public void RecordGrowthDecision(string component,Growth.Context context,Growth.Decision decision,double? elapsedNanoseconds=null){
        var entry=new Growth.Event(component,DateTimeOffset.UtcNow,context,decision,elapsedNanoseconds);
        this.growthDecisions[component]=entry;
        if(decision.Status==Growth.Status.Grow){this.growthEvents.Enqueue(entry);}
    }
    public StoreKind Kind{get;}
    /// <summary>ストアのフォルダ。メモリストアでは null。</summary>
    public string? DirectoryPath=>this.directoryPath;
    public StoreLimits Limits{get;}
    /// <summary>子資源がメモリとディスクを予約する予算。上限はストアの上限と同じ。</summary>
    public ResourceBudget Budget{get;}
    /// <summary>直近に保存または復元した保存番号。開き直した直後は最新の保存番号。</summary>
    public Guid? CurrentCheckpoint{
        get{
            lock(this.gate){
                return this.current?.Token;
            }
        }
    }
    public IReadOnlyList<Guid> Checkpoints{
        get{
            lock(this.gate){
                return [..this.checkpoints.Select(static checkpoint=>checkpoint.Token)];
            }
        }
    }
    /// <summary>子資源が使うフォルダを返す(無ければ作る)。メモリストアでは <see cref="InvalidOperationException"/>。</summary>
    public string GetComponentDirectory(string name){
        ValidateName(name);
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.directoryPath is null){
                throw new InvalidOperationException("A memory store has no directory.");
            }
            var path=Path.Combine(this.directoryPath,DataDirectoryName,name);
            Directory.CreateDirectory(path);
            return path;
        }
    }
    /// <summary>
    /// 子資源を登録する。現在の保存番号にその名前の状態があれば、登録時に <see cref="IStoreComponent.Load"/> へ渡す。
    /// 登録した子資源はストアの破棄で破棄される。
    /// </summary>
    public T Register<T>(T component) where T:IStoreComponent{
        ArgumentNullException.ThrowIfNull(component);
        ValidateName(component.Name);
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            foreach(var registered in this.components){
                if(string.Equals(registered.Name,component.Name,StringComparison.Ordinal)){
                    throw new ArgumentException($"A component named '{component.Name}' is already registered.",nameof(component));
                }
            }
            this.components.Add(component);
            try{
                if(this.current is not null&&this.current.Components.TryGetValue(component.Name,out var state)){
                    component.Load(state);
                }
            }catch{
                this.components.Remove(component);
                throw;
            }
            return component;
        }
    }
    /// <summary>
    /// 子資源の登録を外す(破棄はしない)。子資源は自分が破棄されるときにこれを呼ぶ。
    /// 外した子資源は以後の保存に含まれない。
    /// </summary>
    public bool Unregister(IStoreComponent component){
        ArgumentNullException.ThrowIfNull(component);
        lock(this.gate){
            return this.components.Remove(component);
        }
    }
    /// <summary>
    /// 現在の保存番号にある、その名前の子資源の状態を返す。
    /// エンジンが登録前に構成(索引の大きさなど)を決めるために使う。登録時の <see cref="IStoreComponent.Load"/> には同じ状態が渡される。
    /// </summary>
    public bool TryGetSavedState(string name,[System.Diagnostics.CodeAnalysis.NotNullWhen(true)]out string? state){
        ValidateName(name);
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.current is not null&&this.current.Components.TryGetValue(name,out var saved)){
                state=saved;
                return true;
            }
            state=null;
            return false;
        }
    }
    /// <summary>登録済みの全子資源の状態を保存し、保存番号を返す。メモリストアでは <see cref="NotSupportedException"/>。</summary>
    public Guid Save(){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.directoryPath is null){
                throw new NotSupportedException("A memory store cannot be saved.");
            }
            var states=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var component in this.components){
                states.Add(component.Name,component.Save());
            }
            // 一時ストアは破棄で全削除され、開き直せないので、ディスクへの確実な書き出しは永続ストアだけで行う。
            var durable=this.Kind==StoreKind.Persistent;
            if(durable){
                // 確定点はマニフェストなので、指す先のデータを先に確実にする。全子資源を保存し終えてからまとめて書き出す。
                foreach(var component in this.components){
                    component.Flush();
                }
            }
            var checkpoint=new Checkpoint(Guid.NewGuid(),DateTimeOffset.UtcNow,states);
            // 詰め直しなどで前の保存のデータが要らなくなった(消える)子資源があれば、前の保存は記録に残さない。
            // 残すと、記録は戻れるように見えるのに、実際にはデータが無い。
            var dropOlder=false;
            foreach(var component in this.components){
                if(component.InvalidatesOlderSaves){
                    dropOlder=true;
                }
            }
            List<Checkpoint> next;
            if(dropOlder){
                next=[checkpoint];
            }else{
                next=[..this.checkpoints,checkpoint];
            }
            // 書けなければ、記録も子資源の状態も変えずに例外にする(後始末は確定してからにする)。
            WriteManifest(this.directoryPath,next,durable);
            this.checkpoints.Clear();
            this.checkpoints.AddRange(next);
            this.current=checkpoint;
            foreach(var component in this.components){
                component.SaveCommitted();
            }
            return checkpoint.Token;
        }
    }
    /// <summary>最新の保存状態を、登録済みの子資源へ渡す。</summary>
    public void Load(){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            if(this.checkpoints.Count==0){
                throw new InvalidOperationException("The store has no checkpoint.");
            }
            this.LoadCore(this.checkpoints[^1]);
        }
    }
    /// <summary>
    /// 指定した保存番号の状態を、登録済みの子資源へ渡す。その保存に含まれない子資源には何もしない。
    /// 子資源の復元は、それより新しい保存のデータを切り詰めたり巻き戻したりして無効にするので、新しい保存は記録(マニフェスト)から外す。
    /// 外した保存番号では、以後 <see cref="Load(Guid)"/> できない。
    /// </summary>
    public void Load(Guid token){
        lock(this.gate){
            ObjectDisposedException.ThrowIf(this.disposed,this);
            var index=this.checkpoints.FindIndex(checkpoint=>checkpoint.Token==token);
            if(index<0){
                throw new ArgumentException($"Checkpoint '{token}' does not exist.",nameof(token));
            }
            // 読み込めない保存へ戻して新しい保存だけを失うことがないよう、記録を変える前に、全子資源が読み込めるか確かめる。
            var target=this.checkpoints[index];
            foreach(var component in this.components){
                if(target.Components.TryGetValue(component.Name,out var targetState)){
                    component.ValidateLoad(targetState);
                }
            }
            var newer=this.checkpoints.GetRange(index+1,this.checkpoints.Count-index-1);
            if(newer.Count>0){
                // 子資源を戻す前に外す。戻す途中で落ちても、開き直せば指定した保存へ戻る(逆順だと、無効な新しい保存を指したまま残る)。
                this.checkpoints.RemoveRange(index+1,newer.Count);
                if(this.directoryPath is not null){
                    try{
                        WriteManifest(this.directoryPath,this.checkpoints,this.Kind==StoreKind.Persistent);
                    }catch{
                        this.checkpoints.AddRange(newer);
                        throw;
                    }
                }
            }
            this.LoadCore(this.checkpoints[index]);
        }
    }
    private void LoadCore(Checkpoint checkpoint){
        foreach(var component in this.components){
            if(checkpoint.Components.TryGetValue(component.Name,out var state)){
                component.Load(state);
            }
        }
        this.current=checkpoint;
    }
    /// <summary>登録済みの子資源を破棄する。保存はしない。一時ストアはフォルダを全削除する。</summary>
    public void Dispose(){
        lock(this.gate){
            if(this.disposed){
                return;
            }
            this.disposed=true;
            List<Exception>? errors=null;
            for(var index=this.components.Count-1;index>=0;index--){
                try{
                    this.components[index].Dispose();
                }catch(Exception error){
                    (errors??=[]).Add(error);
                }
            }
            this.components.Clear();
            try{
                this.lockStream?.Dispose();
            }catch(Exception error){
                (errors??=[]).Add(error);
            }
            if(this.Kind==StoreKind.Temporary&&this.directoryPath is not null){
                try{
                    OwnedDirectory.Delete(this.directoryPath);
                }catch(Exception error){
                    (errors??=[]).Add(error);
                }
            }
            if(errors is not null){
                throw new AggregateException("Store cleanup failed.",errors);
            }
        }
    }
    private static void ValidateName(string name){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if(name is "." or ".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.Contains('/')||name.Contains('\\')){
            throw new ArgumentException($"'{name}' cannot be used as a component name.",nameof(name));
        }
    }
    private static List<Checkpoint> ReadManifest(string directory){
        var path=Path.Combine(directory,ManifestFileName);
        if(!File.Exists(path)){
            return [];
        }
        var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path));
        if(manifest is null||manifest.Format!=FormatName){
            throw new InvalidDataException($"'{path}' is not a Seamless store manifest.");
        }
        return manifest.Checkpoints;
    }
    private static void WriteManifest(string directory,List<Checkpoint> checkpoints,bool durable){
        var path=Path.Combine(directory,ManifestFileName);
        var temporary=path+".tmp";
        var bytes=new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(new Manifest(FormatName,checkpoints)));
        using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)){
            stream.Write(bytes);
            if(durable){
                // 名前を変えたマニフェストの中身が、電源断のあとに空で見えないようにする。
                stream.Flush(true);
            }
        }
        File.Move(temporary,path,overwrite:true);
    }
    private sealed record Checkpoint(Guid Token,DateTimeOffset CreatedAt,Dictionary<string,string> Components);
    private sealed record Manifest(string Format,List<Checkpoint> Checkpoints);
}
