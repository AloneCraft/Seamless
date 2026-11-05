namespace Seamless.FLinq.Generator;

// ここに置いてある ProviderIterateTerminalKind / ProviderNativeExecutionBase は
// このファイルの ProviderIterateTerminal 専用で、他に消費者がいない。
// AI_SYNTAX_RULES の File Layout Rules（実質 1 系列専用の基底は本体ファイルへ集約する）に従い
// Bases/ProviderIterateTerminalBase.cs から移してきた。

internal enum ProviderIterateTerminalKind {
    ToList,
    ToArray,
    Count,
    LongCount,
    Any,
    First,
    ToDictionary,
    Sum,
    All,
    FirstOrDefault,
    Contains,
    GroupBuild,
    JoinBuild,
    Consume,
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
        (terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray
            or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.All or PipelineTerminalKind.First
            or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum
            ||(terminalKind==PipelineTerminalKind.Contains&&containsValue is not null)
            // 既定値を取らない FirstOrDefault(Where の後ろなど複数段の形)も native iterate に流す。既定値つきは別の演算なので対象外。
            ||(terminalKind==PipelineTerminalKind.FirstOrDefault&&containsValue is null))
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
            ProviderIterateTerminalKind.Count or ProviderIterateTerminalKind.LongCount => pipeline.NonEnumeratedCountExpression is null,
            ProviderIterateTerminalKind.Any => pipeline.Stages.Any(static stage => stage.Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile),
            _ => true,
        }
        &&pipeline.Stages.All(static stage => PipelineStageCapabilityTable.SupportsProviderNativeIterate(stage.Kind))
        &&!pipeline.Stages.Any(static stage => stage.Func is { RequiresStatementInlining: true })
        &&terminalPredicate is not { RequiresStatementInlining: true };

    protected static ProviderIterateTerminalKind ToProviderIterateTerminalKind(PipelineTerminalKind terminalKind) =>
        terminalKind switch {
            PipelineTerminalKind.Count => ProviderIterateTerminalKind.Count,
            PipelineTerminalKind.LongCount => ProviderIterateTerminalKind.LongCount,
            PipelineTerminalKind.Any => ProviderIterateTerminalKind.Any,
            PipelineTerminalKind.All => ProviderIterateTerminalKind.All,
            PipelineTerminalKind.Contains => ProviderIterateTerminalKind.Contains,
            PipelineTerminalKind.First => ProviderIterateTerminalKind.First,
            PipelineTerminalKind.FirstOrDefault => ProviderIterateTerminalKind.FirstOrDefault,
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
        foreach(var line in this.ExecutionPlan().Split('\n')){if(line.Length>0){builder.AppendLine("// "+line);}}
        builder.AppendLine("// [評価] 入力式を一度だけ評価し、副作用のある式を走査のたびに再評価しない。");
        builder.AppendLine($"var {sourceName}={sourceExpression};");
        if(this.UsesScanCount) {
            var countType="int";if(terminalKind==ProviderIterateTerminalKind.LongCount){countType="long";}
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName})checked(({countType})({sourceName}.{scanCountMethodName}()));");
            return builder.ToString().TrimEnd();
        }
        builder.AppendLine("// [状態] この呼出し専用のstrategyを作る。別クエリ/workerと可変状態を共有しない。");
        builder.AppendLine($"var {stateName}=new {this.StateTypeName}({this.ClosureArgumentList()});");
        if(this.IsLookupBuild){builder.AppendLine($"{stateName}.{this.GroupBuilderField}={actualVariableName};");}
        if(terminalKind==ProviderIterateTerminalKind.Consume){builder.AppendLine($"{stateName}.__FLinqConsumer={actualVariableName};");}
        if(this.UsesBudgetedResult){
            var resultName=$"__queryResult{this.number}";var field="Array";if(terminalKind==ProviderIterateTerminalKind.ToList){field="List";}
            builder.AppendLine("// [資源/結果] 構築と再確保の前に共通予算を予約。例外/取消でもusingで予約を返し、完成した結果をcallerへ渡す。");
            if(terminalKind==ProviderIterateTerminalKind.ToDictionary){
                field="Dictionary";
                builder.AppendLine($"using var {resultName}={sourceName}.CreateDictionaryBuffer<{this.DictionaryKeyType},{this.DictionaryValueType}>((global::System.Collections.Generic.IEqualityComparer<{this.DictionaryKeyType}>?){terminalComparer??"null"});");
            }else{builder.AppendLine($"using var {resultName}={sourceName}.CreateResultBuffer<{pipeline.ElementType}>();");}
            builder.AppendLine($"{stateName}.{field}={resultName};");
        }else if(sourceHasCount) {
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
        if(this.UsesRowRuntime){
            if(providerName=="DatabaseRows"){
                builder.AppendLine("// [アクセス/同期] GetQueryRowsは入力を一度評価し、版付きGatewayなら所有scopeのTransactionRowsへ委譲。従来方式は既存の読取session/overlayを逐次列挙し、別の取引を開始しない。");
            }else{builder.AppendLine("// [アクセス/同期] TransactionRows.Iterateがoperationと走査保護を所有。自己変更と同じsnapshotの可視版を合成し、callback終了後に参照を返す。");}
            if(terminalKind==ProviderIterateTerminalKind.Consume){
                builder.AppendLine("// [照合/同期] 外側のproducer callbackを元順で呼び、falseなら走査を終了する。外側のExecuteQueryが照合と全workerの排出までoperationを保持する。");
            }else if(this.IsLookupBuild){
                builder.AppendLine("// [キュー/同期] selectorの結果を外側が所有する有界格納queueへ渡す。入力の順序を変えず、scope operationは全workerの排出まで保持する。");
            }else if(this.UsesParallelCount){
                builder.AppendLine("// [キュー/同期] runtimeが有界workerを共通CPU枠へ投入。範囲の取得だけを同期し、全worker停止後に部分件数をcheckedで合成してoperationを返す。");
            }else{builder.AppendLine("// [キュー] このquery用のworker queueは作らない。native pending I/Oは呼出し内で完了させ、確定/WAL queueへはここから投入しない。");}
        }
        var method=executionMethodName;if(this.UsesParallelCount){method="IterateParallel";}
        builder.AppendLine($"{sourceName}.{method}(ref {stateName});");
        if(this.IsLookupBuild||terminalKind==ProviderIterateTerminalKind.Consume){return builder.ToString().TrimEnd();}
        if(this.UsesBudgetedResult){
            var field="Array";var finish="ToArray";
            if(terminalKind==ProviderIterateTerminalKind.ToList){field="List";finish="ToList";}
            else if(terminalKind==ProviderIterateTerminalKind.ToDictionary){field="Dictionary";finish="ToDictionary";}
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.{field}!.{finish}();");
        }else if(terminalKind==ProviderIterateTerminalKind.ToList) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.List!;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToArray) {
            builder.AppendLine($"if({stateName}.Count!={stateName}.Array!.Length) global::System.Array.Resize(ref {stateName}.Array,{stateName}.Count);");
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Array!;");
        } else if(terminalKind==ProviderIterateTerminalKind.ToDictionary) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Dictionary!;");
        } else if(terminalKind==ProviderIterateTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=({actualTypeName}){stateName}.Sum;");
        } else if(terminalKind is ProviderIterateTerminalKind.Count or ProviderIterateTerminalKind.LongCount) {
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
        terminalKind is ProviderIterateTerminalKind.Count or ProviderIterateTerminalKind.LongCount
        &&scanCountMethodName is not null
        &&terminalPredicate is null
        &&pipeline.Stages.All(static stage => stage.Kind==PipelineStageKind.AsEnumerable);

    private bool UsesRowRuntime=>providerName is "TransactionRows" or "DatabaseRows";
    private bool UsesParallelCount=>this.UsesRowRuntime
        &&terminalKind is ProviderIterateTerminalKind.Count or ProviderIterateTerminalKind.LongCount
        &&terminalPredicate is null&&pipeline.Stages.All(static stage=>stage.Kind==PipelineStageKind.AsEnumerable);
    private bool UsesBudgetedResult=>this.UsesRowRuntime&&terminalKind is ProviderIterateTerminalKind.ToArray or ProviderIterateTerminalKind.ToList or ProviderIterateTerminalKind.ToDictionary;
    private bool IsLookupBuild=>terminalKind is ProviderIterateTerminalKind.GroupBuild or ProviderIterateTerminalKind.JoinBuild;
    private string GroupElementType=>terminalFunc2?.ReturnTypeName??pipeline.ElementType;
    private string GroupBuilderField{
        get{
            var name="__FLinqGroupBuilder";var used=new HashSet<string>(this.ClosureParameters().Select(static capture=>capture.Name),StringComparer.Ordinal);
            while(used.Contains(name)){name+="_";}return name;
        }
    }

    private string StateStructSource() {
        var builder = new StringBuilder();
        builder.AppendLine("#if DEBUG");
        builder.AppendLine("[global::System.Diagnostics.DebuggerDisplayAttribute(\"Count={Count}, Found={Found}\")]");
        builder.AppendLine("#endif");
        var strategyInterface=strategyInterfaceTypeName;if(this.UsesParallelCount){strategyInterface="global::Seamless.Collections.Iteration.IParallelStrategy";}
        builder.AppendLine($"private struct {this.StateTypeName} : {strategyInterface}<{this.StateTypeName}, {sourceElementType}> {{");
        var debugName="__FLinqExecutionPlanDebugConstant";
        var used=new HashSet<string>(this.CapturedLocals().Select(static local=>local.Name).Concat(this.HoistedLocals().Select(static local=>local.Name)).Concat(this.DelegateFuncs().Select(static function=>function.DelegateFieldName)).Select(static name=>name??string.Empty),StringComparer.Ordinal);
        while(used.Contains(debugName)){debugName+="_";}
        builder.AppendLine("#if DEBUG");
        builder.AppendLine("    // [Debug] 入力/変形/アクセス/並列/所有権を説明する静的計画。行やcodecの参照を保持しない。");
        builder.AppendLine("    internal const string "+debugName+"="+Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(this.ExecutionPlan(),true)+";");
        builder.AppendLine("#endif");
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
        var countType="int";if(terminalKind is ProviderIterateTerminalKind.LongCount or ProviderIterateTerminalKind.GroupBuild or ProviderIterateTerminalKind.JoinBuild or ProviderIterateTerminalKind.Consume){countType="long";}
        builder.AppendLine("    public "+countType+" Count;");
        builder.AppendLine("    public bool Found;");
        builder.AppendLine($"    public {pipeline.ElementType} Value;");
        if(this.IsLookupBuild){builder.AppendLine($"    public global::Seamless.Linq.PartitionedLookupBuilder<{terminalFunc1!.ReturnTypeName},{this.GroupElementType}>? {this.GroupBuilderField};");}
        if(terminalKind==ProviderIterateTerminalKind.Consume){builder.AppendLine($"    public global::System.Func<{pipeline.ElementType},bool>? __FLinqConsumer;");}
        if(terminalKind==ProviderIterateTerminalKind.JoinBuild){builder.AppendLine("    private static bool IsNullJoinKey<TKey>(TKey key)=>key is null;");}
        if(this.UsesBudgetedResult){
            if(terminalKind==ProviderIterateTerminalKind.ToDictionary){builder.AppendLine($"    public global::Seamless.Execution.QueryDictionaryBuffer<{this.DictionaryKeyType},{this.DictionaryValueType}>? Dictionary;");}
            else{
                var field="Array";if(terminalKind==ProviderIterateTerminalKind.ToList){field="List";}
                builder.AppendLine($"    public global::Seamless.Execution.QueryResultBuffer<{pipeline.ElementType}>? {field};");
            }
        }else if(terminalKind==ProviderIterateTerminalKind.ToList) {
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
        if(terminalKind==ProviderIterateTerminalKind.Consume){
            builder.AppendLine("    // [並列] 外側行の照合callbackはproducer上で元順に実行。内側Lookupの格納queue/workerは外側のqueryが所有する。");
        }else if(this.IsLookupBuild){
            builder.AppendLine("    // [並列] native走査とselectorはproducer一つで実行。格納用worker queue/退避/初出順の合成は外側のPartitionedLookupBuilderが所有する。");
        }else if(this.UsesParallelCount){
            builder.AppendLine("    // [分割] callbackも共有可変状態もない件数計算。部分状態を独立に初期化。");
            builder.AppendLine($"    public static {this.StateTypeName} CreatePartial(in {this.StateTypeName} seed)=>new();");
            builder.AppendLine("    // [合成] 非負の部分件数をchecked加算。CountはInt32、LongCountはInt64でoverflowを検出。");
            builder.AppendLine($"    public static void Merge(ref {this.StateTypeName} result,in {this.StateTypeName} partial){{checked{{result.Count+=partial.Count;}}}}");
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
        if(this.UsesParallelCount){builder.AppendLine("    // [並列/順序] 各worker専用の部分件数だけを更新。要素順や利用者callbackに依存しない。");}
        else{builder.AppendLine("    // [並列/順序] 一回の走査の一要素を処理。ここではworkerを追加せず、部分和の再結合もしない。");}
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
            builder.AppendLine("    // [融合段 "+index+"] "+stage.Kind+": "+GeneratedExecutionAnnotations.Clean(stage.InputType)+" -> "+GeneratedExecutionAnnotations.Clean(stage.OutputType)+"。中間IEnumerableの列挙子をこの段のために作らない。");
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
        if(this.IsLookupBuild){
            builder.AppendLine($"    {terminalFunc1!.ReturnTypeName} groupKey=default!;");
            GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(terminalFunc1,"GroupBy","groupKey",new[]{currentName}),"    ");
            if(terminalKind==ProviderIterateTerminalKind.JoinBuild){builder.AppendLine("    if(IsNullJoinKey(groupKey)){return true;}");}
            var element=currentName;
            if(terminalFunc2 is not null){element="groupElement";builder.AppendLine($"    {this.GroupElementType} {element}=default!;");GeneratedCodeWriter.AppendIndented(builder,this.FuncAssignStatic(terminalFunc2,"GroupBy",element,new[]{currentName}),"    ");}
            builder.AppendLine($"    state.{this.GroupBuilderField}!.Add(groupKey,({this.GroupElementType}){element});checked{{state.Count++;}}return true;");
        }else if(terminalKind==ProviderIterateTerminalKind.Consume){
            builder.AppendLine($"    checked{{state.Count++;}}return state.__FLinqConsumer!({currentName});");
        }else if(this.UsesBudgetedResult&&terminalKind is ProviderIterateTerminalKind.ToArray or ProviderIterateTerminalKind.ToList){
            var field="Array";if(terminalKind==ProviderIterateTerminalKind.ToList){field="List";}
            builder.AppendLine($"    state.{field}!.Add(({pipeline.ElementType}){currentName});");
            builder.AppendLine("    checked{state.Count++;}return true;");
        }else if(terminalKind==ProviderIterateTerminalKind.ToList) {
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
        } else if(terminalKind is ProviderIterateTerminalKind.Count or ProviderIterateTerminalKind.LongCount) {
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
    private string ExecutionPlan(){
        var builder=new StringBuilder();
        builder.AppendLine("[生成計画] schema=1; generator="+GeneratedExecutionAnnotations.ToolVersion+"; provider="+providerName+"; terminal="+terminalKind);
        builder.AppendLine("[入力] "+GeneratedExecutionAnnotations.Clean(sourceExpression)+"; element="+GeneratedExecutionAnnotations.Clean(sourceElementType));
        foreach(var func in this.Funcs()){if(func.ExecutionNote is not null){builder.AppendLine(GeneratedExecutionAnnotations.Clean(func.ExecutionNote));}}
        builder.AppendLine("[変形] 正規化後の段="+string.Join(" -> ",pipeline.Stages.Select(static stage=>stage.Kind.ToString()))+" -> "+terminalKind+"。各段をstrategyの同じ要素処理へ融合。");
        if(terminalKind==ProviderIterateTerminalKind.Consume){
            builder.AppendLine("[分担] 前段をnative strategyへ融合し、照合callbackへ渡す。callbackはproducer上で実行し、内側格納だけを別workerへ分担する。");
        }else if(this.IsLookupBuild){
            builder.AppendLine("[分担] selectorはproducer上で元順に実行し、有界batchを分割Lookupへ渡す。比較/codecの安全性と資源条件を確認した計画だけがworkerへの格納を許可する。");
            builder.AppendLine("[並列] このInvokeはproducer上。格納用queue/worker/初出順の合成は外側のPartitionedLookupBuilderが所有する。");
        }else if(this.UsesParallelCount){
            if(providerName=="DatabaseRows"){builder.AppendLine("[方式選択] 版付きGatewayだけが以下のsnapshot workerへ委譲する。従来方式の単純件数は読取session内で自己overlayを反映する既存の論理件数を使う。版付きの件数へ現在値Countを流用しない。");}
            builder.AppendLine("[並列] runtimeがsnapshotのlogをpage境界で有界chunkに分割し、workerごとのhandleと部分件数で処理。全行配列と全rangeのqueueを作らない。");
            builder.AppendLine("[並列判断] 条件/変形callbackがない全件Count/LongCountだけを対象とし、非負の件数加算をcheckedで結合。小さい入力・私有変更あり・worker上限1・入れ子のCPU作業・旧adapterは逐次走査へ戻る。");
            builder.AppendLine("[CPU/待機] host共通のCPU枠をchunkごとに取得。native pendingの完了待ちでは枠を返し、同期reader callbackで再取得。一つのscope operationで全workerの終了まで保護する。");
        }else{
            builder.AppendLine("[並列] この経路は呼出し側の一つの実行主体で同期走査。追加worker/作業queue/部分結果のマージを生成しない。");
            builder.AppendLine("[並列判断] callbackのスレッド安全性や演算の結合則をこの経路では証明しない。読取専用の判定だけで並列化しない。");
        }
        if(providerName=="DatabaseRows"){
            builder.AppendLine("[アクセス] 入力型Table/MaterializedViewを静的に認識。GetQueryRowsで版付きGatewayの所有scopeを捕捉し、可視版・私有変更・走査依存を既存TransactionRowsへ渡す。従来方式は既存overlayと読取保護を維持する。");
            builder.AppendLine("[同期/寿命] 方式をまたぐ暗黙の取引開始や独立coordinatorは作らない。版付きはoperationの寿命、従来方式は読取sessionの寿命でcallbackと後始末を保護する。");
        }else if(providerName=="TransactionRows"){
            builder.AppendLine("[アクセス] 表走査の依存を登録し、ReadVersion以前の可視版と私有変更を合成。点検索への置換や現在値Countの流用はしない。");
            builder.AppendLine("[同期/寿命] scope.Useのoperation内で実行。入力の構造保護とnative handleはruntime側のusing/finallyで解放し、取消/例外でもcallback終了まで保持。");
        }
        if(sourceHasCount){builder.AppendLine("[件数] providerが保証するCountを確保容量に利用。");}
        else{builder.AppendLine("[件数] 静的な件数を仮定しない。結果を必要に応じて増やし、現在値件数をsnapshot件数とみなさない。");}
        if(terminalKind==ProviderIterateTerminalKind.Consume){
            builder.AppendLine("[結果所有] 外側のqueryが照合結果を予算付き結果bufferへ渡す。このstrategyは全件配列やスカラー結果を返さない。");
        }else if(this.IsLookupBuild){
            builder.AppendLine("[結果所有] selectorの結果は外側のLookupへ転送。有界batchと予算付き退避の所有・解放はそのLookupが行い、このstrategyからスカラーを返さない。");
        }else if(this.UsesBudgetedResult){
            builder.AppendLine("[結果所有] 結果コンテナの構築・再確保と既知codecの値費用を共通予算へ計上。完成時にも取引の中止を検査し、usingで作業予約を返す。返却後の配列/List/Dictionaryはcaller所有。");
        }else if(terminalKind is ProviderIterateTerminalKind.ToArray or ProviderIterateTerminalKind.ToList or ProviderIterateTerminalKind.ToDictionary){
            builder.AppendLine("[結果所有] managedな結果コレクションを呼出し側へ返す。この終端は結果全体のResourceBudget予約やPersisted退避を生成しない。");
        }else{builder.AppendLine("[結果所有] 呼出し専用strategyからスカラー結果をコピー。全件配列やquery用退避ファイルを作らない。");}
        if(providerName=="DatabaseRows"){
            builder.AppendLine("[予算/退避] 結果構築はDBの共通予算へ計上。版付きGatewayの入力/変更/workerは所有scopeへ委譲し、従来方式の列挙子をworkerへ共有しない。");
            builder.AppendLine("[取消/例外] 版付きは取引中止、従来方式はDB資源の破棄/故障を処理境界で確認。callbackを強制終了せず、既存の例外と解放順序を保つ。");
        }else if(providerName=="TransactionRows"){
            builder.AppendLine("[予算/退避] 入力走査の作業予約と私有変更の退避は取引runtimeが所有。Persistedは私有作業用で、共有DBの耐久化先ではない。");
            builder.AppendLine("[取消/例外] runtimeが要素処理の前後で取引中止を確認。利用者callbackの実行中に強制終了せず、返った後に中止を通知して参照を解放。");
        }
        builder.AppendLine("[例外順序] callback、比較器、数値演算を出力された位置で実行。先読み、再評価、業務処理の自動再試行を追加しない。");
        builder.AppendLine("[不変条件] 元ソースの制御・比較・数値演算は生成された分岐/加算/比較器の位置で確認できる。早期終了はfalseを返して走査の後始末へ進む。");
        return builder.ToString();
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
    internal static ProviderIterateTerminal GroupBuilder(int number,PipelineEnumerable pipeline,TransactionRowsRoot root,FuncFragment keySelector,FuncFragment? elementSelector)=>new(number,pipeline,ProviderIterateTerminalKind.GroupBuild,root,terminalFunc1:keySelector,terminalFunc2:elementSelector);
    internal static ProviderIterateTerminal JoinBuilder(int number,PipelineEnumerable pipeline,TransactionRowsRoot root,FuncFragment keySelector)=>new(number,pipeline,ProviderIterateTerminalKind.JoinBuild,root,terminalFunc1:keySelector);
    internal static ProviderIterateTerminal Consumer(int number,PipelineEnumerable pipeline,TransactionRowsRoot root)=>new(number,pipeline,ProviderIterateTerminalKind.Consume,root);
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
        terminal=new(number,pipeline,ToProviderIterateTerminalKind(terminalKind),root,terminalPredicate: terminalFunc1,containsValue: terminalValue,containsComparer: terminalComparer,terminalFunc1: terminalFunc1,terminalFunc2: terminalFunc2,terminalComparer: terminalComparer);
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
