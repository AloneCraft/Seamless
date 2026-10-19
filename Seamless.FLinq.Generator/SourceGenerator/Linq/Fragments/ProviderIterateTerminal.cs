namespace Seamless.FLinq.Generator;

// ここに置いてある ProviderIterateTerminalKind / ProviderNativeExecutionBase は
// このファイルの ProviderIterateTerminal 専用で、他に消費者がいない。
// AI_SYNTAX_RULES の File Layout Rules（実質 1 系列専用の基底は本体ファイルへ集約する）に従い
// Bases/ProviderIterateTerminalBase.cs から移してきた。

internal enum ProviderIterateTerminalKind {
    ToList,
    ToArray,
    Count,
    Any,
    First,
    ToDictionary,
    Sum,
    All,
    FirstOrDefault,
    Contains,
}

internal abstract class ProviderNativeExecutionBase(
    int                         number,
    PipelineEnumerable          pipeline,
    ProviderIterateTerminalKind terminalKind,
    string                      providerName,
    string                      sourceVariablePrefix,
    string                      sourceExpression,
    string                      sourceElementType,
    string                      strategyInterfaceTypeName,
    bool                        sourceHasCount,
    string?                     scanCountMethodName,
    FuncFragment?               terminalPredicate  =null,
    string?                     containsValue      =null,
    string?                     containsComparer   =null,
    string                      operationName      ="Iterate",
    string                      executionMethodName="Iterate",
    FuncFragment?               terminalFunc1      =null,
    FuncFragment?               terminalFunc2      =null,
    string?                     terminalComparer   =null)
    :Scalar(number),DirectExecutionFragment {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
            yield return child;
        }
        if(pipeline is { } __visitorChild_pipeline) {
            yield return __visitorChild_pipeline;
        }
    }

    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(terminalPredicate is { } __visitorChild_terminalPredicate) {
            yield return __visitorChild_terminalPredicate;
        }
        if(terminalFunc1 is { } __visitorChild_terminalFunc1) {
            yield return __visitorChild_terminalFunc1;
        }
        if(terminalFunc2 is { } __visitorChild_terminalFunc2) {
            yield return __visitorChild_terminalFunc2;
        }
    }

    private readonly   int        number          =number;
    private string StateTypeBaseName => $"{this.Struct}_{providerName}{operationName}State";
    private string StateTypeName => this.OuterTypeParameters.Length==0
        ? this.StateTypeBaseName
        : $"{this.StateTypeBaseName}<{string.Join(", ",this.OuterTypeParameters)}>";
    protected override Enumerable PrimarySource => pipeline;

    protected static bool CanCreateFor(PipelineEnumerable pipeline,PipelineTerminalKind terminalKind,string? containsValue = null) =>
        (terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToHashSet
            or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.All or PipelineTerminalKind.First
            or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum
            ||(terminalKind==PipelineTerminalKind.Contains&&containsValue is not null))
        &&pipeline.Stages.All(static stage => PipelineStageCapabilityTable.SupportsProviderNativeIterate(stage.Kind))
        &&!pipeline.Stages.Any(static stage => stage.Func is { RequiresStatementInlining: true });

    protected static bool CanCreateFor(
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        FuncFragment? terminalPredicate = null,
        string? containsValue = null) =>
        terminalKind switch {
            ProviderIterateTerminalKind.All => terminalPredicate is not null,
            ProviderIterateTerminalKind.Contains => containsValue is not null,
            // 段（Where 等）が無い素の Count は要素を1つも見なくても NonEnumeratedCountExpression
            // （Set.Count 等の O(1) プロパティ）で答えられる。native iterate は全要素を
            // materialize してしまうため、既に O(1) 経路がある場合はそちらへ譲る。
            ProviderIterateTerminalKind.Count => pipeline.NonEnumeratedCountExpression is null,
            ProviderIterateTerminalKind.Any => pipeline.Stages.Any(static stage => stage.Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile),
            _ => true,
        }
        &&pipeline.Stages.All(static stage => PipelineStageCapabilityTable.SupportsProviderNativeIterate(stage.Kind))
        &&!pipeline.Stages.Any(static stage => stage.Func is { RequiresStatementInlining: true })
        &&terminalPredicate is not { RequiresStatementInlining: true };

    protected static ProviderIterateTerminalKind ToProviderIterateTerminalKind(PipelineTerminalKind terminalKind) =>
        terminalKind switch {
            PipelineTerminalKind.Count => ProviderIterateTerminalKind.Count,
            PipelineTerminalKind.LongCount => ProviderIterateTerminalKind.Count,
            PipelineTerminalKind.Any => ProviderIterateTerminalKind.Any,
            PipelineTerminalKind.All => ProviderIterateTerminalKind.All,
            PipelineTerminalKind.Contains => ProviderIterateTerminalKind.Contains,
            PipelineTerminalKind.First => ProviderIterateTerminalKind.First,
            PipelineTerminalKind.ToList => ProviderIterateTerminalKind.ToList,
            PipelineTerminalKind.ToArray => ProviderIterateTerminalKind.ToArray,
            PipelineTerminalKind.ToDictionary => ProviderIterateTerminalKind.ToDictionary,
            PipelineTerminalKind.Sum => ProviderIterateTerminalKind.Sum,
            _ => throw new NotSupportedException($"{terminalKind} does not support provider native scan."),
        };

    public override string StructMethod {
        get {
            if(this.UsesScanCount) {
                return string.Empty;
            }
            var builder = new StringBuilder();
            foreach(var func in this.Funcs()) {
                GeneratedCodeWriter.AppendMember(builder,this.FuncSupport(func,operationName));
            }
            GeneratedCodeWriter.AppendMember(builder,this.StateStructSource());
            return builder.ToString().TrimEnd();
        }
    }

    public override string GetEnumeratorStruct => string.Empty;
    public override string GetTryGetNextBody => string.Empty;

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var sourceName = $"{sourceVariablePrefix}{this.number}";
        var stateName = $"__{providerName}{operationName}State{this.number}";
        var builder = new StringBuilder();
        builder.AppendLine($"var {sourceName}={sourceExpression};");
        if(this.UsesScanCount) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName})checked((int)({sourceName}.{scanCountMethodName}()));");
            return builder.ToString().TrimEnd();
        }
        builder.AppendLine($"var {stateName}=new {this.StateTypeName}({this.ClosureArgumentList()});");
        if(sourceHasCount) {
            if(terminalKind==ProviderIterateTerminalKind.ToList) {
                builder.AppendLine($"{stateName}.List=new global::System.Collections.Generic.List<{pipeline.ElementType}>({sourceName}.Count);");
            } else if(terminalKind==ProviderIterateTerminalKind.ToArray) {
                builder.AppendLine($"{stateName}.Array=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({sourceName}.Count);");
            } else if(terminalKind==ProviderIterateTerminalKind.ToDictionary) {
                builder.AppendLine($"{stateName}.Dictionary=new({sourceName}.Count,(global::System.Collections.Generic.IEqualityComparer<{this.DictionaryKeyType}>?){terminalComparer??"null"});");
            }
        } else {
            // 件数を持たない根(Tsavorite のマルチ用など)は先取りせず、集めながら伸ばす。
            if(terminalKind==ProviderIterateTerminalKind.ToList) {
                builder.AppendLine($"{stateName}.List=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
            } else if(terminalKind==ProviderIterateTerminalKind.ToArray) {
                builder.AppendLine($"{stateName}.Array=global::System.Array.Empty<{pipeline.ElementType}>();");
            } else if(terminalKind==ProviderIterateTerminalKind.ToDictionary) {
                builder.AppendLine($"{stateName}.Dictionary=new((global::System.Collections.Generic.IEqualityComparer<{this.DictionaryKeyType}>?){terminalComparer??"null"});");
            }
        }
        if(containsValue is not null) {
            builder.AppendLine($"{stateName}.ContainsValue={containsValue};");
            if(containsComparer is not null) {
                builder.AppendLine($"{stateName}.ContainsComparer={containsComparer};");
            }
        }
        builder.AppendLine($"{sourceName}.{executionMethodName}(ref {stateName});");
        if(terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.List!;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"if({stateName}.Count!={stateName}.Array!.Length) global::System.Array.Resize(ref {stateName}.Array,{stateName}.Count);");
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Array!;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Dictionary!;");
        } else if(terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Sum;");
        } else if(terminalKind==ProviderIterateTerminalKind.Count) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Count;");
        } else if(terminalKind is ProviderIterateTerminalKind.Any or ProviderIterateTerminalKind.Contains) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Found;");
        } else if(terminalKind==ProviderIterateTerminalKind.All) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName})(!{stateName}.Found);");
        } else if(terminalKind==ProviderIterateTerminalKind.First) {
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({stateName}.Found) {{");
            builder.AppendLine($"    {actualVariableName}=({actualTypeName}){stateName}.Value!;");
            builder.AppendLine("} else {");
            builder.AppendLine("    throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("}");
        } else {
            builder.AppendLine($"{actualTypeName} {actualVariableName}={stateName}.Found ? ({actualTypeName}){stateName}.Value! : default!;");
        }
        return builder.ToString().TrimEnd();
    }

    // 段も条件も無い素の Count を、要素を取り出さずに数える provider の走査(ScanCount)へ任せる。
    // 段(Select 等)があると、System.Linq と同じく各要素で関数を呼ぶ必要があるので任せない。
    private bool UsesScanCount =>
        terminalKind==ProviderIterateTerminalKind.Count
        &&scanCountMethodName is not null
        &&terminalPredicate is null
        &&pipeline.Stages.All(static stage => stage.Kind==PipelineStageKind.AsEnumerable);

    private string StateStructSource() {
        var builder = new StringBuilder();
        builder.AppendLine($"private struct {this.StateTypeName} : {strategyInterfaceTypeName}<{this.StateTypeName}, {sourceElementType}> {{");
        foreach(var capture in this.CapturedLocals()) {
            builder.AppendLine($"    public {capture.TypeName} {capture.Name};");
        }
        foreach(var hoistedLocal in this.HoistedLocals()) {
            builder.AppendLine($"    public {hoistedLocal.TypeName} {hoistedLocal.Name};");
        }
        foreach(var func in this.DelegateFuncs()) {
            builder.AppendLine($"    public {func.DelegateTypeName} {func.DelegateFieldName};");
        }
        foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index) => (stage,index))) {
            if(stage.UsesIndex) {
                builder.AppendLine($"    public int {this.IndexFieldName(index)};");
            }
        }
        builder.AppendLine("    public int Count;");
        builder.AppendLine("    public bool Found;");
        builder.AppendLine($"    public {pipeline.ElementType} Value;");
        if(terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"    public global::System.Collections.Generic.List<{pipeline.ElementType}>? List;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"    public {pipeline.ElementType}[]? Array;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"    public global::System.Collections.Generic.Dictionary<{this.DictionaryKeyType},{this.DictionaryValueType}>? Dictionary;");
        } else if(terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"    public {pipeline.ElementType} Sum;");
        }
        if(containsValue is not null) {
            builder.AppendLine($"    public {pipeline.ElementType} ContainsValue;");
            builder.AppendLine($"    public global::System.Collections.Generic.IEqualityComparer<{pipeline.ElementType}>? ContainsComparer;");
        }
        var parameters = this.ClosureParameters().ToArray();
        if(parameters.Length>0) {
            builder.AppendLine($"    public {this.StateTypeBaseName}({string.Join(", ",parameters.Select(static p => $"{p.TypeName} {p.Name}"))}) {{");
            foreach(var parameter in parameters) {
                builder.AppendLine($"        this.{parameter.Name}={parameter.Name};");
            }
            foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index) => (stage,index))) {
                if(stage.UsesIndex) {
                    builder.AppendLine($"        this.{this.IndexFieldName(index)}=-1;");
                }
            }
            builder.AppendLine("        this.Count=0;");
            builder.AppendLine("        this.Found=false;");
            builder.AppendLine("        this.Value=default!;");
            builder.AppendLine("    }");
        } else if(pipeline.Stages.Any(static stage => stage.UsesIndex)) {
            builder.AppendLine($"    public {this.StateTypeBaseName}() {{");
            foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index) => (stage,index))) {
                if(stage.UsesIndex) {
                    builder.AppendLine($"        this.{this.IndexFieldName(index)}=-1;");
                }
            }
            builder.AppendLine("    }");
        }
        GeneratedCodeWriter.AppendMember(builder,this.InvokeMethodSource());
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string InvokeMethodSource() {
        var builder = new StringBuilder();
        builder.AppendLine($"{AggressiveInliningAttribute}");
        builder.AppendLine($"public static bool Invoke(ref {this.StateTypeName} state,{sourceElementType} item) {{");
        foreach(var capture in this.CapturedLocals()) {
            builder.AppendLine($"    var {capture.Name}=state.{capture.Name};");
        }
        foreach(var hoistedLocal in this.HoistedLocals()) {
            builder.AppendLine($"    var {hoistedLocal.Name}=state.{hoistedLocal.Name};");
        }
        foreach(var func in this.DelegateFuncs()) {
            builder.AppendLine($"    var {func.DelegateFieldName}=state.{func.DelegateFieldName};");
        }
        builder.AppendLine("    var current0=item;");
        var currentName = "current0";
        var currentIndex = 0;
        foreach(var (stage,index) in pipeline.Stages.Select(static (stage,index) => (stage,index))) {
            if(stage.Kind==PipelineStageKind.AsEnumerable) {
                continue;
            }
            if(stage.UsesIndex) {
                builder.AppendLine($"    checked {{ state.{this.IndexFieldName(index)}++; }}");
            }
            if(stage.Kind==PipelineStageKind.Where) {
                var tempName = $"where{index}";
                builder.AppendLine($"    bool {tempName}=default!;");
                var args = stage.UsesIndex
                    ? new[] { currentName,$"state.{this.IndexFieldName(index)}" }
                    : new[] { currentName };
                GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(stage.Func!,"Where",tempName,args),"    ");
                builder.AppendLine($"    if(!{tempName}) return true;");
                continue;
            }
            if(stage.Kind==PipelineStageKind.Select) {
                var nextName = $"current{++currentIndex}";
                builder.AppendLine($"    {stage.OutputType} {nextName}=default!;");
                var args = stage.UsesIndex
                    ? new[] { currentName,$"state.{this.IndexFieldName(index)}" }
                    : new[] { currentName };
                GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(stage.Func!,"Select",nextName,args),"    ");
                currentName=nextName;
                continue;
            }
            // Cast/OfType は順序に依存しないので unordered な provider scan へ載せられる。
            // Invoke は per-item コールバックで戻り値が「走査を続けるか」なので、
            // OfType のスキップは continue ではなく return true になる（Where と同形）。
            // 入出力が同型なら変換も型判定も不要（OfType<int> を int 列に適用した場合の
            // `is not int` は「絶対に一致しないパターン」として CS8518 になる）。
            if(stage.Kind is PipelineStageKind.Cast or PipelineStageKind.OfType) {
                if(stage.InputType==stage.OutputType) {
                    continue;
                }
                var nextName = $"current{++currentIndex}";
                if(stage.Kind==PipelineStageKind.Cast) {
                    builder.AppendLine($"    var {nextName}=({stage.OutputType})(object?){currentName};");
                } else {
                    builder.AppendLine($"    if({currentName} is not {stage.OutputType} {nextName}) return true;");
                }
                currentName=nextName;
                continue;
            }
        }
        if(terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"    state.List!.Add(({pipeline.ElementType}){currentName});");
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToArray) {
            if(!sourceHasCount) {
                builder.AppendLine("    if(state.Count==state.Array!.Length) global::Seamless.Linq.GeneratedCodeHelpers.GrowArray(ref state.Array);");
            }
            builder.AppendLine($"    state.Array![state.Count++]=({pipeline.ElementType}){currentName};");
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"    {this.DictionaryKeyType} dictionaryKey=default!;");
            GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(terminalFunc1!,"ToDictionary","dictionaryKey",new[] { currentName }),"    ");
            if(terminalFunc2 is not null) {
                builder.AppendLine($"    {this.DictionaryValueType} dictionaryValue=default!;");
                GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(terminalFunc2,"ToDictionary","dictionaryValue",new[] { currentName }),"    ");
                builder.AppendLine("    state.Dictionary!.Add(dictionaryKey,dictionaryValue);");
            } else {
                builder.AppendLine($"    state.Dictionary!.Add(dictionaryKey,({this.DictionaryValueType})(object?){currentName}!);");
            }
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"    state.Sum+=({pipeline.ElementType}){currentName};");
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.All) {
            var tempName = "all";
            builder.AppendLine($"    bool {tempName}=default!;");
            var args = new[] { currentName };
            GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(terminalPredicate!,"All",tempName,args),"    ");
            builder.AppendLine($"    if(!{tempName}) {{");
            builder.AppendLine("        state.Found=true;");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.Contains) {
            builder.AppendLine("    var comparer=state.ContainsComparer ?? global::System.Collections.Generic.EqualityComparer<"+pipeline.ElementType+">.Default;");
            builder.AppendLine($"    if(comparer.Equals(({pipeline.ElementType}){currentName},state.ContainsValue)) {{");
            builder.AppendLine("        state.Found=true;");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.Count) {
            builder.AppendLine("    checked { state.Count++; }");
            builder.AppendLine("    return true;");
        } else if(terminalKind==ProviderIterateTerminalKind.Any) {
            builder.AppendLine("    state.Found=true;");
            builder.AppendLine("    return false;");
        } else {
            builder.AppendLine($"    state.Value=({pipeline.ElementType}){currentName};");
            builder.AppendLine("    state.Found=true;");
            builder.AppendLine("    return false;");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private IEnumerable<FuncFragment> Funcs() =>
        pipeline.Stages.Select(static stage => stage.Func)
            .Where(static func => func is not null)
            .Select(static func => func!)
            .Concat(terminalPredicate is null ? global::System.Linq.Enumerable.Empty<FuncFragment>() : new[] { terminalPredicate })
            .Concat(terminalFunc1 is null ? global::System.Linq.Enumerable.Empty<FuncFragment>() : new[] { terminalFunc1 })
            .Concat(terminalFunc2 is null ? global::System.Linq.Enumerable.Empty<FuncFragment>() : new[] { terminalFunc2 });

    private string DictionaryKeyType => terminalFunc1?.ReturnTypeName??"object";
    private string DictionaryValueType => terminalFunc2?.ReturnTypeName??pipeline.ElementType;

    private ClosureRenderPlan ClosurePlan() => ClosureRenderPlan.Create(this.Funcs().ToArray());

    private IEnumerable<(string Name,string TypeName)> CapturedLocals() => this.ClosurePlan().Captures;

    private IEnumerable<HoistedLocal> HoistedLocals() => this.ClosurePlan().HoistedLocals;

    private IEnumerable<FuncFragment> DelegateFuncs() => this.ClosurePlan().Delegates;

    private IEnumerable<(string Name,string TypeName)> ClosureParameters() {
        var plan=this.ClosurePlan();
        return plan.Captures
            .Concat(plan.HoistedLocals.Select(static hoistedLocal => (hoistedLocal.Name,hoistedLocal.TypeName)))
            .Concat(plan.Delegates.Select(static func => (func.DelegateFieldName!,func.DelegateTypeName!)));
    }

    private string ClosureArgumentList() => string.Join(", ",this.ClosurePlan().ArgumentExpressions());

    private string IndexFieldName(int index) => $"Index{index}";
}


internal sealed class ProviderIterateTerminal:ProviderNativeExecutionBase {
    private ProviderIterateTerminal(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        ProviderNativeRootBase root,
        FuncFragment? terminalPredicate = null,
        string? containsValue = null,
        string? containsComparer = null,
        FuncFragment? terminalFunc1 = null,
        FuncFragment? terminalFunc2 = null,
        string? terminalComparer = null)
        : base(
            number,
            pipeline,
            terminalKind,
            root.ProviderName,
            root.SourceVariablePrefix,
            root.SourceExpression,
            root.ElementType,
            root.StrategyInterfaceTypeName,
            root.NativeCountExpression is not null,
            root.ScanCountMethodName,
            terminalPredicate,
            containsValue,
            containsComparer,
            root.NativeOperationName,
            root.NativeExecutionMethodName,
            terminalFunc1,
            terminalFunc2,
            terminalComparer) {
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        PipelineTerminalKind terminalKind,
        FuncFragment? terminalFunc1,
        FuncFragment? terminalFunc2,
        string? terminalComparer,
        out ProviderIterateTerminal terminal,
        string? terminalValue = null) {
        terminal=null!;
        if(pipeline.Source is not ProviderNativeRootBase root||!CanCreateFor(pipeline,terminalKind,terminalValue)) {
            return false;
        }
        terminal=new(number,pipeline,ToProviderIterateTerminalKind(terminalKind),root,containsValue: terminalValue,containsComparer: terminalComparer,terminalFunc1: terminalFunc1,terminalFunc2: terminalFunc2,terminalComparer: terminalComparer);
        return true;
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        FuncFragment? terminalPredicate,
        string? containsValue,
        string? containsComparer,
        out ProviderIterateTerminal terminal) {
        terminal=null!;
        if(pipeline.Source is not ProviderNativeRootBase root
            ||!CanCreateFor(pipeline,terminalKind,terminalPredicate,containsValue)) {
            return false;
        }
        terminal=new(number,pipeline,terminalKind,root,terminalPredicate,containsValue,containsComparer);
        return true;
    }
}
