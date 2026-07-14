namespace Seamless.FLinq.Linq.Fragments;

public enum ProviderIterateTerminalKind {
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

public abstract class ProviderNativeExecutionBase:Scalar, DirectExecutionFragment {
    private readonly int number;
    private readonly PipelineEnumerable pipeline;
    private readonly ProviderIterateTerminalKind terminalKind;
    private readonly string providerName;
    private readonly string sourceVariablePrefix;
    private readonly string sourceExpression;
    private readonly string sourceElementType;
    private readonly string strategyInterfaceTypeName;
    private readonly string operationName;
    private readonly string executionMethodName;
    private readonly FuncFragment? terminalPredicate;
    private readonly string? containsValue;
    private readonly string? containsComparer;
    private readonly FuncFragment? terminalFunc1;
    private readonly FuncFragment? terminalFunc2;
    private readonly string? terminalComparer;

    protected ProviderNativeExecutionBase(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        string providerName,
        string sourceVariablePrefix,
        string sourceExpression,
        string sourceElementType,
        string strategyInterfaceTypeName,
        FuncFragment? terminalPredicate = null,
        string? containsValue = null,
        string? containsComparer = null,
        string operationName = "Iterate",
        string executionMethodName = "Iterate",
        FuncFragment? terminalFunc1 = null,
        FuncFragment? terminalFunc2 = null,
        string? terminalComparer = null)
        : base(number) {
        this.number=number;
        this.pipeline=pipeline;
        this.terminalKind=terminalKind;
        this.providerName=providerName;
        this.sourceVariablePrefix=sourceVariablePrefix;
        this.sourceExpression=sourceExpression;
        this.sourceElementType=sourceElementType;
        this.strategyInterfaceTypeName=strategyInterfaceTypeName;
        this.operationName=operationName;
        this.executionMethodName=executionMethodName;
        this.terminalPredicate=terminalPredicate;
        this.containsValue=containsValue;
        this.containsComparer=containsComparer;
        this.terminalFunc1=terminalFunc1;
        this.terminalFunc2=terminalFunc2;
        this.terminalComparer=terminalComparer;
    }

    private string StateTypeName => $"{this.Struct}_{this.providerName}{this.operationName}State";
    protected override Enumerable PrimarySource => this.pipeline;

    protected static bool CanCreateFor(PipelineEnumerable pipeline,PipelineTerminalKind terminalKind) =>
        terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray
            or PipelineTerminalKind.Count or PipelineTerminalKind.Any or PipelineTerminalKind.First
            or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum
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
            _ => true,
        }
        &&pipeline.Stages.All(static stage => PipelineStageCapabilityTable.SupportsProviderNativeIterate(stage.Kind))
        &&!pipeline.Stages.Any(static stage => stage.Func is { RequiresStatementInlining: true })
        &&terminalPredicate is not { RequiresStatementInlining: true };

    protected static ProviderIterateTerminalKind ToProviderIterateTerminalKind(PipelineTerminalKind terminalKind) =>
        terminalKind switch {
            PipelineTerminalKind.Count => ProviderIterateTerminalKind.Count,
            PipelineTerminalKind.Any => ProviderIterateTerminalKind.Any,
            PipelineTerminalKind.First => ProviderIterateTerminalKind.First,
            PipelineTerminalKind.ToList => ProviderIterateTerminalKind.ToList,
            PipelineTerminalKind.ToArray => ProviderIterateTerminalKind.ToArray,
            PipelineTerminalKind.ToDictionary => ProviderIterateTerminalKind.ToDictionary,
            PipelineTerminalKind.Sum => ProviderIterateTerminalKind.Sum,
            _ => throw new NotSupportedException($"{terminalKind} does not support provider native scan."),
        };

    public override string StructMethod {
        get {
            var builder = new StringBuilder();
            foreach(var func in this.Funcs()) {
                GeneratedCodeWriter.AppendMember(builder,this.FuncSupport(func,this.operationName));
            }
            GeneratedCodeWriter.AppendMember(builder,this.StateStructSource());
            return builder.ToString().TrimEnd();
        }
    }

    public override string GetEnumeratorStruct => string.Empty;
    public override string GetTryGetNextBody => string.Empty;
    public override string GetCurrentExpression => string.Empty;

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var sourceName = $"{this.sourceVariablePrefix}{this.number}";
        var stateName = $"__{this.providerName}{this.operationName}State{this.number}";
        var builder = new StringBuilder();
        builder.AppendLine($"var {sourceName}={this.sourceExpression};");
        builder.AppendLine($"var {stateName}=new {this.StateTypeName}({this.ClosureArgumentList()});");
        if(this.terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"{stateName}.List=new global::System.Collections.Generic.List<{this.pipeline.ElementType}>({sourceName}.Count);");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"{stateName}.Array=global::System.GC.AllocateUninitializedArray<{this.pipeline.ElementType}>({sourceName}.Count);");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"{stateName}.Dictionary=new({sourceName}.Count,(global::System.Collections.Generic.IEqualityComparer<{this.DictionaryKeyType}>?){this.terminalComparer??"null"});");
        }
        if(this.containsValue is not null) {
            builder.AppendLine($"{stateName}.ContainsValue={this.containsValue};");
            if(this.containsComparer is not null) {
                builder.AppendLine($"{stateName}.ContainsComparer={this.containsComparer};");
            }
        }
        builder.AppendLine($"{sourceName}.{this.executionMethodName}(ref {stateName});");
        if(this.terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.List!;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"if({stateName}.Count!={stateName}.Array!.Length) global::System.Array.Resize(ref {stateName}.Array,{stateName}.Count);");
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Array!;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Dictionary!;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Sum;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Count) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Count;");
        } else if(this.terminalKind is ProviderIterateTerminalKind.Any or ProviderIterateTerminalKind.Contains) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Found;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.All) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName})(!{stateName}.Found);");
        } else if(this.terminalKind==ProviderIterateTerminalKind.First) {
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

    private string StateStructSource() {
        var builder = new StringBuilder();
        builder.AppendLine($"private struct {this.StateTypeName} : {this.strategyInterfaceTypeName}<{this.StateTypeName}, {this.sourceElementType}> {{");
        foreach(var capture in this.CapturedLocals()) {
            builder.AppendLine($"    public {capture.TypeName} {capture.Name};");
        }
        foreach(var hoistedLocal in this.HoistedLocals()) {
            builder.AppendLine($"    public {hoistedLocal.TypeName} {hoistedLocal.Name};");
        }
        foreach(var func in this.DelegateFuncs()) {
            builder.AppendLine($"    public {func.DelegateTypeName} {func.DelegateFieldName};");
        }
        foreach(var (stage,index) in this.pipeline.Stages.Select(static (stage,index) => (stage,index))) {
            if(stage.UsesIndex) {
                builder.AppendLine($"    public int {this.IndexFieldName(index)};");
            }
        }
        builder.AppendLine("    public int Count;");
        builder.AppendLine("    public bool Found;");
        builder.AppendLine($"    public {this.pipeline.ElementType} Value;");
        if(this.terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"    public global::System.Collections.Generic.List<{this.pipeline.ElementType}>? List;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"    public {this.pipeline.ElementType}[]? Array;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"    public global::System.Collections.Generic.Dictionary<{this.DictionaryKeyType},{this.DictionaryValueType}>? Dictionary;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"    public {this.pipeline.ElementType} Sum;");
        }
        if(this.containsValue is not null) {
            builder.AppendLine($"    public {this.pipeline.ElementType} ContainsValue;");
            builder.AppendLine($"    public global::System.Collections.Generic.IEqualityComparer<{this.pipeline.ElementType}>? ContainsComparer;");
        }
        var parameters = this.ClosureParameters().ToArray();
        if(parameters.Length>0) {
            builder.AppendLine($"    public {this.StateTypeName}({string.Join(", ",parameters.Select(static p => $"{p.TypeName} {p.Name}"))}) {{");
            foreach(var parameter in parameters) {
                builder.AppendLine($"        this.{parameter.Name}={parameter.Name};");
            }
            foreach(var (stage,index) in this.pipeline.Stages.Select(static (stage,index) => (stage,index))) {
                if(stage.UsesIndex) {
                    builder.AppendLine($"        this.{this.IndexFieldName(index)}=-1;");
                }
            }
            builder.AppendLine("        this.Count=0;");
            builder.AppendLine("        this.Found=false;");
            builder.AppendLine("        this.Value=default!;");
            builder.AppendLine("    }");
        } else if(this.pipeline.Stages.Any(static stage => stage.UsesIndex)) {
            builder.AppendLine($"    public {this.StateTypeName}() {{");
            foreach(var (stage,index) in this.pipeline.Stages.Select(static (stage,index) => (stage,index))) {
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
        builder.AppendLine($"public static bool Invoke(ref {this.StateTypeName} state,{this.sourceElementType} item) {{");
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
        foreach(var (stage,index) in this.pipeline.Stages.Select(static (stage,index) => (stage,index))) {
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
            }
        }
        if(this.terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"    state.List!.Add(({this.pipeline.ElementType}){currentName});");
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"    state.Array![state.Count++]=({this.pipeline.ElementType}){currentName};");
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"    {this.DictionaryKeyType} dictionaryKey=default!;");
            GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(this.terminalFunc1!,"ToDictionary","dictionaryKey",[currentName]),"    ");
            if(this.terminalFunc2 is not null) {
                builder.AppendLine($"    {this.DictionaryValueType} dictionaryValue=default!;");
                GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(this.terminalFunc2,"ToDictionary","dictionaryValue",[currentName]),"    ");
                builder.AppendLine("    state.Dictionary![dictionaryKey]=dictionaryValue;");
            } else {
                builder.AppendLine($"    state.Dictionary![dictionaryKey]=({this.DictionaryValueType})(object?){currentName}!;");
            }
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"    state.Sum+=({this.pipeline.ElementType}){currentName};");
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.All) {
            var tempName = "all";
            builder.AppendLine($"    bool {tempName}=default!;");
            var args = new[] { currentName };
            GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(this.terminalPredicate!,"All",tempName,args),"    ");
            builder.AppendLine($"    if(!{tempName}) {{");
            builder.AppendLine("        state.Found=true;");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Contains) {
            builder.AppendLine("    var comparer=state.ContainsComparer ?? global::System.Collections.Generic.EqualityComparer<"+this.pipeline.ElementType+">.Default;");
            builder.AppendLine($"    if(comparer.Equals(({this.pipeline.ElementType}){currentName},state.ContainsValue)) {{");
            builder.AppendLine("        state.Found=true;");
            builder.AppendLine("        return false;");
            builder.AppendLine("    }");
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Count) {
            builder.AppendLine("    checked { state.Count++; }");
            builder.AppendLine("    return true;");
        } else if(this.terminalKind==ProviderIterateTerminalKind.Any) {
            builder.AppendLine("    state.Found=true;");
            builder.AppendLine("    return false;");
        } else {
            builder.AppendLine($"    state.Value=({this.pipeline.ElementType}){currentName};");
            builder.AppendLine("    state.Found=true;");
            builder.AppendLine("    return false;");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private IEnumerable<FuncFragment> Funcs() =>
        this.pipeline.Stages.Select(static stage => stage.Func)
            .Where(static func => func is not null)
            .Select(static func => func!)
            .Concat(this.terminalPredicate is null ? [] : [this.terminalPredicate])
            .Concat(this.terminalFunc1 is null ? [] : [this.terminalFunc1])
            .Concat(this.terminalFunc2 is null ? [] : [this.terminalFunc2]);

    private string DictionaryKeyType => this.terminalFunc1?.ReturnTypeName??"object";
    private string DictionaryValueType => this.terminalFunc2?.ReturnTypeName??this.pipeline.ElementType;

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
            if(func.CanInvokeAsDelegate&&func.DelegateFieldName is { } name&&names.Add(name)) {
                yield return func;
            }
        }
    }

    private IEnumerable<(string Name,string TypeName)> ClosureParameters() =>
        this.CapturedLocals()
            .Concat(this.HoistedLocals().Select(static hoistedLocal => (hoistedLocal.Name,hoistedLocal.TypeName)))
            .Concat(this.DelegateFuncs().Select(static func => (func.DelegateFieldName!,func.DelegateTypeName!)));

    private string ClosureArgumentList() {
        var args = this.CapturedLocals().Select(static capture => capture.Name)
            .Concat(this.HoistedLocals().Select(static hoistedLocal => hoistedLocal.ExpressionText))
            .Concat(this.DelegateFuncs().Select(static func => func.DelegateConstructorArg!))
            .ToArray();
        return string.Join(", ",args);
    }

    private string IndexFieldName(int index) => $"Index{index}";
}
