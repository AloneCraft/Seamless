using Seamless.Collections.Persisted;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
using Seamless.Execution.Growth;

namespace Benchmark.Scalable.Core;

internal sealed class ScalableStore:IDisposable{
    public global::Seamless.Execution.Store Owner{get;}
    public ScalableData Data{get;}
    public ScalableStore(ScalableRequest request,string environment,ProfileSettings? settings=null,ResourceBudget? parentBudget=null){
        var options=request.CreateOptions(environment);
        var limits=request.CreateLimits();
        if(request.Kind==StoreKind.Memory){this.Owner=global::Seamless.Execution.Store.CreateMemory(limits:limits,options:options,parentBudget:parentBudget);}
        else{this.Owner=global::Seamless.Execution.Store.CreateTemporary(limits:limits,temporaryDirectory:request.TemporaryDirectory,options:options,parentBudget:parentBudget);}
        try{
            settings??=request.ExplicitSettings??new();
            if(request.Backend=="Persisted"){
                this.Data=new(this.Owner.CreatePersistedDictionary<int,int>("data",settings.InitialCapacity,settings.InitialDataBytes,tuning:global::Benchmark.Infrastructure.IOConfiguration.Parse(request.IOMode,request.AccessPattern),expectedCount:request.Count));
            }else{
                var tuning=new TsavoriteTuning{IndexSize=settings.IndexBytes,LogMemorySize=settings.LogMemoryBytes};
                if(settings.PageBytes is long page){tuning=tuning with{PageSize=page};}
                if(settings.SegmentBytes is long segment){tuning=tuning with{SegmentSize=segment};}
                if(settings.MutableFraction is double fraction){tuning=tuning with{MutableFraction=fraction};}
                tuning=tuning with{Storage=global::Benchmark.Infrastructure.IOConfiguration.Parse(request.IOMode,request.AccessPattern)};
                this.Data=new(this.Owner.CreateTsavorite<int,int>("data",tuning,request.Count).Create());
            }
        }catch{this.Owner.Dispose();throw;}
    }
    public ProfileSettings Effective=>this.Owner.ResolvedProfiles.Single().Value.Settings;
    public ProfileSettings Current=>this.Owner.GetCurrentSettings("data");
    public void Dispose(){
        this.Data.Dispose();
        this.Owner.Dispose();
    }
}

internal sealed class ScalableData:IEnumerable<KeyValuePair<int,int>>,IDisposable{
    private readonly IReadOnlyDictionary<int,int> read;
    private readonly global::Seamless.Collections.Persisted.Dictionary<int,int>? persisted;
    private readonly global::Seamless.Collections.Tsavorite.IDictionary<int,int>? tsavorite;
    public ScalableData(global::Seamless.Collections.Persisted.Dictionary<int,int> data){this.read=data;this.persisted=data;}
    public ScalableData(global::Seamless.Collections.Tsavorite.IDictionary<int,int> data){this.read=data;this.tsavorite=data;}
    public int Count=>this.read.Count;
    public IEnumerable<int> Values=>this.read.Values;
    public bool CanRemove=>this.tsavorite is not null;
    public int this[int key]{set=>this.Add(key,value);}
    public void Add(int key,int value){if(this.persisted is not null){this.persisted.Add(key,value);}else{this.tsavorite![key]=value;}}
    public void Add(KeyValuePair<int,int> pair)=>this.Add(pair.Key,pair.Value);
    public bool Remove(int key)=>this.tsavorite!.Remove(key);
    public bool TryGetValue(int key,out int value)=>this.read.TryGetValue(key,out value);
    public IEnumerator<KeyValuePair<int,int>> GetEnumerator()=>this.read.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public void Dispose()=>((IDisposable)this.read).Dispose();
}
