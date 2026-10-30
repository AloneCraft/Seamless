using System.Text.Json;

namespace Seamless.Execution;

public enum Profile{Minimum,Optimized}

public sealed record ProfileSettings{
    public long? IndexBytes{get;init;}
    public long? PageBytes{get;init;}
    public long? SegmentBytes{get;init;}
    public long? LogMemoryBytes{get;init;}
    public long? InitialCapacity{get;init;}
    public long? InitialDataBytes{get;init;}
    public double? MutableFraction{get;init;}

    public ProfileSettings OverrideWith(ProfileSettings value)=>new(){
        IndexBytes=value.IndexBytes??this.IndexBytes,
        PageBytes=value.PageBytes??this.PageBytes,
        SegmentBytes=value.SegmentBytes??this.SegmentBytes,
        LogMemoryBytes=value.LogMemoryBytes??this.LogMemoryBytes,
        InitialCapacity=value.InitialCapacity??this.InitialCapacity,
        InitialDataBytes=value.InitialDataBytes??this.InitialDataBytes,
        MutableFraction=value.MutableFraction??this.MutableFraction,
    };

    public void Validate(){
        foreach(var size in new[]{this.IndexBytes,this.PageBytes,this.SegmentBytes,this.LogMemoryBytes,this.InitialCapacity,this.InitialDataBytes}){
            if(size is <=0){throw new ArgumentOutOfRangeException(nameof(size),"Profile sizes must be positive.");}
        }
        foreach(var size in new[]{this.IndexBytes,this.PageBytes,this.SegmentBytes,this.LogMemoryBytes}){
            if(size is long bytes&&(bytes&(bytes-1))!=0){throw new ArgumentException("Index and log sizes must be powers of two.");}
        }
        if(this.MutableFraction is double fraction&&(!double.IsFinite(fraction)||fraction<=0||fraction>1)){
            throw new ArgumentOutOfRangeException(nameof(this.MutableFraction));
        }
        if(this.PageBytes is long page){
            if(page<4096||page>1L<<30){throw new ArgumentOutOfRangeException(nameof(this.PageBytes));}
            if(this.SegmentBytes is long segment&&segment<page){throw new ArgumentException("Segment size must be at least the page size.");}
            if(this.LogMemoryBytes is long memory&&memory<page*2){throw new ArgumentException("Log memory must contain at least two pages.");}
        }
        if(this.IndexBytes is <64){throw new ArgumentOutOfRangeException(nameof(this.IndexBytes));}
    }
}

public sealed record ProfileRequest(string Backend,StoreKind StoreKind,string ElementType,long ElementCount=0,string Workload="*",string Environment="*",string IO="BackendDefault",ProfileEvidence? RequiredEvidence=null,string StorageKey="Unknown");

public sealed record MeasuredProfile(
    string ID,int Version,ProfileRequest Conditions,long MaximumCount,ProfileSettings Settings,
    string[] EvidenceRunIDs,bool IndependentlyValidated=false,Growth.Descriptor? Growth=null,ProfileEvidence? Evidence=null){
    public bool AppliesTo(ProfileRequest request)=>this.Conditions.Backend==request.Backend
        &&this.Conditions.StoreKind==request.StoreKind
        &&this.Conditions.IO==request.IO
        &&this.Conditions.StorageKey==request.StorageKey
        &&(this.Conditions.StoreKind==StoreKind.Memory||this.Conditions.StorageKey!="Unknown")
        &&(this.Conditions.ElementType=="*"||this.Conditions.ElementType==request.ElementType)
        &&request.ElementCount>=this.Conditions.ElementCount&&request.ElementCount<=this.MaximumCount
        &&(this.Conditions.Workload=="*"||this.Conditions.Workload==request.Workload)
        &&(this.Conditions.Environment=="*"||this.Conditions.Environment==request.Environment);
}

/// <summary>アプリのDBから独立した、持ち運べる測定済み設定表。</summary>
public sealed class ProfileCatalog{
    private readonly MeasuredProfile[] profiles;
    public static ProfileCatalog Bundled{get;}=LoadBundled();
    public ProfileCatalog(IEnumerable<MeasuredProfile> profiles){
        this.profiles=profiles.ToArray();
        foreach(var profile in this.profiles){
            ArgumentException.ThrowIfNullOrWhiteSpace(profile.ID);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profile.Version);
            profile.Settings.Validate();
            if(profile.Conditions.ElementCount<0||profile.MaximumCount<profile.Conditions.ElementCount){throw new ArgumentException("Invalid profile count range.");}
            if(!profile.IndependentlyValidated||profile.EvidenceRunIDs.Distinct(StringComparer.Ordinal).Count()<2){
                throw new ArgumentException("Optimized profiles require separate measurement and validation runs.");
            }
        }
    }
    public IReadOnlyList<MeasuredProfile> Profiles=>Array.AsReadOnly(this.profiles);
    public static ProfileCatalog Load(string path)=>FromJSON(File.ReadAllText(path));
    public static ProfileCatalog FromJSON(string json){
        var document=JsonSerializer.Deserialize<CatalogDocument>(json)??throw new InvalidDataException("A profile document is required.");
        if(document.Version!=1){throw new InvalidDataException("Unsupported profile document version.");}
        return new(document.Profiles);
    }
    public string ToJSON()=>JsonSerializer.Serialize(new CatalogDocument(1,this.profiles),new JsonSerializerOptions{WriteIndented=true});
    public MeasuredProfile? Find(ProfileRequest request)=>this.Candidates(request).FirstOrDefault(profile=>profile.Evidence==(request.RequiredEvidence??ProfileEvidence.Current));
    internal IEnumerable<MeasuredProfile> Candidates(ProfileRequest request)=>this.profiles
        .Where(profile=>profile.AppliesTo(request))
        .OrderByDescending(profile=>profile.Conditions.ElementType!="*")
        .ThenByDescending(profile=>profile.Conditions.Workload!="*")
        .ThenBy(profile=>profile.MaximumCount-profile.Conditions.ElementCount)
        .ThenByDescending(profile=>profile.Version);
    private static ProfileCatalog LoadBundled(){
        var path=Path.Combine(AppContext.BaseDirectory,"Seamless.Execution.Profiles","measured.json");
        if(!File.Exists(path)){return new([]);}
        return Load(path);
    }
    private sealed record CatalogDocument(int Version,MeasuredProfile[] Profiles);
}

public sealed record ProfileResolution(Profile Requested,Profile Applied,string? ProfileID,int? ProfileVersion,ProfileSettings Settings,string Reason,string[] EvidenceRunIDs,Growth.Descriptor? Growth=null);

public static class ProfileResolver{
    public const int MinimumVersion=1;
    public static ProfileExplanation Explain(Profile requested,ProfileRequest request,StoreLimits limits,
        ProfileSettings? explicitSettings=null,ProfileCatalog? supplied=null,Growth.Policy? explicitGrowth=null){
        var result=Resolve(requested,request,limits,explicitSettings,supplied,explicitGrowth);
        IReadOnlyList<string> missing=Array.Empty<string>();
        if(requested==Profile.Optimized&&result.Applied==Profile.Minimum){missing=Array.AsReadOnly(new[]{result.Reason});}
        return ProfileExplanation.Create(result,explicitGrowth,request.RequiredEvidence) with{Conditions=request,MissingConditions=missing};
    }
    public static ProfileResolution Resolve(Profile requested,ProfileRequest request,StoreLimits limits,
        ProfileSettings? explicitSettings=null,ProfileCatalog? supplied=null,Growth.Policy? explicitGrowth=null){
        ArgumentOutOfRangeException.ThrowIfNegative(request.ElementCount);
        explicitSettings??=new();
        explicitSettings.Validate();
        var minimum=Minimum(request.Backend);
        var selected=minimum.OverrideWith(explicitSettings);
        selected.Validate();
        EnsureBudget(selected,limits);
        if(requested==Profile.Minimum){return new(requested,Profile.Minimum,"Minimum",MinimumVersion,selected,"Minimum initial configuration.",[]);}
        if(requested!=Profile.Optimized){throw new ArgumentOutOfRangeException(nameof(requested));}
        if(request.StoreKind!=StoreKind.Memory&&request.StorageKey=="Unknown"){
            return new(requested,Profile.Minimum,"Minimum",MinimumVersion,selected,"Storage volume is unknown; measured disk profiles cannot be applied.",[]);
        }
        var rejected=new List<string>();
        var candidates=(supplied?.Candidates(request)??[]).Concat(ProfileCatalog.Bundled.Candidates(request));
        foreach(var candidate in candidates){
            if(candidate.Evidence!=(request.RequiredEvidence??ProfileEvidence.Current)){
                rejected.Add(candidate.ID+": evidence versions unknown or incompatible.");continue;
            }
            if(candidate.Growth is not null&&explicitGrowth is null&&Growth.Policy.FromDescriptor(candidate.Growth) is null){
                rejected.Add(candidate.ID+": growth strategy unavailable.");continue;
            }
            var optimized=minimum.OverrideWith(candidate.Settings).OverrideWith(explicitSettings);
            optimized.Validate();
            try{
                EnsureBudget(optimized,limits);
            }catch(ResourceLimitException){
                rejected.Add(candidate.ID+": resource budget exceeded.");continue;
            }
            return new(requested,Profile.Optimized,candidate.ID,candidate.Version,optimized,
                string.Join(" ",rejected.Append("An independently validated measured profile was selected.")),candidate.EvidenceRunIDs,explicitGrowth?.Descriptor??candidate.Growth);
        }
        var reason="No independently validated profile matches these conditions.";
        if(rejected.Count>0){reason=string.Join(" ",rejected);}
        return new(requested,Profile.Minimum,"Minimum",MinimumVersion,selected,reason,[]);
    }
    public static ProfileSettings Minimum(string backend)=>backend switch{
        "Tsavorite"=>new(){IndexBytes=64,PageBytes=4096,SegmentBytes=4096,LogMemoryBytes=8192,MutableFraction=1},
        "Persisted"=>new(){InitialCapacity=16,InitialDataBytes=4096},
        _=>throw new ArgumentException("Unknown storage backend.",nameof(backend)),
    };
    private static void EnsureBudget(ProfileSettings settings,StoreLimits limits){
        var required=checked((settings.IndexBytes??0)+(settings.LogMemoryBytes??0));
        if(required>limits.MemoryBytes){throw new ResourceLimitException(ResourceLimitKind.Memory,required,limits.MemoryBytes);}
    }
}

public sealed record StoreOptions{
    private Growth.Policy? growth;
    public Profile Profile{get;init;}=Profile.Minimum;
    public ProfileCatalog? Profiles{get;init;}
    public Growth.Policy Growth{get=>this.growth??new Growth.Policy<Growth.Doubling>();init=>this.growth=value;}
    internal Growth.Policy? ExplicitGrowth=>this.growth;
    public string Workload{get;init;}="*";
    public string Environment{get;init;}=ProfileEnvironment.Current;
}
