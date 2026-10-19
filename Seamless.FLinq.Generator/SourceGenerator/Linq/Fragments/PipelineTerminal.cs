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
internal sealed class PipelineTerminal(int 番号,PipelineEnumerable pipeline,PipelineTerminalKind terminalKind,FuncFragment? func1 = null,FuncFragment? func2 = null,string? comparerValue = null,string? terminalValue = null):Scalar(番号), DirectExecutionFragment {
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
            if(terminalKind is PipelineTerminalKind.Any or PipelineTerminalKind.All or PipelineTerminalKind.Contains) {
                return "bool";
            }
            if(terminalKind==PipelineTerminalKind.Count) {
                return "int";
            }
            if(terminalKind==PipelineTerminalKind.LongCount) {
                return "long";
            }
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
                return pipeline.ElementType;
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(pipeline.StructMethod,
        func1 is null ? string.Empty : this.FuncSupport(func1,nameof(func1))),
        func2 is null ? string.Empty : this.FuncSupport(func2,nameof(func2)));
    public override string GetEnumeratorStruct {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToHashSet or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum or PipelineTerminalKind.All or PipelineTerminalKind.Contains or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
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
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToHashSet or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum or PipelineTerminalKind.All or PipelineTerminalKind.Contains or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
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
            if(terminalKind==PipelineTerminalKind.LongCount) {
                return "long count = 0;\nwhile(source.TryGetNext(out _)) count++;\nreturn count;";
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
        if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any or PipelineTerminalKind.Contains) {
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

    private static bool IsIntegerOrDecimalType(string typeName) {
        switch(typeName) {
            case "int":
            case "long":
            case "short":
            case "byte":
            case "sbyte":
            case "uint":
            case "ulong":
            case "ushort":
            case "decimal":
            case "System.Int32":
            case "System.Int64":
            case "System.Int16":
            case "System.Byte":
            case "System.SByte":
            case "System.UInt32":
            case "System.UInt64":
            case "System.UInt16":
            case "System.Decimal":
                return true;
            default:
                return false;
        }
    }

    private bool CanIgnoreOrderForTerminal() {
        if(terminalKind is not (PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.Contains)) {
            if(terminalKind!=PipelineTerminalKind.Sum||!IsIntegerOrDecimalType(pipeline.ElementType)) {
                return false;
            }
        }
        var seenOrder = false;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var kind = pipeline.Stages[i].Kind;
            if(kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) {
                seenOrder=true;
                continue;
            }
            if(seenOrder) {
                if(kind is PipelineStageKind.Take or PipelineStageKind.Skip or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile) {
                    return false;
                }
            }
        }
        return seenOrder;
    }

    private bool TryGetSingleDefaultIfEmptyStage(out PipelineStage? defaultIfEmptyStage) {
        defaultIfEmptyStage=null;
        var count = 0;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            if(pipeline.Stages[i].Kind==PipelineStageKind.DefaultIfEmpty) {
                defaultIfEmptyStage=pipeline.Stages[i];
                count++;
            }
        }
        return count==1;
    }

    private bool CanUseDirectSpanPipelineScalarScan() {
        var isTop1Order = this.CanUseDirectSpanPipelineTop1Order(out var orderIdx);
        var canIgnoreOrder = this.CanIgnoreOrderForTerminal();
        var seenFilter = false;
        var skipDistinct = terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any or PipelineTerminalKind.Contains;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(isTop1Order&&i==orderIdx) {
                continue;
            }
            if(canIgnoreOrder&&stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) {
                continue;
            }
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.Cast:
                case PipelineStageKind.Take:
                case PipelineStageKind.Skip:
                    break;
                case PipelineStageKind.DefaultIfEmpty:
                    if(terminalKind is not (PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Sum)) {
                        return false;
                    }
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

    private bool CanUseDirectSpanBinaryShortCircuit(out PipelineStage? binaryStage,out int binaryStageIndex) {
        binaryStage=null;
        binaryStageIndex=-1;
        var count = 0;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy or PipelineStageKind.Concat
                or PipelineStageKind.Intersect or PipelineStageKind.IntersectBy or PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
                binaryStage=st;
                binaryStageIndex=i;
                count++;
            }
        }
        if(count!=1||binaryStage is null) {
            return false;
        }
        for(var i = 0;i<binaryStageIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is not (PipelineStageKind.AsEnumerable or PipelineStageKind.Cast)) {
                return false;
            }
        }
        for(var i = binaryStageIndex+1;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            if(st.Kind==PipelineStageKind.Select&&!st.UsesIndex&&st.Func is { } f&&CanUseDirectSpanPipelineFunc(f)) {
                continue;
            }
            return false;
        }
        return true;
    }

    private string AppendBinaryAfterStages(StringBuilder builder,string currentName,string indent,int binaryStageIndex) {
        for(var i = binaryStageIndex+1;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.Cast) {
                var nextName = $"__pipelineItem{this.result}_{i+1}";
                builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                currentName=nextName;
            } else if(stage.Kind==PipelineStageKind.Select) {
                var nextName = $"__pipelineItem{this.result}_{i+1}";
                builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                currentName=nextName;
            }
        }
        return currentName;
    }

    private bool TryGetDirectPrimarySourceSpanSetup(
        string firstSourceVar,
        out string firstSourceExpression,
        out string firstDirectSpanExpression) {
        firstSourceExpression=string.Empty;
        firstDirectSpanExpression=string.Empty;
        try {
            firstSourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return false;
        }
        firstDirectSpanExpression=pipeline.Source.GetDirectSpanExpression(firstSourceVar)??string.Empty;
        return firstDirectSpanExpression.Length>0;
    }

    private static bool TryGetDirectSecondSourceSpanSetup(
        PipelineStage binaryStage,
        string secondSourceVar,
        out string secondSourceExpression,
        out string secondDirectSpanExpression) {
        secondSourceExpression=string.Empty;
        secondDirectSpanExpression=string.Empty;
        if(binaryStage.SecondSource is not { } secondSource) {
            return false;
        }
        try {
            secondSourceExpression=secondSource.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return false;
        }
        secondDirectSpanExpression=secondSource.GetDirectSpanExpression(secondSourceVar)??string.Empty;
        return secondDirectSpanExpression.Length>0;
    }

    private string? RenderDirectSpanPipelineBinaryScalar(string actualVariableName,string actualTypeName) {
        if(!this.CanUseDirectSpanBinaryShortCircuit(out var binaryStage,out var binaryStageIndex)) {
            return null;
        }
        var firstSrcName = $"__firstSrc{this.result}";
        var secondSrcName = $"__secondSrc{this.result}";
        if(!this.TryGetDirectPrimarySourceSpanSetup(firstSrcName,out var firstSrcExpr,out var firstSpanExpr)) {
            return null;
        }
        if(!TryGetDirectSecondSourceSpanSetup(binaryStage!,secondSrcName,out var secondSrcExpr,out var secondSpanExpr)) {
            return null;
        }
        var firstSpanName = $"__firstSpan{this.result}";
        var secondSpanName = $"__secondSpan{this.result}";
        var builder = new StringBuilder();

        if(binaryStage!.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy or PipelineStageKind.Concat) {
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                builder.AppendLine($"    if({firstSpanName}.Length>0) {{");
                builder.AppendLine($"        {actualVariableName}=true;");
                builder.AppendLine("    } else {");
                builder.AppendLine($"        using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"            var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"            {actualVariableName}={secondSpanName}.Length>0;");
                builder.AppendLine("        }");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }

            if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                if(binaryStage.Kind==PipelineStageKind.Concat) {
                    builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
                    builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                    builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                    builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                    builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                    builder.AppendLine($"        {actualVariableName}={firstSpanName}.Length+{secondSpanName}.Length;");
                    builder.AppendLine("    }");
                    builder.AppendLine("}");
                    return builder.ToString().TrimEnd();
                }
                if(binaryStage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    var unionKeyType = binaryStage.KeyType;
                    if(unionKeyType is null) {
                        if(binaryStage.Func?.ReturnTypeName is { } retType) {
                            unionKeyType=retType;
                        } else {
                            unionKeyType=pipeline.ElementType;
                        }
                    }
                    builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
                    builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                    builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                    builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                    builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                    builder.AppendLine($"        var __set{this.result}=new global::System.Collections.Generic.HashSet<{unionKeyType}>();");
                    builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                    if(binaryStage.Func is not null) {
                        builder.AppendLine($"            {unionKeyType} __k{this.result}=default!;");
                        builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"UnionBy",$"__k{this.result}",$"{firstSpanName}[__i{this.result}]"),"            "));
                        builder.AppendLine($"            __set{this.result}.Add(__k{this.result});");
                    } else {
                        builder.AppendLine($"            __set{this.result}.Add({firstSpanName}[__i{this.result}]);");
                    }
                    builder.AppendLine("        }");
                    builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
                    if(binaryStage.Func is not null) {
                        builder.AppendLine($"            {unionKeyType} __k{this.result}=default!;");
                        builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"UnionBy",$"__k{this.result}",$"{secondSpanName}[__i{this.result}]"),"            "));
                        builder.AppendLine($"            __set{this.result}.Add(__k{this.result});");
                    } else {
                        builder.AppendLine($"            __set{this.result}.Add({secondSpanName}[__i{this.result}]);");
                    }
                    builder.AppendLine("        }");
                    builder.AppendLine($"        {actualVariableName}=__set{this.result}.Count;");
                    builder.AppendLine("    }");
                    builder.AppendLine("}");
                    return builder.ToString().TrimEnd();
                }
            }

            if(terminalKind==PipelineTerminalKind.Sum&&binaryStage.Kind==PipelineStageKind.Concat) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                builder.AppendLine($"    for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                builder.AppendLine($"        var __item{this.result}={firstSpanName}[__i{this.result}];");
                var sumItem1 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","        ",binaryStageIndex);
                builder.AppendLine($"        {actualVariableName}+={sumItem1};");
                builder.AppendLine("    }");
                builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
                builder.AppendLine($"            var __item{this.result}={secondSpanName}[__i{this.result}];");
                var sumItem2 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","            ",binaryStageIndex);
                builder.AppendLine($"            {actualVariableName}+={sumItem2};");
                builder.AppendLine("        }");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }

            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                builder.AppendLine($"    if({firstSpanName}.Length>0) {{");
                builder.AppendLine($"        var __item{this.result}={firstSpanName}[0];");
                var firstItem1 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","        ",binaryStageIndex);
                builder.AppendLine($"        {actualVariableName}={firstItem1};");
                builder.AppendLine("    } else {");
                builder.AppendLine($"        using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"            var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"            if({secondSpanName}.Length>0) {{");
                builder.AppendLine($"                var __item{this.result}={secondSpanName}[0];");
                var firstItem2 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                ",binaryStageIndex);
                builder.AppendLine($"                {actualVariableName}={firstItem2};");
                if(terminalKind==PipelineTerminalKind.First) {
                    builder.AppendLine("            } else {");
                    builder.AppendLine("                throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
                }
                builder.AppendLine("            }");
                builder.AppendLine("        }");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }
            return null;
        }

        var keyType = binaryStage.KeyType;
        if(keyType is null) {
            if(binaryStage.Func?.ReturnTypeName is { } retType) {
                keyType=retType;
            } else {
                keyType=pipeline.ElementType;
            }
        }
        var foundName = $"__found{this.result}";

        if(binaryStage.Kind is PipelineStageKind.Intersect or PipelineStageKind.IntersectBy) {
            if(terminalKind is not (PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount)) {
                return null;
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            } else {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            }
            builder.AppendLine($"var {foundName}=false;");
            builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
            builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
            builder.AppendLine($"    if({firstSpanName}.Length>0) {{");
            builder.AppendLine($"        using(var {secondSrcName}={secondSrcExpr}) {{");
            builder.AppendLine($"            var {secondSpanName}={secondSpanExpr};");
            builder.AppendLine($"            if({secondSpanName}.Length>0) {{");
            builder.AppendLine($"                var __set{this.result}=new global::System.Collections.Generic.HashSet<{keyType}>();");
            builder.AppendLine($"                for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
            if(binaryStage.Func is not null) {
                builder.AppendLine($"                    {keyType} __k{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"IntersectBy",$"__k{this.result}",$"{secondSpanName}[__i{this.result}]"),"                    "));
                builder.AppendLine($"                    __set{this.result}.Add(__k{this.result});");
            } else {
                builder.AppendLine($"                    __set{this.result}.Add({secondSpanName}[__i{this.result}]);");
            }
            builder.AppendLine("                }");
            builder.AppendLine($"                for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
            builder.AppendLine($"                    var __item{this.result}={firstSpanName}[__i{this.result}];");
            if(binaryStage.Func is not null) {
                builder.AppendLine($"                    {keyType} __k{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"IntersectBy",$"__k{this.result}",$"__item{this.result}"),"                    "));
                builder.AppendLine($"                    if(__set{this.result}.Remove(__k{this.result})) {{");
            } else {
                builder.AppendLine($"                    if(__set{this.result}.Remove(__item{this.result})) {{");
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"                        {actualVariableName}=true;");
                builder.AppendLine($"                        {foundName}=true;");
                builder.AppendLine("                        break;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                builder.AppendLine($"                        {actualVariableName}++;");
            } else {
                var hitItem = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                        ",binaryStageIndex);
                builder.AppendLine($"                        {actualVariableName}={hitItem};");
                builder.AppendLine($"                        {foundName}=true;");
                builder.AppendLine("                        break;");
            }
            builder.AppendLine("                    }");
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            if(terminalKind==PipelineTerminalKind.First) {
                builder.AppendLine($"if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            return builder.ToString().TrimEnd();
        }

        if(binaryStage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
            if(terminalKind is not (PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount)) {
                return null;
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            } else {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            }
            builder.AppendLine($"var {foundName}=false;");
            builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
            builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
            builder.AppendLine($"    if({firstSpanName}.Length>0) {{");
            builder.AppendLine($"        using(var {secondSrcName}={secondSrcExpr}) {{");
            builder.AppendLine($"            var {secondSpanName}={secondSpanExpr};");
            builder.AppendLine($"            var __set{this.result}=new global::System.Collections.Generic.HashSet<{keyType}>();");
            builder.AppendLine($"            for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
            if(binaryStage.Func is not null) {
                builder.AppendLine($"                {keyType} __k{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"ExceptBy",$"__k{this.result}",$"{secondSpanName}[__i{this.result}]"),"                "));
                builder.AppendLine($"                __set{this.result}.Add(__k{this.result});");
            } else {
                builder.AppendLine($"                __set{this.result}.Add({secondSpanName}[__i{this.result}]);");
            }
            builder.AppendLine("            }");
            builder.AppendLine($"            for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
            builder.AppendLine($"                var __item{this.result}={firstSpanName}[__i{this.result}];");
            if(binaryStage.Func is not null) {
                builder.AppendLine($"                {keyType} __k{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"ExceptBy",$"__k{this.result}",$"__item{this.result}"),"                "));
                builder.AppendLine($"                if(__set{this.result}.Add(__k{this.result})) {{");
            } else {
                builder.AppendLine($"                if(__set{this.result}.Add(__item{this.result})) {{");
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"                    {actualVariableName}=true;");
                builder.AppendLine($"                    {foundName}=true;");
                builder.AppendLine("                    break;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                builder.AppendLine($"                    {actualVariableName}++;");
            } else {
                var hitItem = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                    ",binaryStageIndex);
                builder.AppendLine($"                    {actualVariableName}={hitItem};");
                builder.AppendLine($"                    {foundName}=true;");
                builder.AppendLine("                    break;");
            }
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            if(terminalKind==PipelineTerminalKind.First) {
                builder.AppendLine($"if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            return builder.ToString().TrimEnd();
        }

        return null;
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
        var skipDistinct = terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any or PipelineTerminalKind.Contains;
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
                    builder.AppendLine($"{indent}var __pipelineSet{this.result}_{index+1}=new global::System.Collections.Generic.HashSet<{keyTypeDistinct}>();");
                    break;
                case PipelineStageKind.DistinctBy:
                    if(skipDistinct) {
                        break;
                    }
                    var keyTypeDistinctBy = stage.KeyType??stage.Func?.ReturnTypeName??"object";
                    builder.AppendLine($"{indent}var __pipelineSet{this.result}_{index+1}=new global::System.Collections.Generic.HashSet<{keyTypeDistinctBy}>();");
                    break;
            }
        }
    }

    private string AppendDirectSpanPipelineScalarStages(StringBuilder builder,string currentName,string indent,HashSet<int> sliceIndices,int startIndex=0,int endIndex=-1) {
        if(endIndex<0) {
            endIndex=pipeline.Stages.Count;
        }
        var skipDistinct = terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Any or PipelineTerminalKind.Contains;
        for(var index = startIndex;index<endIndex;index++) {
            if(sliceIndices.Contains(index)) {
                continue;
            }
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                case PipelineStageKind.OrderBy:
                case PipelineStageKind.OrderByDescending:
                case PipelineStageKind.ThenBy:
                case PipelineStageKind.ThenByDescending:
                case PipelineStageKind.DefaultIfEmpty:
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
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryCount) {
            return binaryCount;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var hasDefaultIfEmpty = this.TryGetSingleDefaultIfEmptyStage(out _);
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
            if(hasDefaultIfEmpty) {
                builder.AppendLine($"    if({actualVariableName}==0) {actualVariableName}=1;");
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
        if(hasDefaultIfEmpty) {
            builder.AppendLine($"if({actualVariableName}==0) {actualVariableName}=1;");
        }
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineAny(string actualVariableName,string actualTypeName) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryAny) {
            return binaryAny;
        }
        if(this.TryGetSingleDefaultIfEmptyStage(out _)) {
            return $"{actualTypeName} {actualVariableName}=true;";
        }
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
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryFirst) {
            return binaryFirst;
        }
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
        var hasDefaultIfEmpty = this.TryGetSingleDefaultIfEmptyStage(out var defaultIfEmptyStage);
        var defaultValue = defaultIfEmptyStage?.Value;
        if(defaultValue is null) {
            defaultValue="default!";
        }
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
                if(hasDefaultIfEmpty) {
                    builder.AppendLine($"        {actualVariableName}={defaultValue};");
                    builder.AppendLine("    }");
                    return builder.ToString().TrimEnd();
                } else {
                    builder.AppendLine($"        throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
                    builder.AppendLine("    }");
                }
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
            if(hasDefaultIfEmpty) {
                builder.AppendLine($"        {actualVariableName}={defaultValue};");
                builder.AppendLine("    }");
                return builder.ToString().TrimEnd();
            } else {
                builder.AppendLine($"        throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
                builder.AppendLine("    }");
            }
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
        if(hasDefaultIfEmpty) {
            builder.AppendLine($"if(!{foundName}) {actualVariableName}={defaultValue};");
        } else {
            builder.AppendLine($"if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        }
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineFirstOrDefault(string actualVariableName,string actualTypeName) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryFirstOrDefault) {
            return binaryFirstOrDefault;
        }
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
        var hasDefaultIfEmpty = this.TryGetSingleDefaultIfEmptyStage(out var defaultIfEmptyStage);
        var defaultValue = defaultIfEmptyStage?.Value;
        if(defaultValue is null) {
            defaultValue="default!";
        }
        var builder = new StringBuilder();
        if(hasDefaultIfEmpty) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}={defaultValue};");
        } else {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        }
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
            } else if(hasDefaultIfEmpty) {
                builder.AppendLine("    } else {");
                builder.AppendLine($"        {actualVariableName}={defaultValue};");
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
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binarySum) {
            return binarySum;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var hasDefaultIfEmpty = this.TryGetSingleDefaultIfEmptyStage(out var defaultIfEmptyStage);
        var defaultValue = defaultIfEmptyStage?.Value;
        if(defaultValue is null) {
            defaultValue="default!";
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
        if(hasDefaultIfEmpty) {
            builder.AppendLine($"    if({spanName}.Length==0) {{");
            builder.AppendLine($"        {actualVariableName}={defaultValue};");
            builder.AppendLine("    }");
        }
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

    private string? RenderDirectSpanPipelineToHashSet(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetup(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        var comparerArg = string.IsNullOrEmpty(comparerValue) ? string.Empty : $", {comparerValue}";
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    {actualVariableName}=new({spanName}.Length{comparerArg});");
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"        ");
        builder.AppendLine($"        {actualVariableName}.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToHashSetWithFilter(string actualVariableName,string actualTypeName) {
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
        var comparerArg = string.IsNullOrEmpty(comparerValue) ? string.Empty : comparerValue;
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        if(!hasFilter) {
            var capacityExpr = extraCount>0 ? $"{spanName}.Length+{extraCount}" : $"{spanName}.Length";
            var ctorArgs = string.IsNullOrEmpty(comparerArg) ? capacityExpr : $"{capacityExpr}, {comparerArg}";
            builder.AppendLine($"    {actualVariableName}=new({ctorArgs});");
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
        var filterCtorArgs = string.IsNullOrEmpty(comparerArg) ? string.Empty : comparerArg;
        builder.AppendLine($"    {actualVariableName}=new({filterCtorArgs});");
        foreach(var prep in prefixPrepends) {
            builder.AppendLine($"    {actualVariableName}.Add({prep.Value});");
        }
        if(isReversed) {
            builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentFilteredName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {actualVariableName}.Add({currentFilteredName});");
        builder.AppendLine("    }");
        foreach(var app in prefixAppends) {
            builder.AppendLine($"    {actualVariableName}.Add({app.Value});");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineAll(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=true;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentItem = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        bool __allCond{this.result}=default!;");
        AppendIndented(builder,this.FuncAssign(func1!,"All",$"__allCond{this.result}",currentItem),"        ");
        builder.AppendLine($"        if(!__allCond{this.result}) {{");
        builder.AppendLine($"            {actualVariableName}=false;");
        builder.AppendLine($"            break;");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineContains(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        var comparerExpr = comparerValue is null ? null : $"({comparerValue})";
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentItem = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        if(comparerExpr is not null) {
            builder.AppendLine($"        if({comparerExpr}.Equals({currentItem}, {terminalValue})) {{");
        } else {
            builder.AppendLine($"        if(global::System.Collections.Generic.EqualityComparer<{pipeline.ElementType}>.Default.Equals({currentItem}, {terminalValue})) {{");
        }
        builder.AppendLine($"            {actualVariableName}=true;");
        builder.AppendLine($"            break;");
        builder.AppendLine($"        }}");
        builder.AppendLine($"    }}");
        builder.AppendLine($"}}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineLast(string actualVariableName,string actualTypeName,bool orDefault) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var hasOrderDependentStage = pipeline.Stages.Any(static s => s.Kind is PipelineStageKind.Take or PipelineStageKind.Skip or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.Distinct or PipelineStageKind.DistinctBy or PipelineStageKind.TakeLast or PipelineStageKind.SkipLast);
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var builder = new StringBuilder();
        var foundName = $"__pipelineFound{this.result}";
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        builder.AppendLine($"bool {foundName}=false;");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        if(!hasOrderDependentStage) {
            builder.AppendLine($"    for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
            builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
            var currentItem = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
            builder.AppendLine($"        {actualVariableName}={currentItem};");
            builder.AppendLine($"        {foundName}=true;");
            builder.AppendLine($"        break;");
            builder.AppendLine($"    }}");
        } else {
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
            builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
            var currentItem = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
            builder.AppendLine($"        {actualVariableName}={currentItem};");
            builder.AppendLine($"        {foundName}=true;");
            builder.AppendLine($"    }}");
        }
        builder.AppendLine($"}}");
        if(!orDefault) {
            builder.AppendLine($"if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        }
        return builder.ToString().TrimEnd();
    }

    private bool CanUseConsumeBasedDirectScalar(out List<PipelineStage> afterStages) {
        afterStages=new List<PipelineStage>();
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                afterStages.Add(st);
                continue;
            }
            if(st.Kind==PipelineStageKind.Select&&!st.UsesIndex&&st.Func is { } f&&CanUseDirectSpanPipelineFunc(f)) {
                afterStages.Add(st);
                continue;
            }
            return false;
        }
        return true;
    }

    private string? TryRenderConsumeBasedDirectScalar(string actualVariableName,string actualTypeName) {
        if(terminalKind is not (PipelineTerminalKind.Sum or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault)) {
            return null;
        }
        if(pipeline.Source is not (Join or Zip or Zip_resultSelector)) {
            return null;
        }
        if(!this.CanUseConsumeBasedDirectScalar(out var afterStages)) {
            return null;
        }
        var placeholder = $"__FLINQ_CONSUME_{this.result}__";
        string consumeStatement;
        try {
            consumeStatement=pipeline.Source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
            return null;
        }

        var builder = new StringBuilder();
        var endLabel = $"__consumeEnd{this.result}";
        var foundVar = $"__consumeFound{this.result}";

        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
        } else if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var {foundVar}=false;");
        } else if(terminalKind==PipelineTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        }

        var escapedPlaceholder = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escapedPlaceholder}\.Consume\((?<arg>.+?)\);";
        var replacedStatement = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            var sb = new StringBuilder();
            var currentItem = arg;
            for(var i = 0;i<afterStages.Count;i++) {
                var st = afterStages[i];
                if(st.Kind==PipelineStageKind.Cast) {
                    var nextName = $"__stageItem{this.result}_{i+1}";
                    sb.AppendLine($"{indent}var {nextName}=({st.OutputType})(object?){currentItem};");
                    currentItem=nextName;
                } else if(st.Kind==PipelineStageKind.Select) {
                    var nextName = $"__stageItem{this.result}_{i+1}";
                    sb.AppendLine($"{indent}{st.OutputType} {nextName}=default!;");
                    sb.AppendLine(IndentLines(this.FuncAssignStatic(st.Func!,nameof(PipelineStageKind.Select),nextName,currentItem),indent));
                    currentItem=nextName;
                }
            }
            if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                sb.Append($"{indent}{actualVariableName}++;");
            } else if(terminalKind==PipelineTerminalKind.Sum) {
                sb.Append($"{indent}{actualVariableName}+={currentItem};");
            } else if(terminalKind==PipelineTerminalKind.Any) {
                sb.AppendLine($"{indent}{actualVariableName}=true;");
                sb.Append($"{indent}goto {endLabel};");
            } else if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
                sb.AppendLine($"{indent}{actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}{foundVar}=true;");
                sb.Append($"{indent}goto {endLabel};");
            }
            return sb.ToString();
        });

        builder.AppendLine(replacedStatement);

        if(terminalKind is PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
            builder.AppendLine($"{endLabel}:;");
            if(terminalKind==PipelineTerminalKind.First) {
                builder.AppendLine($"if(!{foundVar}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
        }

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
        if(terminalKind==PipelineTerminalKind.ToHashSet) {
            if(this.RenderDirectSpanPipelineToHashSet(actualVariableName,actualTypeName) is { } directSpanHashSet) {
                return directSpanHashSet;
            }
            if(this.RenderDirectSpanPipelineToHashSetWithFilter(actualVariableName,actualTypeName) is { } directSpanFilteredHashSet) {
                return directSpanFilteredHashSet;
            }
            var comparerArg = string.IsNullOrEmpty(comparerValue) ? string.Empty : comparerValue;
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNonEnumeratedCount(out var __count{this.result})) {{");
            var ctorWithCount = string.IsNullOrEmpty(comparerArg) ? $"__count{this.result}" : $"__count{this.result}, {comparerArg}";
            builder.AppendLine($"    {actualVariableName}=new({ctorWithCount});");
            builder.AppendLine($"}} else {{");
            var ctorWithoutCount = string.IsNullOrEmpty(comparerArg) ? string.Empty : comparerArg;
            builder.AppendLine($"    {actualVariableName}=new({ctorWithoutCount});");
            builder.AppendLine($"}}");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {actualVariableName}.Add(__item{this.result});");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            if(this.RenderDirectSpanPipelineCount(actualVariableName,actualTypeName) is { } directSpanCount) {
                return directSpanCount;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeCount) {
                return consumeCount;
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
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeAny) {
                return consumeAny;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out _)) {actualVariableName}=true;");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.All) {
            if(this.RenderDirectSpanPipelineAll(actualVariableName,actualTypeName) is { } directSpanAll) {
                return directSpanAll;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=true;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    bool __allCond{this.result}=default!;");
            AppendIndented(builder,this.FuncAssign(func1!,"All",$"__allCond{this.result}",$"__item{this.result}"),"    ");
            builder.AppendLine($"    if(!__allCond{this.result}) {{");
            builder.AppendLine($"        {actualVariableName}=false;");
            builder.AppendLine($"        break;");
            builder.AppendLine($"    }}");
            builder.AppendLine($"}}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Contains) {
            if(this.RenderDirectSpanPipelineContains(actualVariableName,actualTypeName) is { } directSpanContains) {
                return directSpanContains;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            var comparerExpr = comparerValue is null ? null : $"({comparerValue})";
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            if(comparerExpr is not null) {
                builder.AppendLine($"    if({comparerExpr}.Equals(__item{this.result}, {terminalValue})) {{");
            } else {
                builder.AppendLine($"    if(global::System.Collections.Generic.EqualityComparer<{pipeline.ElementType}>.Default.Equals(__item{this.result}, {terminalValue})) {{");
            }
            builder.AppendLine($"        {actualVariableName}=true;");
            builder.AppendLine($"        break;");
            builder.AppendLine($"    }}");
            builder.AppendLine($"}}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.First) {
            if(this.RenderDirectSpanPipelineFirst(actualVariableName,actualTypeName) is { } directSpanFirst) {
                return directSpanFirst;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeFirst) {
                return consumeFirst;
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
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeFirstOrDefault) {
                return consumeFirstOrDefault;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"if({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=__item{this.result};");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
            var orDefault = terminalKind==PipelineTerminalKind.LastOrDefault;
            if(this.RenderDirectSpanPipelineLast(actualVariableName,actualTypeName,orDefault) is { } directSpanLast) {
                return directSpanLast;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"bool __found{this.result}=false;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {actualVariableName}=__item{this.result};");
            builder.AppendLine($"    __found{this.result}=true;");
            builder.AppendLine($"}}");
            if(!orDefault) {
                builder.AppendLine($"if(!__found{this.result}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            if(this.RenderDirectSpanPipelineSum(actualVariableName,actualTypeName) is { } directSpanSum) {
                return directSpanSum;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeSum) {
                return consumeSum;
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
