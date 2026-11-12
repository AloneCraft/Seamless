using System.Text.Json;

namespace Seamless.Management;

public sealed record PlanOptimizationEvent(int? Sequence,int? RunId,string Layer,string Phase,string RuleId,int? Round,int? Pass,bool? Changed,bool? Revisited,string? Before,string? After);
public sealed record PlanOptimizationTermination(int? RunId,string Layer,int? RoundCount,string Termination);
public sealed record PlanOptimizationSummary(int? RewriteCount,int? MaxRewriteDepth,int? OptimizationPassCount,bool? ReachedFixedPoint,string[] AppliedRuleNames);
public sealed record PlanOptimizationTrace(bool Available,bool Complete,PlanOptimizationEvent[] Events,PlanOptimizationTermination[] LayerTerminations,PlanOptimizationSummary? Summary,string? UnavailableReason=null);

/// <summary>生成器が記録した最適化追跡だけを読む。適用候補や不採用理由は補完しない。</summary>
public static class OptimizationTraceReader{
    public const int MaximumEvents=256;
    private static readonly HashSet<string> KnownRules=new(StringComparer.Ordinal){
        "Algebraic.CountComparisonToAny","Algebraic.WhereTerminalFusion","Algebraic.IdentitySelectRemoval","Algebraic.Reverse","Algebraic.Rewrite",
        "Optimize.PredicatePushdown","Projection.Merge","Pipeline.Distinct","Pipeline.DistinctBy","Pipeline.Take","Pipeline.Skip","Pipeline.Reverse","Pipeline.Where",
        "GroupByConsumptionPlan","JoinConsumptionPlan"
    };
    private static readonly HashSet<string> KnownPhases=new(StringComparer.Ordinal){"AlgebraicRewriter","OptimizeVisitor","ProjectionMergeVisitor","NormalizeStages","SemanticPlan"};

    public static PlanOptimizationTrace Read(JsonElement trace,bool includeCode=false){
        try{
            if(trace.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined){return Missing("NotRecorded");}
            if(trace.ValueKind!=JsonValueKind.Object){return Missing("InvalidTrace");}
            if(Number(trace,"schemaVersion")!=1){return Missing("UnsupportedTraceSchema");}
            var events=Array(trace,"events");var terminations=Array(trace,"layerTerminations");
            if(events is null||terminations is null){return Missing("InvalidTrace");}
            var complete=(Boolean(trace,"complete")??true)&&events.Value.GetArrayLength()<=MaximumEvents&&terminations.Value.GetArrayLength()<=MaximumEvents;
            var result=new List<PlanOptimizationEvent>();
            foreach(var item in events.Value.EnumerateArray().Take(MaximumEvents)){
                Object(item);
                var rule=Text(item,"ruleId")??"Unknown";var phase=Text(item,"phase")??"Unknown";
                if(!includeCode){rule=Known(rule,KnownRules);phase=Known(phase,KnownPhases);}
                result.Add(new(Number(item,"sequence"),Number(item,"runId"),Layer(item),phase,rule,Number(item,"round"),Number(item,"pass"),Boolean(item,"changed"),Boolean(item,"revisited"),includeCode?Text(item,"before"):null,includeCode?Text(item,"after"):null));
            }
            var endings=new List<PlanOptimizationTermination>();
            foreach(var item in terminations.Value.EnumerateArray().Take(MaximumEvents)){
                Object(item);endings.Add(new(Number(item,"runId"),Layer(item),Number(item,"roundCount"),Termination(item)));
            }
            PlanOptimizationSummary? summary=null;
            if(Property(trace,"summary") is{} value&&value.ValueKind!=JsonValueKind.Null){
                Object(value);var rules=Array(value,"appliedRuleNames");var names=new List<string>();
                if(rules is not null){
                    complete&=rules.Value.GetArrayLength()<=MaximumEvents;
                    foreach(var rule in rules.Value.EnumerateArray().Take(MaximumEvents)){
                        if(rule.ValueKind!=JsonValueKind.String){throw new JsonException();}
                        var name=Bound(rule.GetString()!);names.Add(includeCode?name:Known(name,KnownRules));
                    }
                }
                summary=new(Number(value,"rewriteCount"),Number(value,"maxRewriteDepth"),Number(value,"optimizationPassCount"),Boolean(value,"reachedFixedPoint"),names.ToArray());
            }
            return new(true,complete,result.ToArray(),endings.ToArray(),summary);
        }catch(JsonException){return Missing("InvalidTrace");}
    }
    internal static JsonElement Project(JsonElement trace,bool includeCode){
        if(includeCode&&trace.ValueKind!=JsonValueKind.Undefined){return trace.Clone();}
        var view=Read(trace);
        if(!view.Available){return JsonSerializer.SerializeToElement<object?>(null);}
        return ManagementJson.Element(new{
            schemaVersion=1,complete=view.Complete,
            events=view.Events.Select(item=>new{item.Sequence,item.RunId,item.Layer,item.Phase,item.RuleId,item.Round,item.Pass,item.Changed,item.Revisited}).ToArray(),
            layerTerminations=view.LayerTerminations,
            summary=view.Summary
        });
    }
    private static PlanOptimizationTrace Missing(string reason)=>new(false,false,[],[],null,reason);
    private static string Known(string value,HashSet<string> allowed){if(allowed.Contains(value)){return value;}return "Unknown";}
    private static void Object(JsonElement value){if(value.ValueKind!=JsonValueKind.Object){throw new JsonException();}}
    private static JsonElement? Property(JsonElement value,string name){
        Object(value);
        foreach(var property in value.EnumerateObject()){if(string.Equals(property.Name,name,StringComparison.OrdinalIgnoreCase)){return property.Value;}}
        return null;
    }
    private static JsonElement? Array(JsonElement value,string name){var item=Property(value,name);if(item is null||item.Value.ValueKind==JsonValueKind.Null){return null;}if(item.Value.ValueKind!=JsonValueKind.Array){throw new JsonException();}return item;}
    private static int? Number(JsonElement value,string name){var item=Property(value,name);if(item is null||item.Value.ValueKind==JsonValueKind.Null){return null;}if(item.Value.ValueKind!=JsonValueKind.Number||!item.Value.TryGetInt32(out var number)||number<0){throw new JsonException();}return number;}
    private static bool? Boolean(JsonElement value,string name){var item=Property(value,name);if(item is null||item.Value.ValueKind==JsonValueKind.Null){return null;}if(item.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)){throw new JsonException();}return item.Value.GetBoolean();}
    private static string? Text(JsonElement value,string name){var item=Property(value,name);if(item is null||item.Value.ValueKind==JsonValueKind.Null){return null;}if(item.Value.ValueKind!=JsonValueKind.String){throw new JsonException();}return Bound(item.Value.GetString()!);}
    private static string Bound(string value){if(value.Length>65536){throw new JsonException();}return value;}
    private static string Layer(JsonElement value){
        var item=Property(value,"layer");
        if(item is null){return "Unknown";}
        if(item.Value.ValueKind==JsonValueKind.Number){if(!item.Value.TryGetInt32(out var number)){throw new JsonException();}return number switch{0=>"Syntax",1=>"Pipeline",_=>"Unknown"};}
        return Text(value,"layer") switch{"Syntax"=>"Syntax","Pipeline"=>"Pipeline",_=>"Unknown"};
    }
    private static string Termination(JsonElement value){
        var item=Property(value,"termination");
        if(item is null){return "Unknown";}
        if(item.Value.ValueKind==JsonValueKind.Number){if(!item.Value.TryGetInt32(out var number)){throw new JsonException();}return number switch{0=>"FixedPoint",1=>"CycleDetected",2=>"IterationLimit",3=>"RebindFailed",_=>"Unknown"};}
        return Text(value,"termination") switch{"FixedPoint"=>"FixedPoint","CycleDetected"=>"CycleDetected","IterationLimit"=>"IterationLimit","RebindFailed"=>"RebindFailed",_=>"Unknown"};
    }
}
