using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.Core;

public enum CapabilityVerification{
    Unsupported,
    ApiShapeOnly,
    RuntimeVerified,
}

public enum SourceRootRoute{
    Array,
    Enumerable,
    Range,
    List,
    PersistedSet,
    TsavoriteHashSet,
    ZLinqFromArray,
    ZLinqFromEnumerable,
    ZLinqFromList,
    ZLinqFromRange,
}

public enum LinqBindingTarget{
    SystemLinq,
    FLinqInterceptor,
    ZLinq,
}

public sealed record ExecutionRouteCapability(
    ExecutionPath Path,
    SourceRootRoute RootRoute,
    LinqBindingTarget BindingTarget,
    string ExpectedContainingType,
    bool PreservesProviderRoot,
    bool MustMaterializeBeforeReturn);

public sealed record OperatorPathCapability(
    ExecutionPath Path,
    bool IsSupported,
    CapabilityVerification Verification,
    bool SupportsNativeFusion=false,
    string? Limitation=null);

public sealed record OperatorCapability(string OperatorId,IReadOnlyList<OperatorPathCapability> Paths){
    [JsonIgnore]public bool IsSupportedOnAllPaths=>this.Paths.Count==Enum.GetValues<ExecutionPath>().Length&&this.Paths.All(static path=>path.IsSupported);

    // ZLinq は対応しないクエリでは経路ごと外せる(QueryZLinqSupport)ので、System と FLinq の経路だけで判定する。
    [JsonIgnore]public bool IsSupportedOnCorePaths=>this.Paths
        .Where(static path=>path.Path!=ExecutionPath.ZLinq)
        .All(static path=>path.IsSupported);

    public OperatorPathCapability Get(ExecutionPath path){
        return this.Paths.Single(capability=>capability.Path==path);
    }
}

public sealed record QueryCapabilityProfile(
    int ProfileVersion,
    SourceTarget SourceTarget,
    SourceKind SourceKind,
    SourceFamily SourceFamily,
    QueryOrderSemantics InitialOrder,
    bool Repeatable,
    bool RequiresDispose,
    bool RequiresSameThread,
    CountCost CountCost,
    IReadOnlyList<ExecutionRouteCapability> Routes,
    IReadOnlyList<OperatorCapability> Operators){
    public const int CurrentProfileVersion=14;

    public OperatorCapability GetOperator(string operatorId){
        return this.Operators.Single(capability=>StringComparer.Ordinal.Equals(capability.OperatorId,operatorId));
    }

    public bool SupportsOnCorePaths(string operatorId){
        return this.Operators.FirstOrDefault(capability=>StringComparer.Ordinal.Equals(capability.OperatorId,operatorId))?.IsSupportedOnCorePaths==true;
    }

    public bool SupportsOnAllPaths(string operatorId){
        return this.Operators.FirstOrDefault(capability=>StringComparer.Ordinal.Equals(capability.OperatorId,operatorId))?.IsSupportedOnAllPaths==true;
    }
}

public sealed record QueryCapabilitySet(
    int ProfileVersion,
    int OperatorSetVersion,
    OperatorSetMode Mode,
    IReadOnlyList<SourceTarget> SourceTargets,
    IReadOnlyList<string> OperatorIds,
    string CapabilityId);

public static class QueryCapabilityProfiles{
    private static readonly IReadOnlyDictionary<SourceTarget,QueryCapabilityProfile> Defaults=CreateDefaults();

    public static QueryCapabilityProfile Get(SourceTarget sourceTarget)=>Defaults[sourceTarget];

    /// <summary>
    /// その Source でその要素型を使えるか。int 以外の要素型は、配列・List・IEnumerable だけ。
    /// Range は int だけ、永続系(Persisted・Tsavorite)は int 以外の保存を確かめていないので対象外。
    /// </summary>
    public static bool SupportsElementType(SourceTarget sourceTarget,QueryElementType elementType){
        return elementType==QueryElementType.Int
            ||sourceTarget.GetSourceKind() is SourceKind.Array or SourceKind.IEnumerable or SourceKind.List;
    }

    /// <summary>要素型ごとの演算子集合。<c>Int</c> は従来と同じ集合・同じ識別子。</summary>
    public static QueryCapabilitySet CreateSet(OperatorSetMode mode,IEnumerable<SourceTarget> sourceTargets,QueryElementType elementType){
        var baseSet=CreateSet(mode,sourceTargets);
        if(elementType==QueryElementType.Int){
            return baseSet;
        }
        foreach(var target in baseSet.SourceTargets){
            if(!SupportsElementType(target,elementType)){
                throw new ArgumentException($"{elementType} is not supported on {target}.",nameof(sourceTargets));
            }
        }
        var descriptor=ElementTypeCatalog.Get(elementType);
        var operatorIds=baseSet.OperatorIds.Where(descriptor.Supports).ToArray();
        var payload=new{
            profileVersion=QueryCapabilityProfile.CurrentProfileVersion,
            operatorSetVersion=BuiltInOperatorCatalog.OperatorSetVersion,
            mode,
            sourceTargets=baseSet.SourceTargets,
            operatorIds,
            elementType,
        };
        var json=JsonSerializer.Serialize(payload,new JsonSerializerOptions{
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
            Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)},
        });
        return baseSet with{
            OperatorIds=operatorIds,
            CapabilityId=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
        };
    }

    public static IReadOnlyList<QueryCapabilityProfile> GetAll(){
        return Enum.GetValues<SourceTarget>().Select(Get).ToArray();
    }

    public static QueryCapabilitySet CreateSet(OperatorSetMode mode,IEnumerable<SourceTarget> sourceTargets){
        ArgumentNullException.ThrowIfNull(sourceTargets);
        var selectedTargets=sourceTargets.Distinct().Order().ToArray();
        if(selectedTargets.Length==0){
            throw new ArgumentException("At least one source target is required.",nameof(sourceTargets));
        }
        var capabilityTargets=mode==OperatorSetMode.Common
            ?Enum.GetValues<SourceTarget>()
            :selectedTargets;
        var profiles=capabilityTargets.Select(Get).ToArray();
        var operatorIds=BuiltInOperatorCatalog.All
            .Where(descriptor=>profiles.All(profile=>profile.SupportsOnCorePaths(descriptor.OperatorId)))
            .Select(static descriptor=>descriptor.OperatorId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var id=ComputeSetId(mode,selectedTargets,operatorIds);
        return new(
            QueryCapabilityProfile.CurrentProfileVersion,
            BuiltInOperatorCatalog.OperatorSetVersion,
            mode,
            selectedTargets,
            operatorIds,
            id);
    }

    private static IReadOnlyDictionary<SourceTarget,QueryCapabilityProfile> CreateDefaults(){
        return Enum.GetValues<SourceTarget>().ToDictionary(static target=>target,CreateProfile);
    }

    private static QueryCapabilityProfile CreateProfile(SourceTarget target){
        var sourceKind=target.GetSourceKind();
        var isProvider=sourceKind is SourceKind.Persisted or SourceKind.Tsavorite;
        var operators=new List<OperatorCapability>();
        foreach(var descriptor in BuiltInOperatorCatalog.All){
            var paths=new List<OperatorPathCapability>();
            foreach(var path in Enum.GetValues<ExecutionPath>()){
                var supported=true;
                var verification=CapabilityVerification.RuntimeVerified;
                string? limitation=null;
                if(descriptor.OperatorId==QueryOperatorIds.SelectManyXor
                    &&path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
                    supported=false;
                    verification=CapabilityVerification.RuntimeVerified;
                    limitation="FLinq generated SelectMany captures the inner fixture from an enumerator struct and fails with CS1673.";
                }
                if(descriptor.OperatorId is QueryOperatorIds.CountBy or QueryOperatorIds.AggregateBy
                    &&target.GetSourceKind()==SourceKind.Range
                    &&path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
                    supported=false;
                    verification=CapabilityVerification.RuntimeVerified;
                    limitation="FLinq generated CountBy/AggregateBy over Enumerable.Range references the undeclared locals __rangeStart0/__rangeCount0 and fails with CS0103 (generator defect found by the chain verifier port).";
                }
                if(target.GetFamily()==SourceFamily.Seamless
                    &&descriptor.OperatorId==QueryOperatorIds.Contains
                    &&path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized){
                    supported=false;
                    verification=CapabilityVerification.RuntimeVerified;
                    limitation="Concrete provider roots bind their instance Contains method and therefore do not produce an FLinq interceptor.";
                }
                if(descriptor.OperatorId is QueryOperatorIds.OrderBy or QueryOperatorIds.ThenBy or QueryOperatorIds.OrderByKey
                    &&path==ExecutionPath.ZLinq){
                    supported=false;
                    verification=CapabilityVerification.RuntimeVerified;
                    limitation="ZLinq 1.5.6 OrderBy materialization throws because its comparer reports inconsistent results on the deterministic integer fixture.";
                }
                if(isProvider&&descriptor.RequiresDeterministicOrder){
                    supported=false;
                    verification=CapabilityVerification.ApiShapeOnly;
                    limitation="The operator is position-dependent and is excluded for an unordered provider root.";
                }
                var nativeFusion=target.GetFamily()==SourceFamily.Seamless
                    &&isProvider
                    &&path is ExecutionPath.FLinqNoOptimizations or ExecutionPath.FLinqOptimized
                    &&descriptor.OperatorId is QueryOperatorIds.Where or QueryOperatorIds.Select;
                paths.Add(new(path,supported,verification,nativeFusion,limitation));
            }
            operators.Add(new(descriptor.OperatorId,paths));
        }
        return new(
            QueryCapabilityProfile.CurrentProfileVersion,
            target,
            sourceKind,
            target.GetFamily(),
            target.GetInitialOrder(),
            Repeatable:true,
            target.RequiresDispose(),
            target.RequiresSameThread(),
            target.GetCountCost(),
            CreateRoutes(target),
            operators);
    }

    private static IReadOnlyList<ExecutionRouteCapability> CreateRoutes(SourceTarget target){
        return[
            CreateRoute(target,ExecutionPath.SystemLinq),
            CreateRoute(target,ExecutionPath.FLinqNoOptimizations),
            CreateRoute(target,ExecutionPath.FLinqOptimized),
            CreateRoute(target,ExecutionPath.ZLinq),
        ];
    }

    private static ExecutionRouteCapability CreateRoute(SourceTarget target,ExecutionPath path){
        if(path==ExecutionPath.ZLinq){
            var zLinqRoot=target.GetSourceKind() switch{
                SourceKind.Array=>SourceRootRoute.ZLinqFromArray,
                SourceKind.List=>SourceRootRoute.ZLinqFromList,
                SourceKind.Range=>SourceRootRoute.ZLinqFromRange,
                _=>SourceRootRoute.ZLinqFromEnumerable,
            };
            return new(path,zLinqRoot,LinqBindingTarget.ZLinq,"ZLinq.ValueEnumerable",false,true);
        }
        if(path==ExecutionPath.SystemLinq){
            var systemRoot=target.GetSourceKind() switch{
                SourceKind.Array=>SourceRootRoute.Array,
                SourceKind.IEnumerable=>SourceRootRoute.Enumerable,
                SourceKind.Range=>SourceRootRoute.Range,
                SourceKind.List=>SourceRootRoute.List,
                SourceKind.Persisted=>SourceRootRoute.PersistedSet,
                SourceKind.Tsavorite=>SourceRootRoute.TsavoriteHashSet,
                _=>throw new ArgumentOutOfRangeException(nameof(target)),
            };
            return new(path,systemRoot,LinqBindingTarget.SystemLinq,"System.Linq.Enumerable",false,false);
        }
        var preservesProviderRoot=target.GetFamily()==SourceFamily.Seamless&&target.GetSourceKind() is SourceKind.Persisted or SourceKind.Tsavorite;
        SourceRootRoute flinqRoot;
        if(preservesProviderRoot){
            flinqRoot=target.GetSourceKind()==SourceKind.Persisted?SourceRootRoute.PersistedSet:SourceRootRoute.TsavoriteHashSet;
        }else{
            flinqRoot=target.GetSourceKind() switch{
                SourceKind.Array=>SourceRootRoute.Array,
                SourceKind.Range=>SourceRootRoute.Range,
                _=>SourceRootRoute.Enumerable,
            };
        }
        return new(path,flinqRoot,LinqBindingTarget.FLinqInterceptor,"System.Linq.Enumerable",preservesProviderRoot,false);
    }

    private static string ComputeSetId(OperatorSetMode mode,IReadOnlyList<SourceTarget> targets,IReadOnlyList<string> operatorIds){
        var payload=new{
            profileVersion=QueryCapabilityProfile.CurrentProfileVersion,
            operatorSetVersion=BuiltInOperatorCatalog.OperatorSetVersion,
            mode,
            sourceTargets=targets,
            operatorIds,
        };
        var json=JsonSerializer.Serialize(payload,new JsonSerializerOptions{
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
            Converters={new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)},
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
