using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using Seamless.Execution;
using Seamless.Execution.Growth;
using System.Text.Json;

namespace Benchmark.Dynamic.Worker;

public static class WorkerStorage{
    public static global::Seamless.Collections.StorageTuning? Storage(WorkerStorageSettings settings)=>IOConfiguration.Parse(settings.IOMode,settings.AccessPattern);
    public static ProfileSettings Requested(WorkerStorageSettings settings)=>new(){
        IndexBytes=settings.IndexBytes,PageBytes=settings.PageBytes,SegmentBytes=settings.SegmentBytes,LogMemoryBytes=settings.MemoryBytes,
        InitialCapacity=settings.InitialCapacity,InitialDataBytes=settings.InitialDataBytes
    };
    public static ConfigurationChange[] Changes(SourceFixture fixture,string runID,int interval){
        return fixture.Stores.SelectMany((owner,source)=>owner.GrowthEvents.Concat(owner.GrowthDecisions.Values.Where(change=>change.Decision.Status!=Status.Grow&&change.Context.RequiredBytes>change.Context.CurrentBytes))
            .Select(change=>new ConfigurationChange(Guid.NewGuid().ToString("N"),runID,interval,
                JsonSerializer.Serialize(new{Source=source,change.Component,change.Context.Resource,Bytes=change.Context.CurrentBytes}),
                JsonSerializer.Serialize(new{Source=source,change.Component,change.Context.Resource,Bytes=change.Decision.TargetBytes}),
                change.Decision.Reason,change.Decision.Status==Status.Grow,change.ElapsedNanoseconds,"","",change.RecordedAt))).ToArray();
    }
    public static StoreOptions Options(WorkerStorageSettings settings,string workload="*"){
        var options=new StoreOptions{Profile=Enum.Parse<Profile>(settings.Profile),Workload=workload,Environment=ProfileEnvironment.Current};
        if(settings.ProfileCatalogJSON is not null){options=options with{Profiles=ProfileCatalog.FromJSON(settings.ProfileCatalogJSON)};}
        if(settings.Growth!="profile"){options=options with{Growth=Policy(settings.Growth)};}
        return options;
    }
    public static Policy Policy(string id)=>id switch{
        "fixed"=>new Policy<Fixed>(),
        "doubling"=>new Policy<Doubling>(),
        "estimated"=>new Policy<Estimated>(),
        _=>throw new ArgumentException("未登録の拡張ストラテジーです。利用者定義は型付きAPIへ登録してください。",nameof(id)),
    };
    public static Store Create(WorkerStorageSettings settings,string workload="*"){
        if(settings.Kind=="Memory"&&settings.IOMode!="BackendDefault"){throw new ArgumentException("入出力方式の比較はTemporaryで実行してください。");}
        var limits=new StoreLimits{MemoryBytes=settings.MemoryLimitBytes,DiskBytes=settings.DiskLimitBytes};
        var options=Options(settings,workload);
        return settings.Kind switch{
            "Memory"=>Store.CreateMemory(limits,options),
            "Temporary"=>Store.CreateTemporary(limits,options:options),
            _=>throw new ArgumentException("測定はMemoryまたはTemporaryで実行します。既存の永続データは変更しません。"),
        };
    }
    public static global::Seamless.Collections.Tsavorite.TsavoriteTuning Tuning(WorkerStorageSettings settings){
        var tuning=new global::Seamless.Collections.Tsavorite.TsavoriteTuning{IndexSize=settings.IndexBytes,LogMemorySize=settings.MemoryBytes,Storage=Storage(settings)};
        if(settings.Growth!="profile"){tuning=tuning with{Growth=Policy(settings.Growth)};}
        if(settings.PageBytes is long page){tuning=tuning with{PageSize=page};}
        if(settings.SegmentBytes is long segment){tuning=tuning with{SegmentSize=segment};}
        return tuning;
    }
}
