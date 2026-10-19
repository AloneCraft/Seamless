namespace Seamless.FLinq.Generator;

/*
PipelineTerminal は PipelineEnumerable の終端を scalar/direct execution として出す。

入力例:
  source.Where(p).Select(s).ToArray()
  source.Where(p).Any()

生成例:
  using var e = pipeline.GetEnumerator();
  if(kind == Any) result = e.TryGetNext(out _);
  if(kind == Count) while(e.TryGetNext(out _)) count++;
  if(kind == ToArray) TryGetNonEnumeratedCount/TryCopyTo を試し、だめなら ArrayPool で蓄積する。

バリエーション:
  - Any/Count/First は単一結果 enumerator も direct execution も生成できる。
  - ToList/ToArray/ToDictionary/Sum は direct execution 専用で、GetEnumeratorExpression は使わない。
  - ToDictionary は func1 を keySelector、func2 を elementSelector として扱い、comparerValue を constructor に渡す。
*/
internal sealed class PipelineTerminal(int 番号,PipelineEnumerable pipeline,PipelineTerminalKind terminalKind,FuncFragment? func1 = null,FuncFragment? func2 = null,string? comparerValue = null):Scalar(番号), DirectExecutionFragment {
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
        if(func1 is { } __visitorChild_func1) {
            yield return __visitorChild_func1;
        }
        if(func2 is { } __visitorChild_func2) {
            yield return __visitorChild_func2;
        }
    }

    protected override Enumerable PrimarySource => pipeline;
    private string ResultType {
        get {
            if(terminalKind==PipelineTerminalKind.Any) {
                return "bool";
            }
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int";
            }
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
                return pipeline.ElementType;
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    public override string StructMethod => pipeline.StructMethod;
    public override string GetEnumeratorStruct {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                return pipeline.GetEnumeratorStruct;
            }
            return $$"""
                {{pipeline.GetEnumeratorStruct}}
                {{this.DefineScalarEnumeratorStruct(this.ResultType)}}
                """;
        }
    }
    public override string GetEnumeratorExpression {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum) {
                throw new NotSupportedException($"{nameof(PipelineTerminal)} uses direct execution.");
            }
            return RenderEnumeratorExpression(this.EnumeratorTypeName,pipeline.GetEnumeratorExpression);
        }
    }
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorConstructor => $"this.Current = Compute(ref this.{this.e});";
    public override string GetEnumeratorFields => $$"""
        public {{this.ResultType}} Current { get; private set; }
        private bool {{this.yielded}} = false;
        {{AggressiveInliningAttribute}}
        private static {{this.ResultType}} Compute(ref TEnum source){
            {{this.ComputeBody}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}){
            current=default!;
            return false;
        }
        this.{{this.yielded}} = true;
        current = this.Current;
        return true;
        """;
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    private string ComputeBody {
        get {
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int count = 0;\nwhile(source.TryGetNext(out _)) count++;\nreturn count;";
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                return "return source.TryGetNext(out _);";
            }
            if(terminalKind==PipelineTerminalKind.First) {
                return "if(source.TryGetNext(out var item)) return item;\nthrow global::Seamless.Linq.GeneratedCodeHelpers.NoElements();";
            }
            if(terminalKind==PipelineTerminalKind.FirstOrDefault) {
                return "if(source.TryGetNext(out var item)) return item;\nreturn default!;";
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    private bool CanUseDirectSpanPipelineCopy() {
        foreach(var stage in pipeline.Stages) {
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.Cast:
                    break;
                case PipelineStageKind.Select:
                    if(stage.UsesIndex||stage.Func is not { } func||!CanUseDirectSpanPipelineFunc(func)) {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    private static bool CanUseDirectSpanPipelineFunc(FuncFragment func) {
        if(!func.CanInlineAssign
            ||func.CapturedLocals.Length!=0
            ||func.HoistedLocals.Length!=0
            ||func.RequiresStatementInlining
            ||func.ParameterNames.Length!=1
            ||string.IsNullOrWhiteSpace(func.ParameterNames[0])) {
            return false;
        }

        return true;
    }

    private bool IsDirectSpanFilterStage(PipelineStage stage) {
        if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any) {
            if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                return false;
            }
        }
        return stage.Kind is PipelineStageKind.Where
            or PipelineStageKind.TakeWhile
            or PipelineStageKind.SkipWhile
            or PipelineStageKind.Distinct
            or PipelineStageKind.DistinctBy;
    }

    private static bool IsDirectSpanSliceStage(PipelineStage stage) =>
        stage.Kind is PipelineStageKind.Take
            or PipelineStageKind.Skip
            or PipelineStageKind.TakeLast
            or PipelineStageKind.SkipLast;

    private HashSet<int> GetPrefixSliceStageIndices() {
        var result = new HashSet<int>();
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(this.IsDirectSpanFilterStage(stage)) {
                break;
            }
            if(IsDirectSpanSliceStage(stage)||stage.Kind is PipelineStageKind.Reverse or PipelineStageKind.Prepend or PipelineStageKind.Append) {
                result.Add(i);
            }
        }
        return result;
    }

    private List<PipelineStage> GetPrefixPrependStages() {
        var result = new List<PipelineStage>();
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(this.IsDirectSpanFilterStage(stage)) {
                break;
            }
            if(stage.Kind==PipelineStageKind.Prepend) {
                result.Add(stage);
            }
        }
        return result;
    }

    private List<PipelineStage> GetPrefixAppendStages() {
        var result = new List<PipelineStage>();
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(this.IsDirectSpanFilterStage(stage)) {
                break;
            }
            if(stage.Kind==PipelineStageKind.Append) {
                result.Add(stage);
            }
        }
        return result;
    }

    private bool IsPrefixReversed() {
        var count = 0;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(this.IsDirectSpanFilterStage(stage)) {
                break;
            }
            if(stage.Kind==PipelineStageKind.Reverse) {
                count++;
            }
        }
        return count%2!=0;
    }

    private void AppendDirectSpanPipelineSlices(StringBuilder builder,string spanName,string indent,HashSet<int> sliceIndices) {
        for(var index = 0;index<pipeline.Stages.Count;index++) {
            if(!sliceIndices.Contains(index)) {
                continue;
            }
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.Take:
                    builder.AppendLine($"{indent}{spanName}={spanName}.Slice(0,global::System.Math.Min({spanName}.Length,global::System.Math.Max(0,{stage.Value})));");
                    break;
                case PipelineStageKind.Skip:
                    builder.AppendLine($"{indent}{spanName}={spanName}.Slice(global::System.Math.Min({spanName}.Length,global::System.Math.Max(0,{stage.Value})));");
                    break;
                case PipelineStageKind.TakeLast:
                    builder.AppendLine($"{indent}{spanName}={spanName}.Slice(global::System.Math.Max(0,{spanName}.Length-global::System.Math.Max(0,{stage.Value})));");
                    break;
                case PipelineStageKind.SkipLast:
                    builder.AppendLine($"{indent}{spanName}={spanName}.Slice(0,global::System.Math.Max(0,{spanName}.Length-global::System.Math.Max(0,{stage.Value})));");
                    break;
            }
        }
    }

    private static bool IsPrimitiveComparableType(string typeName) {
        switch(typeName) {
            case "int":
            case "long":
            case "short":
            case "byte":
            case "sbyte":
            case "uint":
            case "ulong":
            case "ushort":
            case "float":
            case "double":
            case "decimal":
            case "char":
            case "System.Int32":
            case "System.Int64":
            case "System.Int16":
            case "System.Byte":
            case "System.SByte":
            case "System.UInt32":
            case "System.UInt64":
            case "System.UInt16":
            case "System.Single":
            case "System.Double":
            case "System.Decimal":
            case "System.Char":
                return true;
            default:
                return false;
        }
    }

    private bool CanUseDirectSpanPipelineTop1Order(out int orderStageIndex) {
        orderStageIndex=-1;
        if(terminalKind is not (PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault)) {
            return false;
        }
        var count = 0;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                if(stage.UsesIndex) {
                    return false;
                }
                if(stage.Func is { } func&&!CanUseDirectSpanPipelineFunc(func)) {
                    return false;
                }
                orderStageIndex=i;
                count++;
            }
        }
        if(count!=1) {
            orderStageIndex=-1;
            return false;
        }
        for(var i = orderStageIndex+1;i<pipeline.Stages.Count;i++) {
            var kind = pipeline.Stages[i].Kind;
            if(kind is not (PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)) {
                orderStageIndex=-1;
                return false;
            }
        }
        return true;
    }

    private bool CanUseDirectSpanPipelineScalarScan() {
        var isTop1Order = this.CanUseDirectSpanPipelineTop1Order(out var orderIdx);
        var seenFilter = false;
        var skipDistinct = terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(isTop1Order&&i==orderIdx) {
                continue;
            }
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.Cast:
                case PipelineStageKind.Take:
                case PipelineStageKind.Skip:
                    break;
                case PipelineStageKind.Reverse:
                case PipelineStageKind.TakeLast:
                case PipelineStageKind.SkipLast:
                case PipelineStageKind.Prepend:
                case PipelineStageKind.Append:
                    if(seenFilter) {
                        return false;
                    }
                    break;
                case PipelineStageKind.Distinct:
                    if(!skipDistinct) {
                        seenFilter=true;
                    }
                    break;
                case PipelineStageKind.Select: {
                    if(stage.UsesIndex||stage.Func is not { } selectFunc||!CanUseDirectSpanPipelineFunc(selectFunc)) {
                        return false;
                    }
                    break;
                }
                case PipelineStageKind.Where:
                case PipelineStageKind.TakeWhile:
                case PipelineStageKind.SkipWhile:
                    if(stage.UsesIndex||stage.Func is not { } filterFunc||!CanUseDirectSpanPipelineFunc(filterFunc)) {
                        return false;
                    }
                    seenFilter=true;
                    break;
                case PipelineStageKind.DistinctBy: {
                    if(stage.UsesIndex||stage.Func is not { } distinctByFunc||!CanUseDirectSpanPipelineFunc(distinctByFunc)) {
                        return false;
                    }
                    if(!skipDistinct) {
                        seenFilter=true;
                    }
                    break;
                }
                default:
                    return false;
            }
        }
        return true;
    }

    private string? TryGetDirectSourceSpanSetupForScalar(string sourceVar,out string sourceExpression,out string directSpanExpression) {
        sourceExpression=string.Empty;
        directSpanExpression=string.Empty;
        if(!this.CanUseDirectSpanPipelineScalarScan()) {
            return null;
        }
        try {
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        directSpanExpression=pipeline.Source.GetDirectSpanExpression(sourceVar)??string.Empty;
        if(directSpanExpression.Length==0) {
            return null;
        }
        return string.Empty;
    }

    private string? TryGetDirectSourceSpanSetup(string sourceVar,out string sourceExpression,out string directSpanExpression) {
        sourceExpression=string.Empty;
        directSpanExpression=string.Empty;
        if(!this.CanUseDirectSpanPipelineCopy()) {
            return null;
        }
        try {
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        directSpanExpression=pipeline.Source.GetDirectSpanExpression(sourceVar)??string.Empty;
        return directSpanExpression.Length==0 ? null : string.Empty;
    }

    private string AppendDirectSpanPipelineStages(StringBuilder builder,string currentName,string indent) {
        for(var index = 0;index<pipeline.Stages.Count;index++) {
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                    break;
                case PipelineStageKind.Cast: {
                    var nextName = $"__pipelineItem{this.result}_{index+1}";
                    builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                    currentName=nextName;
                    break;
                }
                case PipelineStageKind.Select: {
                    var nextName = $"__pipelineItem{this.result}_{index+1}";
                    builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                    currentName=nextName;
                    break;
                }
                default:
                    throw new NotSupportedException($"{stage.Kind} is not a direct span pipeline copy stage.");
            }
        }
        return currentName;
    }

    private void AppendDirectSpanPipelineScalarInit(StringBuilder builder,string spanName,string indent,HashSet<int> sliceIndices) {
        var skipDistinct = terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any;
        for(var index = 0;index<pipeline.Stages.Count;index++) {
            if(sliceIndices.Contains(index)) {
                continue;
            }
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.Take:
                    builder.AppendLine($"{indent}var __pipelineTake{this.result}_{index+1}=0;");
                    break;
                case PipelineStageKind.Skip:
                    builder.AppendLine($"{indent}var __pipelineSkip{this.result}_{index+1}=0;");
                    break;
                case PipelineStageKind.SkipWhile:
                    builder.AppendLine($"{indent}var __pipelineSkipWhileActive{this.result}_{index+1}=true;");
                    break;
                case PipelineStageKind.Distinct:
                    if(skipDistinct) {
                        break;
                    }
                    var keyTypeDistinct = stage.KeyType??stage.OutputType;
                    builder.AppendLine($"{indent}var __pipelineSet{this.result}_{index+1}=new global::System.Collections.Generic.HashSet<{keyTypeDistinct}>(global::System.Math.Min({spanName}.Length,1024));");
                    break;
                case PipelineStageKind.DistinctBy:
                    if(skipDistinct) {
                        break;
                    }
                    var keyTypeDistinctBy = stage.KeyType??stage.Func?.ReturnTypeName??"object";
                    builder.AppendLine($"{indent}var __pipelineSet{this.result}_{index+1}=new global::System.Collections.Generic.HashSet<{keyTypeDistinctBy}>(global::System.Math.Min({spanName}.Length,1024));");
                    break;
            }
        }
    }

    private string AppendDirectSpanPipelineScalarStages(StringBuilder builder,string currentName,string indent,HashSet<int> sliceIndices,int startIndex=0,int endIndex=-1) {
        if(endIndex<0) {
            endIndex=pipeline.Stages.Count;
        }
        var skipDistinct = terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any;
        for(var index = startIndex;index<endIndex;index++) {
            if(sliceIndices.Contains(index)) {
                continue;
            }
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                    break;
                case PipelineStageKind.Cast: {
                    var nextName = $"__pipelineItem{this.result}_{index+1}";
                    builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                    currentName=nextName;
                    break;
                }
                case PipelineStageKind.Select: {
                    var nextName = $"__pipelineItem{this.result}_{index+1}";
                    builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                    currentName=nextName;
                    break;
                }
                case PipelineStageKind.Where: {
                    var condName = $"__pipelineCond{this.result}_{index+1}";
                    builder.AppendLine($"{indent}bool {condName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Where),condName,currentName),indent));
                    builder.AppendLine($"{indent}if(!{condName}) continue;");
                    break;
                }
                case PipelineStageKind.Take: {
                    var takeName = $"__pipelineTake{this.result}_{index+1}";
                    builder.AppendLine($"{indent}if({takeName}>={stage.Value}) break;");
                    builder.AppendLine($"{indent}{takeName}++;");
                    break;
                }
                case PipelineStageKind.Skip: {
                    var skipName = $"__pipelineSkip{this.result}_{index+1}";
                    builder.AppendLine($"{indent}if({skipName}<{stage.Value}) {{ {skipName}++; continue; }}");
                    break;
                }
                case PipelineStageKind.TakeWhile: {
                    var condName = $"__pipelineTakeWhileCond{this.result}_{index+1}";
                    builder.AppendLine($"{indent}bool {condName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.TakeWhile),condName,currentName),indent));
                    builder.AppendLine($"{indent}if(!{condName}) break;");
                    break;
                }
                case PipelineStageKind.SkipWhile: {
                    var activeName = $"__pipelineSkipWhileActive{this.result}_{index+1}";
                    var condName = $"__pipelineSkipWhileCond{this.result}_{index+1}";
                    builder.AppendLine($"{indent}if({activeName}) {{");
                    builder.AppendLine($"{indent}    bool {condName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.SkipWhile),condName,currentName),$"{indent}    "));
                    builder.AppendLine($"{indent}    if({condName}) continue;");
                    builder.AppendLine($"{indent}    {activeName}=false;");
                    builder.AppendLine($"{indent}}}");
                    break;
                }
                case PipelineStageKind.Distinct: {
                    if(skipDistinct) {
                        break;
                    }
                    var setName = $"__pipelineSet{this.result}_{index+1}";
                    builder.AppendLine($"{indent}if(!{setName}.Add({currentName})) continue;");
                    break;
                }
                case PipelineStageKind.DistinctBy: {
                    if(skipDistinct) {
                        break;
                    }
                    var setName = $"__pipelineSet{this.result}_{index+1}";
                    var keyName = $"__pipelineKey{this.result}_{index+1}";
                    var keyType = stage.KeyType??stage.Func?.ReturnTypeName??"object";
                    builder.AppendLine($"{indent}{keyType} {keyName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.DistinctBy),keyName,currentName),indent));
                    builder.AppendLine($"{indent}if(!{setName}.Add({keyName})) continue;");
                    break;
                }
                default:
                    throw new NotSupportedException($"{stage.Kind} is not a direct span pipeline scalar stage.");
            }
        }
        return currentName;
    }

    private string? RenderDirectSpanPipelineCount(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasFilter = pipeline.Stages.Any(this.IsDirectSpanFilterStage);
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var extraCount = prefixPrepends.Count+prefixAppends.Count;
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        if(!hasFilter) {
            if(extraCount>0) {
                builder.AppendLine($"    {actualVariableName}={spanName}.Length+{extraCount};");
            } else {
                builder.AppendLine($"    {actualVariableName}={spanName}.Length;");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}++;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineAny(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasFilter = pipeline.Stages.Any(this.IsDirectSpanFilterStage);
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var extraCount = prefixPrepends.Count+prefixAppends.Count;
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        if(!hasFilter) {
            if(extraCount>0) {
                builder.AppendLine($"    {actualVariableName}=true;");
            } else {
                builder.AppendLine($"    {actualVariableName}={spanName}.Length>0;");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}=true;");
        builder.AppendLine("        break;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineTop1Order(string actualVariableName,string actualTypeName,int orderStageIndex) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var isReversed = this.IsPrefixReversed();
        var orderStage = pipeline.Stages[orderStageIndex];
        var isDescending = orderStage.Kind==PipelineStageKind.OrderByDescending;
        string itemBeforeOrderType;
        if(orderStageIndex==0) {
            itemBeforeOrderType=pipeline.ElementType;
        } else {
            itemBeforeOrderType=pipeline.Stages[orderStageIndex-1].OutputType;
        }
        var keyType = orderStage.KeyType;
        if(keyType is null) {
            if(orderStage.Func?.ReturnTypeName is { } retType) {
                keyType=retType;
            } else {
                keyType=itemBeforeOrderType;
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        var hasMinName = $"__pipelineHasTop{this.result}";
        var bestItemName = $"__pipelineBestItem{this.result}";
        var bestKeyName = $"__pipelineBestKey{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";

        builder.AppendLine($"var {hasMinName}=false;");
        builder.AppendLine($"{itemBeforeOrderType} {bestItemName}=default!;");
        builder.AppendLine($"{keyType} {bestKeyName}=default!;");

        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);

        if(isReversed) {
            builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentBeforeOrder = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices,0,orderStageIndex);

        var currentKeyName = $"__pipelineKey{this.result}_{orderStageIndex+1}";
        builder.AppendLine($"        {keyType} {currentKeyName}=default!;");
        if(orderStage.Func is not null) {
            builder.AppendLine(IndentLines(this.FuncAssignStatic(orderStage.Func,nameof(PipelineStageKind.OrderBy),currentKeyName,currentBeforeOrder),"        "));
        } else {
            builder.AppendLine($"        {currentKeyName}={currentBeforeOrder};");
        }

        string comparisonExpr;
        var hasCustomComparer = orderStage.Value is not null and not "null";
        var isPrimitive = IsPrimitiveComparableType(keyType);
        if(hasCustomComparer) {
            if(isDescending) {
                comparisonExpr = $"({orderStage.Value}.Compare({currentKeyName},{bestKeyName})>0)";
            } else {
                comparisonExpr = $"({orderStage.Value}.Compare({currentKeyName},{bestKeyName})<0)";
            }
        } else if(isPrimitive) {
            if(isDescending) {
                comparisonExpr = $"{currentKeyName}>{bestKeyName}";
            } else {
                comparisonExpr = $"{currentKeyName}<{bestKeyName}";
            }
        } else {
            if(isDescending) {
                comparisonExpr = $"(global::System.Collections.Generic.Comparer<{keyType}>.Default.Compare({currentKeyName},{bestKeyName})>0)";
            } else {
                comparisonExpr = $"(global::System.Collections.Generic.Comparer<{keyType}>.Default.Compare({currentKeyName},{bestKeyName})<0)";
            }
        }

        builder.AppendLine($"        if(!{hasMinName}) {{");
        builder.AppendLine($"            {hasMinName}=true;");
        builder.AppendLine($"            {bestKeyName}={currentKeyName};");
        builder.AppendLine($"            {bestItemName}={currentBeforeOrder};");
        builder.AppendLine($"        }} else if({comparisonExpr}) {{");
        builder.AppendLine($"            {bestKeyName}={currentKeyName};");
        builder.AppendLine($"            {bestItemName}={currentBeforeOrder};");
        builder.AppendLine($"        }}");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        if(terminalKind==PipelineTerminalKind.First) {
            builder.AppendLine($"if(!{hasMinName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        }

        builder.AppendLine($"if({hasMinName}) {{");
        var afterItem = bestItemName;
        for(var i = orderStageIndex+1;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind==PipelineStageKind.Cast) {
                var nextItem = $"__pipelineItem{this.result}_{i+1}";
                builder.AppendLine($"    var {nextItem}=({st.OutputType})(object?){afterItem};");
                afterItem=nextItem;
            } else if(st.Kind==PipelineStageKind.Select) {
                var nextItem = $"__pipelineItem{this.result}_{i+1}";
                builder.AppendLine($"    {st.OutputType} {nextItem}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(st.Func!,nameof(PipelineStageKind.Select),nextItem,afterItem),"    "));
                afterItem=nextItem;
            }
        }
        builder.AppendLine($"    {actualVariableName}={afterItem};");
        builder.AppendLine("}");

        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineFirst(string actualVariableName,string actualTypeName) {
        if(this.CanUseDirectSpanPipelineTop1Order(out var orderStageIndex)) {
            return this.RenderDirectSpanPipelineTop1Order(actualVariableName,actualTypeName,orderStageIndex);
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasFilter = pipeline.Stages.Any(this.IsDirectSpanFilterStage);
        var isReversed = this.IsPrefixReversed();
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        if(!hasFilter) {
            builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
            builder.AppendLine($"    var {spanName}={directSpanExpression};");
            this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
            if(prefixPrepends.Count>0) {
                builder.AppendLine($"    {actualVariableName}={prefixPrepends[0].Value};");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }
            if(isReversed) {
                if(prefixAppends.Count>0) {
                    builder.AppendLine($"    {actualVariableName}={prefixAppends[prefixAppends.Count-1].Value};");
                    builder.AppendLine("}");
                    return builder.ToString().TrimEnd();
                }
                builder.AppendLine($"    if({spanName}.Length==0) {{");
                builder.AppendLine($"        throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
                builder.AppendLine("    }");
                var sourceItemName = $"__pipelineItem{this.result}_0";
                builder.AppendLine($"    var {sourceItemName}={spanName}[{spanName}.Length-1];");
                var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"    ",sliceIndices);
                builder.AppendLine($"    {actualVariableName}={currentName};");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }
            builder.AppendLine($"    if({spanName}.Length==0) {{");
            if(prefixAppends.Count>0) {
                builder.AppendLine($"        {actualVariableName}={prefixAppends[0].Value};");
                builder.AppendLine("    } else {");
                var sourceItemName = $"__pipelineItem{this.result}_0";
                builder.AppendLine($"        var {sourceItemName}={spanName}[0];");
                var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
                builder.AppendLine($"        {actualVariableName}={currentName};");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }
            builder.AppendLine($"        throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("    }");
            var directItemName = $"__pipelineItem{this.result}_0";
            builder.AppendLine($"    var {directItemName}={spanName}[0];");
            var directCurrentName = this.AppendDirectSpanPipelineScalarStages(builder,directItemName,"    ",sliceIndices);
            builder.AppendLine($"    {actualVariableName}={directCurrentName};");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        var foundName = $"__pipelineFound{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var loopSourceItemName = $"__pipelineItem{this.result}_0";
        builder.AppendLine($"var {foundName}=false;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        if(isReversed) {
            builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"        var {loopSourceItemName}={spanName}[{indexName}];");
        var currentLoopName = this.AppendDirectSpanPipelineScalarStages(builder,loopSourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}={currentLoopName};");
        builder.AppendLine($"        {foundName}=true;");
        builder.AppendLine("        break;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine($"if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineFirstOrDefault(string actualVariableName,string actualTypeName) {
        if(this.CanUseDirectSpanPipelineTop1Order(out var orderStageIndex)) {
            return this.RenderDirectSpanPipelineTop1Order(actualVariableName,actualTypeName,orderStageIndex);
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasFilter = pipeline.Stages.Any(this.IsDirectSpanFilterStage);
        var isReversed = this.IsPrefixReversed();
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        if(!hasFilter) {
            builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
            builder.AppendLine($"    var {spanName}={directSpanExpression};");
            this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
            if(prefixPrepends.Count>0) {
                builder.AppendLine($"    {actualVariableName}={prefixPrepends[0].Value};");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }
            if(isReversed) {
                if(prefixAppends.Count>0) {
                    builder.AppendLine($"    {actualVariableName}={prefixAppends[prefixAppends.Count-1].Value};");
                    builder.AppendLine("}");
                    return builder.ToString().TrimEnd();
                }
                builder.AppendLine($"    if({spanName}.Length>0) {{");
                var sourceItemName = $"__pipelineItem{this.result}_0";
                builder.AppendLine($"        var {sourceItemName}={spanName}[{spanName}.Length-1];");
                var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
                builder.AppendLine($"        {actualVariableName}={currentName};");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }
            builder.AppendLine($"    if({spanName}.Length>0) {{");
            var directItemName = $"__pipelineItem{this.result}_0";
            builder.AppendLine($"        var {directItemName}={spanName}[0];");
            var directCurrentName = this.AppendDirectSpanPipelineScalarStages(builder,directItemName,"        ",sliceIndices);
            builder.AppendLine($"        {actualVariableName}={directCurrentName};");
            if(prefixAppends.Count>0) {
                builder.AppendLine("    } else {");
                builder.AppendLine($"        {actualVariableName}={prefixAppends[0].Value};");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        var indexName = $"__pipelineIndex{this.result}";
        var loopSourceItemName = $"__pipelineItem{this.result}_0";
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        if(isReversed) {
            builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"        var {loopSourceItemName}={spanName}[{indexName}];");
        var currentLoopName = this.AppendDirectSpanPipelineScalarStages(builder,loopSourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}={currentLoopName};");
        builder.AppendLine("        break;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineSum(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        foreach(var prep in prefixPrepends) {
            builder.AppendLine($"    {actualVariableName}+={prep.Value};");
        }
        foreach(var app in prefixAppends) {
            builder.AppendLine($"    {actualVariableName}+={app.Value};");
        }
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}+={currentName};");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToList(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetup(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var destinationName = $"__pipelineDestination{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        // Direct span pipeline copy avoids runtime TryCopyTo for count-preserving chains.
        // Example: `int[] src; src.Select(static x=>x+1).ToArray()` emits
        // `var __pipelineSpan=__pipelineSrc.GetSpan();` and fills the destination by index.
        // IEnumerable<T>, Tsavorite, and Persisted roots have no direct span expression, so
        // they keep the runtime fallback or backend-native scan path.
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    {actualVariableName}=new({spanName}.Length);");
        builder.AppendLine($"    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{spanName}.Length);");
        builder.AppendLine($"    var {destinationName}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"        ");
        builder.AppendLine($"        {destinationName}[{indexName}]={currentName};");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToArray(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetup(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        // See RenderDirectSpanPipelineToList: direct span roots prove the count at generation
        // time, so Select/AsEnumerable/identity Cast materialization can skip TryCopyTo.
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    if({spanName}.Length==0) {{");
        builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
        builder.AppendLine("    } else {");
        builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({spanName}.Length);");
        builder.AppendLine($"        for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"            var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"            ");
        builder.AppendLine($"            {actualVariableName}[{indexName}]={currentName};");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToListWithFilter(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasFilter = pipeline.Stages.Any(this.IsDirectSpanFilterStage);
        var isReversed = this.IsPrefixReversed();
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var extraCount = prefixPrepends.Count+prefixAppends.Count;
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        if(!hasFilter) {
            if(extraCount>0) {
                builder.AppendLine($"    {actualVariableName}=new({spanName}.Length+{extraCount});");
            } else {
                builder.AppendLine($"    {actualVariableName}=new({spanName}.Length);");
            }
            foreach(var prep in prefixPrepends) {
                builder.AppendLine($"    {actualVariableName}.Add({prep.Value});");
            }
            if(isReversed) {
                builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
            } else {
                builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
            }
            builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
            var currentDirectName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
            builder.AppendLine($"        {actualVariableName}.Add({currentDirectName});");
            builder.AppendLine("    }");
            foreach(var app in prefixAppends) {
                builder.AppendLine($"    {actualVariableName}.Add({app.Value});");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        builder.AppendLine($"    {actualVariableName}=new();");
        foreach(var prep in prefixPrepends) {
            builder.AppendLine($"    {actualVariableName}.Add({prep.Value});");
        }
        if(isReversed) {
            builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}.Add({currentName});");
        builder.AppendLine("    }");
        foreach(var app in prefixAppends) {
            builder.AppendLine($"    {actualVariableName}.Add({app.Value});");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToArrayWithFilter(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasFilter = pipeline.Stages.Any(this.IsDirectSpanFilterStage);
        var isReversed = this.IsPrefixReversed();
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var extraCount = prefixPrepends.Count+prefixAppends.Count;
        var indexName = $"__pipelineIndex{this.result}";
        var dstIndexName = $"__pipelineDstIndex{this.result}";
        var countVarName = $"__pipelineTotalCount{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        if(!hasFilter) {
            if(extraCount>0) {
                builder.AppendLine($"    var {countVarName}={spanName}.Length+{extraCount};");
            } else {
                builder.AppendLine($"    var {countVarName}={spanName}.Length;");
            }
            builder.AppendLine($"    if({countVarName}==0) {{");
            builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            builder.AppendLine("    } else {");
            builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countVarName});");
            builder.AppendLine($"        var {dstIndexName}=0;");
            foreach(var prep in prefixPrepends) {
                builder.AppendLine($"        {actualVariableName}[{dstIndexName}++]={prep.Value};");
            }
            builder.AppendLine($"        for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
            if(isReversed) {
                builder.AppendLine($"            var {sourceItemName}={spanName}[{spanName}.Length-1-{indexName}];");
            } else {
                builder.AppendLine($"            var {sourceItemName}={spanName}[{indexName}];");
            }
            var currentDirectName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"            ",sliceIndices);
            builder.AppendLine($"            {actualVariableName}[{dstIndexName}++]={currentDirectName};");
            builder.AppendLine("        }");
            foreach(var app in prefixAppends) {
                builder.AppendLine($"        {actualVariableName}[{dstIndexName}++]={app.Value};");
            }
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        var bufferName = $"__pipelineBuffer{this.result}";
        var countName = $"__pipelineCount{this.result}";
        if(extraCount>0) {
            builder.AppendLine($"    var {countVarName}={spanName}.Length+{extraCount};");
        } else {
            builder.AppendLine($"    var {countVarName}={spanName}.Length;");
        }
        builder.AppendLine($"    if({countVarName}==0) {{");
        builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
        builder.AppendLine("    } else {");
        builder.AppendLine($"        var {bufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({countVarName});");
        builder.AppendLine($"        var {countName}=0;");
        builder.AppendLine("        try {");
        foreach(var prep in prefixPrepends) {
            builder.AppendLine($"            {bufferName}[{countName}++]={prep.Value};");
        }
        if(isReversed) {
            builder.AppendLine($"            for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"            for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"                var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"                ",sliceIndices);
        builder.AppendLine($"                {bufferName}[{countName}++]={currentName};");
        builder.AppendLine("            }");
        foreach(var app in prefixAppends) {
            builder.AppendLine($"            {bufferName}[{countName}++]={app.Value};");
        }
        builder.AppendLine($"            if({countName}==0) {{");
        builder.AppendLine($"                {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
        builder.AppendLine("            } else {");
        builder.AppendLine($"                {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
        builder.AppendLine($"                global::System.Array.Copy({bufferName},{actualVariableName},{countName});");
        builder.AppendLine("            }");
        builder.AppendLine("        } finally {");
        builder.AppendLine($"            global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({bufferName},clearArray:true);");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        var builder = new StringBuilder();
        if(terminalKind==PipelineTerminalKind.ToList) {
            if(this.RenderDirectSpanPipelineToList(actualVariableName,actualTypeName) is { } directSpanPipeline) {
                return directSpanPipeline;
            }
            if(this.RenderDirectSpanPipelineToListWithFilter(actualVariableName,actualTypeName) is { } directSpanFilteredList) {
                return directSpanFilteredList;
            }
            // using ではなく try-finally なのは、遅経路のヘルパーへ enumerator を ref で渡すため。
            // using ローカルは読み取り専用で ref に渡せず、値渡しにするとコンストラクタで
            // プール配列を借りる enumerator（OrderBy 等）で二重 Return になる。
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine("try {");
            builder.AppendLine($"    if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            builder.AppendLine($"        {actualVariableName}=new(__count{this.result});");
            builder.AppendLine($"        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},__count{this.result});");
            builder.AppendLine($"        var __destination{this.result}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
            builder.AppendLine($"        if(!{this.EnumeratorName()}.TryCopyTo(__destination{this.result},0)) {{");
            builder.AppendLine($"            var __index{this.result}=0;");
            builder.AppendLine($"            while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) __destination{this.result}[__index{this.result}++]=__item{this.result};");
            builder.AppendLine("        }");
            builder.AppendLine($"    }}else{{");
            builder.AppendLine($"        {actualVariableName}=global::Seamless.Linq.GeneratedCodeHelpers.ToListFromValueEnumerator<{pipeline.EnumeratorTypeName},{pipeline.ElementType}>(ref {this.EnumeratorName()});");
            builder.AppendLine($"    }}");
            builder.AppendLine("}");
            builder.AppendLine("finally {");
            builder.AppendLine($"    {this.EnumeratorName()}.Dispose();");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToArray) {
            if(this.RenderDirectSpanPipelineToArray(actualVariableName,actualTypeName) is { } directSpanPipeline) {
                return directSpanPipeline;
            }
            if(this.RenderDirectSpanPipelineToArrayWithFilter(actualVariableName,actualTypeName) is { } directSpanFilteredArray) {
                return directSpanFilteredArray;
            }
            var listName = $"{actualVariableName}_list";
            var countName = $"{actualVariableName}_count";
            var indexName = $"{actualVariableName}_index";
            var bufferName = $"{actualVariableName}_buffer";
            var nName = $"{actualVariableName}_n";
            var nextName = $"{actualVariableName}_next";
            var endLabel = $"__arrayEnd{this.result}";
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var {countName})) {{");
            builder.AppendLine($"    {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
            builder.AppendLine($"    if({this.EnumeratorName()}.TryCopyTo(global::System.MemoryExtensions.AsSpan({actualVariableName}),0)) goto {endLabel};");
            builder.AppendLine($"    var {indexName}=0;");
            builder.AppendLine($"    while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"        if((uint){indexName}>=(uint){actualVariableName}.Length) {{");
            builder.AppendLine($"            var {listName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({actualVariableName});");
            builder.AppendLine($"            do {{");
            builder.AppendLine($"                {listName}.Add(__item{this.result});");
            builder.AppendLine($"            }} while({this.EnumeratorName()}.TryGetNext(out __item{this.result}));");
            builder.AppendLine($"            {actualVariableName}={listName}.ToArray();");
            builder.AppendLine($"            goto {endLabel};");
            builder.AppendLine($"        }}");
            builder.AppendLine($"        {actualVariableName}[{indexName}++]=__item{this.result};");
            builder.AppendLine($"    }}");
            builder.AppendLine($"    if({indexName}!={actualVariableName}.Length) global::System.Array.Resize(ref {actualVariableName},{indexName});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    {pipeline.ElementType}[]? {bufferName}=null;");
            builder.AppendLine($"    var {nName}=0;");
            builder.AppendLine($"    try {{");
            builder.AppendLine($"        while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"            if({bufferName} is null) {{");
            builder.AppendLine($"                {bufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent(16);");
            builder.AppendLine($"            }} else if({nName}=={bufferName}.Length) {{");
            builder.AppendLine($"                var {nextName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({bufferName}.Length*2);");
            builder.AppendLine($"                global::System.Array.Copy({bufferName},{nextName},{nName});");
            builder.AppendLine($"                global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({bufferName},clearArray:true);");
            builder.AppendLine($"                {bufferName}={nextName};");
            builder.AppendLine($"            }}");
            builder.AppendLine($"            {bufferName}[{nName}++]=__item{this.result};");
            builder.AppendLine($"        }}");
            builder.AppendLine($"        if({nName}==0) {{");
            builder.AppendLine($"            {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            builder.AppendLine($"        }} else {{");
            builder.AppendLine($"            {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({nName});");
            builder.AppendLine($"            global::System.Array.Copy({bufferName}!,{actualVariableName},{nName});");
            builder.AppendLine($"        }}");
            builder.AppendLine($"    }} finally {{");
            builder.AppendLine($"        if({bufferName} is not null) global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({bufferName},clearArray:true);");
            builder.AppendLine($"    }}");
            builder.AppendLine($"}}");
            builder.AppendLine($"{endLabel}:;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Count) {
            if(this.RenderDirectSpanPipelineCount(actualVariableName,actualTypeName) is { } directSpanCount) {
                return directSpanCount;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}++;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Any) {
            if(this.RenderDirectSpanPipelineAny(actualVariableName,actualTypeName) is { } directSpanAny) {
                return directSpanAny;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}=true;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.First) {
            if(this.RenderDirectSpanPipelineFirst(actualVariableName,actualTypeName) is { } directSpanFirst) {
                return directSpanFirst;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=__item{this.result};");
            builder.AppendLine("} else {");
            builder.AppendLine("    throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.FirstOrDefault) {
            if(this.RenderDirectSpanPipelineFirstOrDefault(actualVariableName,actualTypeName) is { } directSpanFirstOrDefault) {
                return directSpanFirstOrDefault;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=__item{this.result};");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            if(this.RenderDirectSpanPipelineSum(actualVariableName,actualTypeName) is { } directSpanSum) {
                return directSpanSum;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {actualVariableName}+=__item{this.result};");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToDictionary) {
            var func1Code = func1;
            var func2Code = func2;
            var cmpArg = comparerValue??"null";
            var keyType = func1Code?.ReturnTypeName??"object";
            var valType = func2Code?.ReturnTypeName??pipeline.ElementType;
            var dictTypeName = $"global::System.Collections.Generic.Dictionary<{keyType},{valType}>";
            var eName = this.EnumeratorName();
            var itemName = $"__dItem{this.result}";
            var keyName = $"__dKey{this.result}";
            var valName = $"__dVal{this.result}";
            builder.AppendLine($"using var {eName}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"if({eName}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=new {dictTypeName}(__count{this.result},(global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"}}else{{");
            builder.AppendLine($"    {actualVariableName}=new {dictTypeName}((global::System.Collections.Generic.IEqualityComparer<{keyType}>?){cmpArg});");
            builder.AppendLine($"}}");
            builder.AppendLine($"while({eName}.TryGetNext(out var {itemName})) {{");
            builder.AppendLine($"    {keyType} {keyName}=default!;");
            AppendIndented(builder,this.FuncAssign(func1Code!,"ToDictionary",keyName,itemName),"    ");
            if(func2Code is not null) {
                builder.AppendLine($"    {valType} {valName}=default!;");
                AppendIndented(builder,this.FuncAssign(func2Code,"ToDictionary",valName,itemName),"    ");
                builder.AppendLine($"    {actualVariableName}.Add({keyName},{valName});");
            } else {
                builder.AppendLine($"    {actualVariableName}.Add({keyName},({valType})(object?){itemName}!);");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        throw new NotSupportedException($"Unsupported pipeline terminal: {terminalKind}.");
    }

    private string EnumeratorName() => $"__pipeline{this.result}";
    private static void AppendIndented(StringBuilder sb,string text,string indent) {
        GeneratedCodeWriter.AppendIndented(sb,text,indent);
    }
}
