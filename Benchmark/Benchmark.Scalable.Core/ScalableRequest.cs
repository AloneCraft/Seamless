using Seamless.Execution;

using Benchmark.Infrastructure;

namespace Benchmark.Scalable.Core;

public enum Workload{Collections,FilterAggregate,Sort,Join}
public enum ScalableImplementation{Native,Scoped,System}
public sealed record ScalableRequest(
    long Count=4096,int Seed=42,string Backend="Tsavorite",StoreKind Kind=StoreKind.Memory,
    Workload Workload=Workload.Collections,Profile Profile=Profile.Minimum,string Growth="profile",
    ProfileSettings? ExplicitSettings=null,string? ProfileCatalogJSON=null,
    long MemoryLimitBytes=268435456,long DiskLimitBytes=17179869184,int Samples=7,
    bool Adaptive=false,int ChunkSize=4096,ScalableImplementation Implementation=ScalableImplementation.Native,string IOMode="BackendDefault",string AccessPattern="Sequential",string? TemporaryDirectory=null){
    [System.Text.Json.Serialization.JsonIgnore]public ScalableImplementation ActualImplementation{get{if(this.Workload==Workload.Collections){return ScalableImplementation.Native;}return this.Implementation;}}
    [System.Text.Json.Serialization.JsonIgnore]public string WorkloadID{
        get{
            if(this.Workload==Workload.Collections&&this.Backend=="Persisted"){return "Collections.AppendOnly";}
            return this.Workload.ToString();
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]public string ProfileWorkloadID{get{if(this.ActualImplementation==ScalableImplementation.Native){return this.WorkloadID;}return this.WorkloadID+"."+this.ActualImplementation;}}
    public void Validate(){
        ArgumentOutOfRangeException.ThrowIfNegative(this.Count);
        if(this.Count>int.MaxValue){throw new ArgumentOutOfRangeException(nameof(this.Count));}
        if(this.Samples<7||this.Samples>100){throw new ArgumentOutOfRangeException(nameof(this.Samples));}
        if(this.ChunkSize<16||this.ChunkSize>65536){throw new ArgumentOutOfRangeException(nameof(this.ChunkSize));}
        if(this.Backend is not ("Persisted" or "Tsavorite")){throw new ArgumentException("Unknown backend.");}
        if(this.Kind is not (StoreKind.Memory or StoreKind.Temporary)){throw new ArgumentException("Choose Memory or Temporary.");}
        this.ExplicitSettings?.Validate();
        _=IOConfiguration.Parse(this.IOMode,this.AccessPattern);
        if(this.Kind==StoreKind.Memory&&this.IOMode!="BackendDefault"){throw new ArgumentException("入出力方式の比較はTemporaryで実行してください。");}
    }
    internal StoreLimits CreateLimits(){
        var divisor=1L;if(this.Adaptive){divisor=2;}
        return new(){MemoryBytes=this.MemoryLimitBytes/divisor,DiskBytes=this.DiskLimitBytes/divisor};
    }
    internal StoreOptions CreateOptions(string environment){
        var options=new StoreOptions{Profile=this.Profile,Workload=this.ProfileWorkloadID,Environment=environment};
        if(this.Growth!="profile"){
            Seamless.Execution.Growth.Policy growth=this.Growth switch{
                "fixed"=>new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Fixed>(),
                "doubling"=>new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Doubling>(),
                "estimated"=>new Seamless.Execution.Growth.Policy<Seamless.Execution.Growth.Estimated>(),
                _=>throw new ArgumentException("Unknown growth policy.")
            };
            options=options with{Growth=growth};
        }
        if(this.ProfileCatalogJSON is not null){options=options with{Profiles=ProfileCatalog.FromJSON(this.ProfileCatalogJSON)};}
        return options;
    }
    /// <summary>ストア・入力列挙・測定DBを使わず実行前の設定と未確定条件を返す。</summary>
    public ScalableExplanation Explain(string? environment=null){
        this.Validate();
        var options=this.CreateOptions(environment??ProfileEnvironment.Current);
        Seamless.Execution.Growth.Policy? growth=null;
        if(this.Growth!="profile"){growth=options.Growth;}
        var request=new ProfileRequest(this.Backend,this.Kind,"System.Int32|System.Int32",this.Count,this.ProfileWorkloadID,options.Environment,
            ProfileIO.Describe(IOConfiguration.Parse(this.IOMode,this.AccessPattern)),StorageKey:this.StorageKey);
        return new(ProfileResolver.Explain(this.Profile,request,this.CreateLimits(),this.ExplicitSettings,options.Profiles,growth),
            this.ActualImplementation.ToString(),"実装の選択は確定。拡張・結合・外部ソートの詳細経路と資源量は実行時に判断します。");
    }
    [System.Text.Json.Serialization.JsonIgnore]public string StorageKey{
        get{
            if(this.Kind==StoreKind.Memory){return "Memory";}
            return ProfileEnvironment.Storage(this.TemporaryDirectory??Path.GetTempPath());
        }
    }
}
public sealed record ScalableExplanation(ProfileExplanation Profile,string Implementation,string RuntimeDecisions);
public sealed record ScalableProgress(string RunID,int Sample,int Total,long Completed,long Count,string Phase);
