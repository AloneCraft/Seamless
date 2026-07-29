using System.Text.Json;

namespace Benchmark.Dynamic;

public enum 動的最適化Trace表示種別{
    Document,
    Layer,
    Round,
    Event,
    Error,
}

public sealed record 動的最適化Trace表示行(
    動的最適化Trace表示種別 種別,
    int 階層,
    string 見出し,
    string 詳細,
    string Before,
    string After){
    public Thickness 左余白=>new(this.階層*18d,3d,3d,3d);
    public bool 差分あり=>this.種別==動的最適化Trace表示種別.Event
        &&(!string.IsNullOrEmpty(this.Before)||!string.IsNullOrEmpty(this.After));
}

public static class 動的最適化Trace表示Parser{
    public static IReadOnlyList<動的最適化Trace表示行> 解析(string? traceJson){
        if(string.IsNullOrWhiteSpace(traceJson)){
            return[];
        }
        var rows=new List<動的最適化Trace表示行>();
        try{
            using var outer=JsonDocument.Parse(traceJson);
            if(outer.RootElement.ValueKind!=JsonValueKind.Array){
                return[Error("Traceの外側JSONは配列ではありません。")];
            }
            var documentIndex=0;
            foreach(var item in outer.RootElement.EnumerateArray()){
                documentIndex++;
                var innerJson=item.ValueKind==JsonValueKind.String?item.GetString():item.GetRawText();
                if(string.IsNullOrWhiteSpace(innerJson)){
                    rows.Add(Error($"Trace document {documentIndex} は空です。"));
                    continue;
                }
                try{
                    using var inner=JsonDocument.Parse(innerJson);
                    AppendDocument(rows,inner.RootElement,documentIndex);
                }catch(JsonException exception){
                    rows.Add(Error($"Trace document {documentIndex}: {exception.Message}"));
                }
            }
        }catch(JsonException exception){
            rows.Add(Error(exception.Message));
        }
        return rows;
    }

    private static void AppendDocument(List<動的最適化Trace表示行> rows,JsonElement root,int documentIndex){
        var method=String(root,"MethodKey",$"document-{documentIndex}");
        var schema=Int32(root,"SchemaVersion");
        var summary=root.TryGetProperty("Summary",out var summaryElement)?summaryElement:default;
        var rewriteCount=Int32(summaryElement,"RewriteCount");
        var passCount=Int32(summaryElement,"OptimizationPassCount");
        var fixedPoint=Boolean(summaryElement,"ReachedFixedPoint");
        rows.Add(new(
            動的最適化Trace表示種別.Document,
            0,
            $"Method: {method}",
            $"schema={schema} / rewrite={rewriteCount} / pass={passCount} / fixedPoint={fixedPoint}",
            string.Empty,
            string.Empty));

        var events=root.TryGetProperty("Events",out var eventsElement)&&eventsElement.ValueKind==JsonValueKind.Array
            ?eventsElement.EnumerateArray().Select(static item=>TraceEvent.Parse(item)).ToArray()
            :[];
        var terminations=root.TryGetProperty("LayerTerminations",out var terminationElement)
            &&terminationElement.ValueKind==JsonValueKind.Array
                ?terminationElement.EnumerateArray().Select(static item=>TraceTermination.Parse(item)).ToArray()
                :[];
        var layers=events.Select(static item=>item.Layer)
            .Concat(terminations.Select(static item=>item.Layer))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        foreach(var layer in layers){
            var layerEvents=events.Where(item=>item.Layer==layer).OrderBy(static item=>item.Sequence).ToArray();
            var layerTerminations=terminations.Where(item=>item.Layer==layer).OrderBy(static item=>item.RunId).ToArray();
            var terminationText=layerTerminations.Length==0
                ?"termination=unknown"
                :string.Join(", ",layerTerminations.Select(static item=>$"run {item.RunId}: {item.Termination} ({item.RoundCount} rounds)"));
            rows.Add(new(
                動的最適化Trace表示種別.Layer,
                1,
                $"Layer: {layer}",
                $"events={layerEvents.Length} / {terminationText}",
                string.Empty,
                string.Empty));
            foreach(var run in layerEvents.Select(static item=>item.RunId)
                .Concat(layerTerminations.Select(static item=>item.RunId))
                .Distinct()
                .Order()){
                var runEvents=layerEvents.Where(item=>item.RunId==run).ToArray();
                var rounds=runEvents.Select(static item=>item.Round).Distinct().Order().ToArray();
                foreach(var round in rounds){
                    var roundEvents=runEvents.Where(item=>item.Round==round).OrderBy(static item=>item.Sequence).ToArray();
                    rows.Add(new(
                        動的最適化Trace表示種別.Round,
                        2,
                        $"Run {run} / Round {round}",
                        $"events={roundEvents.Length}",
                        string.Empty,
                        string.Empty));
                    foreach(var traceEvent in roundEvents){
                        var relation=string.Join(",",new[]{
                            traceEvent.Left?"left":null,
                            traceEvent.Right?"right":null,
                            traceEvent.Inner?"inner":null,
                            traceEvent.Revisited?"revisited":null,
                        }.Where(static item=>item is not null));
                        var position=traceEvent.Depth is { }depth?$"depth={depth}":$"stage={traceEvent.StageIndex}";
                        rows.Add(new(
                            動的最適化Trace表示種別.Event,
                            3,
                            $"#{traceEvent.Sequence} {traceEvent.Phase} / {traceEvent.RuleId}",
                            $"{traceEvent.NodePath} / {position} / pass={traceEvent.Pass} / visit={traceEvent.VisitNumber}"
                                +(relation.Length==0?string.Empty:$" / {relation}"),
                            traceEvent.Before,
                            traceEvent.After));
                    }
                }
            }
        }
    }

    private static 動的最適化Trace表示行 Error(string message){
        return new(動的最適化Trace表示種別.Error,0,"Trace parse error",message,string.Empty,string.Empty);
    }

    private static string String(JsonElement element,string property,string fallback=""){
        return element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(property,out var value)
            ?value.GetString()??fallback
            :fallback;
    }

    private static int Int32(JsonElement element,string property){
        return element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(property,out var value)
            &&value.TryGetInt32(out var parsed)?parsed:0;
    }

    private static int? NullableInt32(JsonElement element,string property){
        return element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(property,out var value)
            &&value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out var parsed)?parsed:null;
    }

    private static bool Boolean(JsonElement element,string property){
        return element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(property,out var value)
            &&value.ValueKind==JsonValueKind.True;
    }

    private sealed record TraceTermination(int RunId,string Layer,int RoundCount,string Termination){
        public static TraceTermination Parse(JsonElement element){
            return new(
                Int32(element,"RunId"),
                String(element,"Layer","Unknown"),
                Int32(element,"RoundCount"),
                String(element,"Termination","Unknown"));
        }
    }

    private sealed record TraceEvent(
        int Sequence,
        int RunId,
        string Layer,
        string Phase,
        string RuleId,
        int Round,
        int Pass,
        int VisitNumber,
        string NodePath,
        int? Depth,
        int? StageIndex,
        bool Left,
        bool Right,
        bool Inner,
        bool Revisited,
        string Before,
        string After){
        public static TraceEvent Parse(JsonElement element){
            return new(
                Int32(element,"Sequence"),
                Int32(element,"RunId"),
                String(element,"Layer","Unknown"),
                String(element,"Phase"),
                String(element,"RuleId"),
                Int32(element,"Round"),
                Int32(element,"Pass"),
                Int32(element,"VisitNumber"),
                String(element,"NodePath","/"),
                NullableInt32(element,"Depth"),
                NullableInt32(element,"StageIndex"),
                Boolean(element,"Left"),
                Boolean(element,"Right"),
                Boolean(element,"Inner"),
                Boolean(element,"Revisited"),
                String(element,"Before"),
                String(element,"After"));
        }
    }
}
