using System.Text;

namespace Seamless.FLinq.Linq.Fragments;

/*
TsavoriteHashSetScanTerminal は TsavoriteHashSetRoot + Where/Select/AsEnumerable + Count/Any/First の形を
HashSet 内部 scan へ direct execution する専用終端。

入力例:
  tsavoriteSet.Where(x => x > threshold).Select(x => x + 1).First()

生成例:
  var source = originalHashSetExpression;
  var state = new ScanState(captures...);
  ScanInt32(source, itemObject => ScanBody(ref state, itemObject));
  result = terminalKind switch {
      Count => state.Count,
      Any => state.Found,
      First => state.Found ? state.Value : throw NoElements()
  };

主な制約:
  - root の要素型は int。
  - stage は Where/Select/AsEnumerable のみ。
  - terminal は Count/Any/First のみ。
  - statement inline が必要な FuncFragment は対象外。

設計意図:
  通常 enumerator を作らず UnsafeAccessor で Tsavorite の scan API へ入り、body delegate 内で pipeline stage を順に評価する。
*/
public sealed class TsavoriteHashSetScanTerminal(
    int 番号,
    PipelineEnumerable pipeline,
    PipelineTerminalKind terminalKind)
    : Scalar(番号),DirectExecutionFragment {
    private TsavoriteHashSetRoot Root => (TsavoriteHashSetRoot)pipeline.Source;
    private string StateTypeName => $"{this.Struct}_TsavoriteHashSetScanState";
    private string BodyMethodName => $"{this.Struct}_TsavoriteHashSetScanBody";
    private string AccessorMethodName => $"{this.Struct}_TsavoriteHashSetScan";

    protected override Enumerable PrimarySource => pipeline;

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        PipelineTerminalKind terminalKind,
        out TsavoriteHashSetScanTerminal terminal) {
        terminal=null!;
        if(pipeline.Source is not TsavoriteHashSetRoot root) {
            return false;
        }        if(terminalKind is not (PipelineTerminalKind.Count or PipelineTerminalKind.Any or PipelineTerminalKind.First)) {
            return false;
        }
        if(pipeline.Stages.Any(static stage => stage.Kind is not (PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.AsEnumerable))) {
            return false;
        }
        if(pipeline.Stages.Any(static stage => stage.Func is { RequiresStatementInlining: true })) {
            return false;
        }

        terminal=new TsavoriteHashSetScanTerminal(number,pipeline,terminalKind);
        return true;
    }

    public override string StructMethod {
        get {
            var builder = new StringBuilder();
            foreach(var func in this.Funcs()) {
                AppendMember(builder,this.FuncSupport(func,"Scan"));
            }
            AppendMember(builder,this.StateStructSource());
            AppendMember(builder,this.BodyMethodSource());
            AppendMember(builder,this.UnsafeAccessorSource());
            return builder.ToString().TrimEnd();
        }
    }

    public override string GetEnumeratorStruct => string.Empty;
    public override string GetTryGetNextBody => string.Empty;
    public override string GetCurrentExpression => string.Empty;

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var sourceName=$"__tsavoriteScanSource{番号}";
        var stateName=$"__tsavoriteScanState{番号}";
        var builder = new StringBuilder();
        builder.AppendLine($"var {sourceName}={this.Root.SourceExpression};");
        builder.AppendLine($"var {stateName}=new {this.StateTypeName}[] {{ new {this.StateTypeName}({this.ClosureArgumentList()}) }};");
        builder.AppendLine($"{this.AccessorMethodName}<{this.Root.ElementType}>(null,{sourceName},itemObject=>{this.BodyMethodName}(ref {stateName}[0],itemObject));");
        if(terminalKind==PipelineTerminalKind.Count) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}[0].Count;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}[0].Found;");
        } else {
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({stateName}[0].Found) {{");
            builder.AppendLine($"    {actualVariableName}=({actualTypeName}){stateName}[0].Value!;");
            builder.AppendLine("} else {");
            builder.AppendLine("    throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("}");
        }
        return builder.ToString().TrimEnd();
    }

    private string StateStructSource() {
        var builder = new StringBuilder();
        builder.AppendLine($"private struct {this.StateTypeName} {{");
        foreach(var capture in this.CapturedLocals()) {
            builder.AppendLine($"    public {capture.TypeName} {capture.Name};");
        }
        foreach(var hoistedLocal in this.HoistedLocals()) {
            builder.AppendLine($"    public {hoistedLocal.TypeName} {hoistedLocal.Name};");
        }
        foreach(var func in this.DelegateFuncs()) {
            builder.AppendLine($"    public {func.DelegateTypeName} {func.DelegateFieldName};");
        }
        foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index)=>(stage,index))) {
            if(stage.UsesIndex) {
                builder.AppendLine($"    public int {this.IndexFieldName(index)};");
            }
        }
        builder.AppendLine("    public int Count;");
        builder.AppendLine("    public bool Found;");
        builder.AppendLine($"    public {pipeline.ElementType} Value;");
        var parameters = this.ClosureParameters().ToArray();
        if(parameters.Length>0) {
            builder.AppendLine($"    public {this.StateTypeName}({string.Join(", ",parameters.Select(static p=>$"{p.TypeName} {p.Name}"))}) {{");
            foreach(var parameter in parameters) {
                builder.AppendLine($"        this.{parameter.Name}={parameter.Name};");
            }
            foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index)=>(stage,index))) {
                if(stage.UsesIndex) {
                    builder.AppendLine($"        this.{this.IndexFieldName(index)}=-1;");
                }
            }
            builder.AppendLine("        this.Count=0;");
            builder.AppendLine("        this.Found=false;");
            builder.AppendLine("        this.Value=default!;");
            builder.AppendLine("    }");
        } else if(pipeline.Stages.Any(static stage=>stage.UsesIndex)) {
            builder.AppendLine($"    public {this.StateTypeName}() {{");
            foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index)=>(stage,index))) {
                if(stage.UsesIndex) {
                    builder.AppendLine($"        this.{this.IndexFieldName(index)}=-1;");
                }
            }
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string BodyMethodSource() {
        var builder = new StringBuilder();
        builder.AppendLine($"{AggressiveInliningAttribute}");
        builder.AppendLine($"private static bool {this.BodyMethodName}(ref {this.StateTypeName} state,object? itemObject) {{");
        foreach(var capture in this.CapturedLocals()) {
            builder.AppendLine($"    var {capture.Name}=state.{capture.Name};");
        }
        foreach(var hoistedLocal in this.HoistedLocals()) {
            builder.AppendLine($"    var {hoistedLocal.Name}=state.{hoistedLocal.Name};");
        }
        foreach(var func in this.DelegateFuncs()) {
            builder.AppendLine($"    var {func.DelegateFieldName}=state.{func.DelegateFieldName};");
        }
        builder.AppendLine($"    var current0=({this.Root.ElementType})itemObject!;");
        var currentName = "current0";
        var currentType = this.Root.ElementType;
        var currentIndex = 0;
        foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index)=>(stage,index))) {
            if(stage.Kind==PipelineStageKind.AsEnumerable) {
                continue;
            }
            if(stage.UsesIndex) {
                builder.AppendLine($"    checked {{ state.{this.IndexFieldName(index)}++; }}");
            }
            if(stage.Kind==PipelineStageKind.Where) {
                var tempName=$"where{index}";
                builder.AppendLine($"    bool {tempName}=default!;");
                var args = stage.UsesIndex
                    ? new[] { currentName,$"state.{this.IndexFieldName(index)}" }
                    : new[] { currentName };
                AppendIndented(builder,this.FuncAssignStatic(stage.Func!,"Where",tempName,args),"    ");
                builder.AppendLine($"    if(!{tempName}) return true;");
                continue;
            }
            if(stage.Kind==PipelineStageKind.Select) {
                var nextName=$"current{++currentIndex}";
                builder.AppendLine($"    {stage.OutputType} {nextName}=default!;");
                var args = stage.UsesIndex
                    ? new[] { currentName,$"state.{this.IndexFieldName(index)}" }
                    : new[] { currentName };
                AppendIndented(builder,this.FuncAssignStatic(stage.Func!,"Select",nextName,args),"    ");
                currentName=nextName;
                currentType=stage.OutputType;
            }
        }
        if(terminalKind==PipelineTerminalKind.Count) {
            builder.AppendLine("    checked { state.Count++; }");
            builder.AppendLine("    return true;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine("    state.Found=true;");
            builder.AppendLine("    return false;");
        } else {
            builder.AppendLine($"    state.Value=({pipeline.ElementType}){currentName};");
            builder.AppendLine("    state.Found=true;");
            builder.AppendLine("    return false;");
        }
        builder.AppendLine("}");
        _=currentType;
        return builder.ToString().TrimEnd();
    }

    private string UnsafeAccessorSource() => $$"""
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.StaticMethod, Name = "{{this.UnsafeAccessorMethodName()}}")]
        private static extern void {{this.AccessorMethodName}}<TElement>([global::System.Runtime.CompilerServices.UnsafeAccessorType("Seamless.Tsavorite.Collections.HashSetScanUnsafe, Seamless")] object? _,global::Seamless.Tsavorite.Collections.HashSet<TElement> source,global::System.Func<object?,bool> body);
        """;

    private string UnsafeAccessorMethodName() => "Scan";

    private IEnumerable<FuncFragment> Funcs() =>
        pipeline.Stages.Select(static stage=>stage.Func).Where(static func=>func is not null).Select(static func=>func!);

    private IEnumerable<(string Name,string TypeName)> CapturedLocals() {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in this.Funcs()) {
            foreach(var capture in func.CapturedLocals) {
                if(names.Add(capture.Name)) {
                    yield return capture;
                }
            }
        }
    }

    private IEnumerable<HoistedLocal> HoistedLocals() {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in this.Funcs()) {
            foreach(var hoistedLocal in func.HoistedLocals) {
                if(names.Add(hoistedLocal.Name)) {
                    yield return hoistedLocal;
                }
            }
        }
    }

    private IEnumerable<FuncFragment> DelegateFuncs() {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var func in this.Funcs()) {
            if(func.CanInvokeAsDelegate && func.DelegateFieldName is { } name && names.Add(name)) {
                yield return func;
            }
        }
    }

    private IEnumerable<(string Name,string TypeName)> ClosureParameters() =>
        this.CapturedLocals()
            .Concat(this.HoistedLocals().Select(static hoistedLocal=>(hoistedLocal.Name,hoistedLocal.TypeName)))
            .Concat(this.DelegateFuncs().Select(static func=>(func.DelegateFieldName!,func.DelegateTypeName!)));

    private string ClosureArgumentList() {
        var args = this.CapturedLocals().Select(static capture=>capture.Name)
            .Concat(this.HoistedLocals().Select(static hoistedLocal=>hoistedLocal.ExpressionText))
            .Concat(this.DelegateFuncs().Select(static func=>func.DelegateConstructorArg!))
            .ToArray();
        return string.Join(", ",args);
    }

    private string IndexFieldName(int index) => $"Index{index}";

    private static void AppendMember(StringBuilder builder,string source) {
        if(string.IsNullOrWhiteSpace(source)) {
            return;
        }
        if(builder.Length>0) {
            builder.AppendLine();
        }
        builder.AppendLine(source.TrimEnd());
    }

    private static void AppendIndented(StringBuilder builder,string source,string indent) {
        if(string.IsNullOrWhiteSpace(source)) {
            return;
        }
        foreach(var line in source.Replace("\r\n","\n").Split('\n')) {
            if(line.Length==0) {
                builder.AppendLine();
            } else {
                builder.Append(indent).AppendLine(line);
            }
        }
    }
}
