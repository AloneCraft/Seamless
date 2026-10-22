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
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                return pipeline.ElementType;
            }
            if(terminalKind==PipelineTerminalKind.Aggregate) {
                if(func2 is not null) {
                    return func2.ReturnTypeName;
                }
                if(func1 is not null) {
                    return func1.ReturnTypeName;
                }
                return pipeline.ElementType;
            }
            throw new NotSupportedException($"{terminalKind} does not support scalar enumerator generation.");
        }
    }
    public override string StructMethod {
        get {
            var baseMethod=JoinStaticMethods(JoinStaticMethods(pipeline.StructMethod,
                func1 is null ? string.Empty : this.FuncSupport(func1,nameof(func1))),
                func2 is null ? string.Empty : this.FuncSupport(func2,nameof(func2)));
            if(this.CanUseDirectSpanPipelineOrder(out var orderStageIndex)
                ||this.CanUseGroupByAggregateWithOrder(out _,out orderStageIndex,out _)){
                var orderStage=pipeline.Stages[orderStageIndex];
                var isDescending=orderStage.Kind==PipelineStageKind.OrderByDescending;
                var keyType=orderStage.KeyType;
                if(keyType is null){
                    if(orderStage.Func?.ReturnTypeName is { } retType){
                        keyType=retType;
                    }else{
                        keyType=orderStage.InputType??pipeline.ElementType;
                    }
                }
                // クラス直下に置く補助型なので、補助クラス退避の正規表現（Struct\d+_接尾辞）にマッチする名前にしてメソッドごとに付け替えさせる。
                var comparerStructName=$"{this.Struct}_OrderDirectComparer";
                string descExpr;
                if(isDescending){
                    descExpr="-c";
                }else{
                    descExpr="c";
                }
                var structBuilder=new StringBuilder();
                structBuilder.AppendLine($"private readonly struct {comparerStructName} : global::System.Collections.Generic.IComparer<int> {{");
                structBuilder.AppendLine($"    private readonly {keyType}[] __keys;");
                structBuilder.AppendLine($"    private readonly global::System.Collections.Generic.IComparer<{keyType}>? __comparer;");
                structBuilder.AppendLine($"    public {comparerStructName}({keyType}[] keys,global::System.Collections.Generic.IComparer<{keyType}>? comparer) {{");
                structBuilder.AppendLine("        this.__keys=keys;");
                structBuilder.AppendLine("        this.__comparer=comparer;");
                structBuilder.AppendLine("    }");
                structBuilder.AppendLine("    public int Compare(int left,int right) {");
                structBuilder.AppendLine("        int c;");
                structBuilder.AppendLine("        if(this.__comparer is not null) {");
                structBuilder.AppendLine("            c=this.__comparer.Compare(this.__keys[left],this.__keys[right]);");
                structBuilder.AppendLine("        } else {");
                structBuilder.AppendLine($"            c=global::System.Collections.Generic.Comparer<{keyType}>.Default.Compare(this.__keys[left],this.__keys[right]);");
                structBuilder.AppendLine("        }");
                structBuilder.AppendLine("        if(c!=0) {");
                structBuilder.AppendLine($"            return {descExpr};");
                structBuilder.AppendLine("        } else {");
                structBuilder.AppendLine("            return left.CompareTo(right);");
                structBuilder.AppendLine("        }");
                structBuilder.AppendLine("    }");
                structBuilder.AppendLine("}");
                return JoinStaticMethods(baseMethod,structBuilder.ToString().TrimEnd());
            }else{
                return baseMethod;
            }
        }
    }
    public override string GetEnumeratorStruct {
        get {
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToHashSet or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum or PipelineTerminalKind.Average or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.Aggregate or PipelineTerminalKind.All or PipelineTerminalKind.Contains or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
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
            if(terminalKind is PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToHashSet or PipelineTerminalKind.ToDictionary or PipelineTerminalKind.Sum or PipelineTerminalKind.Average or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.Aggregate or PipelineTerminalKind.All or PipelineTerminalKind.Contains or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
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

    private bool IsPipelineReversed() {
        var count = 0;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.Reverse) {
                count++;
            }
        }
        return count%2!=0;
    }

    private void AppendDirectSpanPipelineSlices(StringBuilder builder,string spanName,string indent,HashSet<int> sliceIndices) {
        var isReversed = false;
        for(var index = 0;index<pipeline.Stages.Count;index++) {
            var stage = pipeline.Stages[index];
            if(stage.Kind==PipelineStageKind.Reverse) {
                isReversed=!isReversed;
            }
            if(!sliceIndices.Contains(index)) {
                continue;
            }
            var kind = stage.Kind;
            if(isReversed) {
                if(kind==PipelineStageKind.Take) {
                    kind=PipelineStageKind.TakeLast;
                } else if(kind==PipelineStageKind.Skip) {
                    kind=PipelineStageKind.SkipLast;
                } else if(kind==PipelineStageKind.TakeLast) {
                    kind=PipelineStageKind.Take;
                } else if(kind==PipelineStageKind.SkipLast) {
                    kind=PipelineStageKind.Skip;
                } else {
                }
            }
            switch(kind) {
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
        var seenStatefulFilter = false;
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
                    if(seenStatefulFilter) {
                        return false;
                    }
                    break;
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
                        seenStatefulFilter=true;
                    }
                    break;
                case PipelineStageKind.Select: {
                    if(stage.UsesIndex||stage.Func is not { } selectFunc||!CanUseDirectSpanPipelineFunc(selectFunc)) {
                        return false;
                    }
                    break;
                }
                case PipelineStageKind.Where:
                    if(stage.UsesIndex||stage.Func is not { } whereFunc||!CanUseDirectSpanPipelineFunc(whereFunc)) {
                        return false;
                    }
                    seenFilter=true;
                    break;
                case PipelineStageKind.TakeWhile:
                case PipelineStageKind.SkipWhile:
                    if(stage.UsesIndex||stage.Func is not { } filterFunc||!CanUseDirectSpanPipelineFunc(filterFunc)) {
                        return false;
                    }
                    seenFilter=true;
                    seenStatefulFilter=true;
                    break;
                case PipelineStageKind.DistinctBy: {
                    if(stage.UsesIndex||stage.Func is not { } distinctByFunc||!CanUseDirectSpanPipelineFunc(distinctByFunc)) {
                        return false;
                    }
                    if(!skipDistinct) {
                        seenFilter=true;
                        seenStatefulFilter=true;
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
        var firstFilterIndex = -1;
        for(var i = 0;i<binaryStageIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                firstFilterIndex=i;
                break;
            }
        }
        var firstSliceIndex = -1;
        for(var i = 0;i<binaryStageIndex;i++) {
            if(pipeline.Stages[i].Kind is PipelineStageKind.Skip or PipelineStageKind.Take or PipelineStageKind.SkipLast or PipelineStageKind.TakeLast) {
                firstSliceIndex=i;
                break;
            }
        }
        for(var i = 0;i<binaryStageIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            if(st.Kind is PipelineStageKind.Skip or PipelineStageKind.Take) {
                continue;
            }
            if(st.Kind is PipelineStageKind.SkipLast or PipelineStageKind.TakeLast) {
                if(firstFilterIndex>=0&&i>firstFilterIndex) {
                    return false;
                }
                continue;
            }
            if(st.Kind==PipelineStageKind.Select&&!st.UsesIndex&&st.Func is { } f&&CanUseDirectSpanPipelineFunc(f)) {
                continue;
            }
            if(st.Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile&&!st.UsesIndex&&st.Func is { } wf&&CanUseDirectSpanPipelineFunc(wf)) {
                continue;
            }
            if(firstSliceIndex<0) {
                if(st.Kind==PipelineStageKind.Distinct) {
                    continue;
                }
                if(this.CanIgnoreOrderForTerminal()&&st.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending) {
                    continue;
                }
            }
            return false;
        }
        for(var i = binaryStageIndex+1;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast or PipelineStageKind.Skip or PipelineStageKind.Take) {
                continue;
            }
            if(st.Kind==PipelineStageKind.Select&&!st.UsesIndex&&st.Func is { } f&&CanUseDirectSpanPipelineFunc(f)) {
                continue;
            }
            if(st.Kind==PipelineStageKind.Where&&!st.UsesIndex&&st.Func is { } wf&&CanUseDirectSpanPipelineFunc(wf)) {
                continue;
            }
            return false;
        }
        return true;
    }

    private int GetFirstBinaryPreFilterIndex(int binaryStageIndex) {
        for(var i = 0;i<binaryStageIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile or PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                return i;
            }
        }
        return -1;
    }

    private void AppendBinaryBeforeInit(StringBuilder builder,string indent,int binaryStageIndex) {
        var firstFilterIndex = this.GetFirstBinaryPreFilterIndex(binaryStageIndex);
        for(var i = 0;i<binaryStageIndex;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.SkipWhile) {
                var skipWhileFlag = $"__preSkipWhile{this.result}_{i+1}";
                builder.AppendLine($"{indent}bool {skipWhileFlag}=false;");
            } else if(firstFilterIndex>=0&&i>firstFilterIndex) {
                if(stage.Kind==PipelineStageKind.Take) {
                    var takeCountVar = $"__preTake{this.result}_{i+1}";
                    builder.AppendLine($"{indent}int {takeCountVar}=0;");
                } else if(stage.Kind==PipelineStageKind.Skip) {
                    var skipCountVar = $"__preSkip{this.result}_{i+1}";
                    builder.AppendLine($"{indent}int {skipCountVar}=0;");
                }
            }
        }
    }

    private void AppendBinaryBeforeSlices(StringBuilder builder,string spanName,string indent,int binaryStageIndex) {
        var firstFilterIndex = this.GetFirstBinaryPreFilterIndex(binaryStageIndex);
        for(var i = 0;i<binaryStageIndex;i++) {
            if(firstFilterIndex>=0&&i>firstFilterIndex) {
                continue;
            }
            var st = pipeline.Stages[i];
            if(st.Kind==PipelineStageKind.Skip) {
                builder.AppendLine($"{indent}if({spanName}.Length>{st.Value}) {{");
                builder.AppendLine($"{indent}    {spanName}={spanName}.Slice({st.Value});");
                builder.AppendLine($"{indent}}} else {{");
                builder.AppendLine($"{indent}    {spanName}=global::System.ReadOnlySpan<{pipeline.ElementType}>.Empty;");
                builder.AppendLine($"{indent}}}");
            } else if(st.Kind==PipelineStageKind.Take) {
                builder.AppendLine($"{indent}if({spanName}.Length>{st.Value}) {{");
                builder.AppendLine($"{indent}    {spanName}={spanName}.Slice(0,{st.Value});");
                builder.AppendLine($"{indent}}}");
            } else if(st.Kind==PipelineStageKind.SkipLast) {
                builder.AppendLine($"{indent}if({spanName}.Length>{st.Value}) {{");
                builder.AppendLine($"{indent}    {spanName}={spanName}.Slice(0,{spanName}.Length-{st.Value});");
                builder.AppendLine($"{indent}}} else {{");
                builder.AppendLine($"{indent}    {spanName}=global::System.ReadOnlySpan<{pipeline.ElementType}>.Empty;");
                builder.AppendLine($"{indent}}}");
            } else if(st.Kind==PipelineStageKind.TakeLast) {
                builder.AppendLine($"{indent}if({spanName}.Length>{st.Value}) {{");
                builder.AppendLine($"{indent}    {spanName}={spanName}.Slice({spanName}.Length-{st.Value});");
                builder.AppendLine($"{indent}}}");
            }
        }
    }

    private string AppendBinaryBeforeStages(StringBuilder builder,string currentName,string indent,int binaryStageIndex) {
        var firstFilterIndex = this.GetFirstBinaryPreFilterIndex(binaryStageIndex);
        for(var i = 0;i<binaryStageIndex;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.Cast) {
                var nextName = $"__preItem{this.result}_{i+1}";
                builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                currentName=nextName;
            } else if(stage.Kind==PipelineStageKind.Where) {
                var condName = $"__preCond{this.result}_{i+1}";
                builder.AppendLine($"{indent}bool {condName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Where),condName,currentName),indent));
                builder.AppendLine($"{indent}if(!{condName}) continue;");
            } else if(stage.Kind==PipelineStageKind.Select) {
                var nextName = $"__preItem{this.result}_{i+1}";
                builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                currentName=nextName;
            } else if(stage.Kind==PipelineStageKind.TakeWhile) {
                var condName = $"__preCond{this.result}_{i+1}";
                builder.AppendLine($"{indent}bool {condName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.TakeWhile),condName,currentName),indent));
                builder.AppendLine($"{indent}if(!{condName}) break;");
            } else if(stage.Kind==PipelineStageKind.SkipWhile) {
                var skipWhileFlag = $"__preSkipWhile{this.result}_{i+1}";
                builder.AppendLine($"{indent}if(!{skipWhileFlag}) {{");
                builder.AppendLine($"{indent}    bool __cond=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.SkipWhile),"__cond",currentName),$"{indent}    "));
                builder.AppendLine($"{indent}    if(__cond) continue;");
                builder.AppendLine($"{indent}    {skipWhileFlag}=true;");
                builder.AppendLine($"{indent}}}");
            } else if(firstFilterIndex>=0&&i>firstFilterIndex) {
                if(stage.Kind==PipelineStageKind.Take) {
                    var takeCountVar = $"__preTake{this.result}_{i+1}";
                    builder.AppendLine($"{indent}if(++{takeCountVar}>{stage.Value}) break;");
                } else if(stage.Kind==PipelineStageKind.Skip) {
                    var skipCountVar = $"__preSkip{this.result}_{i+1}";
                    builder.AppendLine($"{indent}if(++{skipCountVar}<={stage.Value}) continue;");
                }
            }
        }
        return currentName;
    }

    private void AppendBinaryAfterInit(StringBuilder builder,string indent,int binaryStageIndex) {
        for(var i = binaryStageIndex+1;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.Take) {
                var takeCountName = $"__postTake{this.result}_{i+1}";
                builder.AppendLine($"{indent}int {takeCountName}=0;");
            } else if(stage.Kind==PipelineStageKind.Skip) {
                var skipCountName = $"__postSkip{this.result}_{i+1}";
                builder.AppendLine($"{indent}int {skipCountName}=0;");
            }
        }
    }

    private string AppendBinaryAfterStages(StringBuilder builder,string currentName,string indent,int binaryStageIndex) {
        for(var i = binaryStageIndex+1;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(stage.Kind==PipelineStageKind.Cast) {
                var nextName = $"__pipelineItem{this.result}_{i+1}";
                builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                currentName=nextName;
            } else if(stage.Kind==PipelineStageKind.Where) {
                var condName = $"__pipelineCond{this.result}_{i+1}";
                builder.AppendLine($"{indent}bool {condName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Where),condName,currentName),indent));
                builder.AppendLine($"{indent}if(!{condName}) continue;");
            } else if(stage.Kind==PipelineStageKind.Select) {
                var nextName = $"__pipelineItem{this.result}_{i+1}";
                builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                currentName=nextName;
            } else if(stage.Kind==PipelineStageKind.Take) {
                var takeCountName = $"__postTake{this.result}_{i+1}";
                builder.AppendLine($"{indent}if(++{takeCountName}>{stage.Value}) break;");
            } else if(stage.Kind==PipelineStageKind.Skip) {
                var skipCountName = $"__postSkip{this.result}_{i+1}";
                builder.AppendLine($"{indent}if(++{skipCountName}<={stage.Value}) continue;");
            }
        }
        return currentName;
    }

    private static string RenderHashSetConstructorArgs(string capacityExpr,string? comparerExpr){
        if(string.IsNullOrEmpty(comparerExpr)){
            return capacityExpr;
        }else{
            return $"{capacityExpr},{comparerExpr}";
        }
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

    private bool CanUseDirectSpanUnionIntersectShortCircuit(
        out PipelineStage? unionStage,
        out int unionStageIndex,
        out PipelineStage? intersectStage,
        out int intersectStageIndex) {
        unionStage=null;
        unionStageIndex=-1;
        intersectStage=null;
        intersectStageIndex=-1;
        var binaryCount = 0;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy or PipelineStageKind.Concat
                or PipelineStageKind.Intersect or PipelineStageKind.IntersectBy or PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
                if(binaryCount==0) {
                    unionStage=st;
                    unionStageIndex=i;
                } else if(binaryCount==1) {
                    intersectStage=st;
                    intersectStageIndex=i;
                }
                binaryCount++;
            }
        }
        if(binaryCount!=2||unionStage is null||intersectStage is null) {
            return false;
        }
        if(unionStage.Kind is not (PipelineStageKind.Union or PipelineStageKind.UnionBy or PipelineStageKind.Concat)) {
            return false;
        }
        if(intersectStage.Kind is not (PipelineStageKind.Intersect or PipelineStageKind.IntersectBy)) {
            return false;
        }
        for(var i = 0;i<unionStageIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast or PipelineStageKind.Skip or PipelineStageKind.Take or PipelineStageKind.SkipLast or PipelineStageKind.TakeLast) {
                continue;
            }
            if(st.Kind==PipelineStageKind.Select&&!st.UsesIndex&&st.Func is { } f&&CanUseDirectSpanPipelineFunc(f)) {
                continue;
            }
            if(st.Kind is PipelineStageKind.Where or PipelineStageKind.TakeWhile or PipelineStageKind.SkipWhile&&!st.UsesIndex&&st.Func is { } wf&&CanUseDirectSpanPipelineFunc(wf)) {
                continue;
            }
            return false;
        }
        for(var i = unionStageIndex+1;i<intersectStageIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            return false;
        }
        for(var i = intersectStageIndex+1;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            if(st.Kind==PipelineStageKind.Select&&!st.UsesIndex&&st.Func is { } f&&CanUseDirectSpanPipelineFunc(f)) {
                continue;
            }
            if(st.Kind==PipelineStageKind.Where&&!st.UsesIndex&&st.Func is { } wf&&CanUseDirectSpanPipelineFunc(wf)) {
                continue;
            }
            return false;
        }
        return true;
    }

    private string? RenderDirectSpanPipelineUnionIntersect(string actualVariableName,string actualTypeName) {
        if(!this.CanUseDirectSpanUnionIntersectShortCircuit(out var unionStage,out var unionStageIndex,out var intersectStage,out var intersectStageIndex)) {
            return null;
        }
        if(terminalKind is not (PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Sum or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)) {
            return null;
        }
        var firstSrcName = $"__firstSrc{this.result}";
        var secondSrcName = $"__secondSrc{this.result}";
        var thirdSrcName = $"__thirdSrc{this.result}";
        if(!this.TryGetDirectPrimarySourceSpanSetup(firstSrcName,out var firstSrcExpr,out var firstSpanExpr)) {
            return null;
        }
        if(!TryGetDirectSecondSourceSpanSetup(unionStage!,secondSrcName,out var secondSrcExpr,out var secondSpanExpr)) {
            return null;
        }
        if(!TryGetDirectSecondSourceSpanSetup(intersectStage!,thirdSrcName,out var thirdSrcExpr,out var thirdSpanExpr)) {
            return null;
        }
        var firstSpanName = $"__firstSpan{this.result}";
        var secondSpanName = $"__secondSpan{this.result}";
        var thirdSpanName = $"__thirdSpan{this.result}";
        var foundName = $"__found{this.result}";
        var builder = new StringBuilder();

        var keyType = intersectStage!.KeyType;
        if(keyType is null) {
            if(intersectStage.Func?.ReturnTypeName is { } retType) {
                keyType=retType;
            } else {
                keyType=intersectStage.OutputType;
            }
        }

        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
        } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
        } else {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        }
        if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
            builder.AppendLine($"var {foundName}=false;");
        }

        var rentedBufferName = $"__rentedBuffer{this.result}";
        var countName = $"__count{this.result}";
        var maxCountName = $"__maxCount{this.result}";

        builder.AppendLine($"using(var {thirdSrcName}={thirdSrcExpr}) {{");
        builder.AppendLine($"    var {thirdSpanName}={thirdSpanExpr};");
        builder.AppendLine($"    if({thirdSpanName}.Length>0) {{");
        string capExpr;
        if(intersectStage.Func is not null) {
            capExpr=$"global::System.Math.Min({thirdSpanName}.Length,32)";
        } else {
            capExpr=$"{thirdSpanName}.Length";
        }
        var setArgs=RenderHashSetConstructorArgs(capExpr,intersectStage.Value);
        builder.AppendLine($"        var __set{this.result}=new global::System.Collections.Generic.HashSet<{keyType}>({setArgs});");
        builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{thirdSpanName}.Length;__i{this.result}++) {{");
        // IntersectBy の第2引数は要素ではなく鍵そのものなので、鍵選択関数は通さずそのまま集合へ足す。
        builder.AppendLine($"            __set{this.result}.Add({thirdSpanName}[__i{this.result}]);");
        builder.AppendLine("        }");

        builder.AppendLine($"        using(var {firstSrcName}={firstSrcExpr}) {{");
        builder.AppendLine($"            var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"            ",unionStageIndex);
        this.AppendBinaryBeforeInit(builder,"            ",unionStageIndex);
        builder.AppendLine($"            using(var {secondSrcName}={secondSrcExpr}) {{");
        builder.AppendLine($"                var {secondSpanName}={secondSpanExpr};");

        var innerIndent = "                ";
        if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
            builder.AppendLine($"{innerIndent}var {maxCountName}=global::System.Math.Min({firstSpanName}.Length+{secondSpanName}.Length,{thirdSpanName}.Length);");
            builder.AppendLine($"{innerIndent}var {rentedBufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({maxCountName});");
            builder.AppendLine($"{innerIndent}var {countName}=0;");
            builder.AppendLine($"{innerIndent}try {{");
            innerIndent="                    ";
        }

        // firstSpan 走査
        builder.AppendLine($"{innerIndent}for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
        builder.AppendLine($"{innerIndent}    var __item{this.result}={firstSpanName}[__i{this.result}];");
        var filteredItem1 = this.AppendBinaryBeforeStages(builder,$"__item{this.result}",$"{innerIndent}    ",unionStageIndex);
        if(intersectStage.Func is not null) {
            builder.AppendLine($"{innerIndent}    {keyType} __k{this.result}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(intersectStage.Func,"IntersectBy",$"__k{this.result}",filteredItem1),$"{innerIndent}    "));
            builder.AppendLine($"{innerIndent}    if(__set{this.result}.Remove(__k{this.result})) {{");
        } else {
            builder.AppendLine($"{innerIndent}    if(__set{this.result}.Remove({filteredItem1})) {{");
        }
        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            builder.AppendLine($"{innerIndent}        {actualVariableName}++;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{innerIndent}        {actualVariableName}=true;");
            builder.AppendLine($"{innerIndent}        goto __end{this.result};");
        } else if(terminalKind==PipelineTerminalKind.Sum) {
            var hitItem = this.AppendBinaryAfterStages(builder,filteredItem1,$"{innerIndent}        ",intersectStageIndex);
            builder.AppendLine($"{innerIndent}        {actualVariableName}+={hitItem};");
        } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
            var hitItem = this.AppendBinaryAfterStages(builder,filteredItem1,$"{innerIndent}        ",intersectStageIndex);
            builder.AppendLine($"{innerIndent}        {rentedBufferName}[{countName}++]={hitItem};");
        } else {
            var hitItem = this.AppendBinaryAfterStages(builder,filteredItem1,$"{innerIndent}        ",intersectStageIndex);
            builder.AppendLine($"{innerIndent}        {actualVariableName}={hitItem};");
            builder.AppendLine($"{innerIndent}        {foundName}=true;");
            builder.AppendLine($"{innerIndent}        goto __end{this.result};");
        }
        builder.AppendLine($"{innerIndent}    }}");
        builder.AppendLine($"{innerIndent}}}");

        // secondSpan 走査
        builder.AppendLine($"{innerIndent}for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
        builder.AppendLine($"{innerIndent}    var __item{this.result}={secondSpanName}[__i{this.result}];");
        if(intersectStage.Func is not null) {
            builder.AppendLine($"{innerIndent}    {keyType} __k{this.result}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(intersectStage.Func,"IntersectBy",$"__k{this.result}",$"__item{this.result}"),$"{innerIndent}    "));
            builder.AppendLine($"{innerIndent}    if(__set{this.result}.Remove(__k{this.result})) {{");
        } else {
            builder.AppendLine($"{innerIndent}    if(__set{this.result}.Remove(__item{this.result})) {{");
        }
        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            builder.AppendLine($"{innerIndent}        {actualVariableName}++;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{innerIndent}        {actualVariableName}=true;");
            builder.AppendLine($"{innerIndent}        goto __end{this.result};");
        } else if(terminalKind==PipelineTerminalKind.Sum) {
            var hitItem = this.AppendBinaryAfterStages(builder,$"__item{this.result}",$"{innerIndent}        ",intersectStageIndex);
            builder.AppendLine($"{innerIndent}        {actualVariableName}+={hitItem};");
        } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
            var hitItem = this.AppendBinaryAfterStages(builder,$"__item{this.result}",$"{innerIndent}        ",intersectStageIndex);
            builder.AppendLine($"{innerIndent}        {rentedBufferName}[{countName}++]={hitItem};");
        } else {
            var hitItem = this.AppendBinaryAfterStages(builder,$"__item{this.result}",$"{innerIndent}        ",intersectStageIndex);
            builder.AppendLine($"{innerIndent}        {actualVariableName}={hitItem};");
            builder.AppendLine($"{innerIndent}        {foundName}=true;");
            builder.AppendLine($"{innerIndent}        goto __end{this.result};");
        }
        builder.AppendLine($"{innerIndent}    }}");
        builder.AppendLine($"{innerIndent}}}");

        if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
            if(terminalKind==PipelineTerminalKind.ToArray) {
                builder.AppendLine($"                    if({countName}==0) {{");
                builder.AppendLine($"                        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                builder.AppendLine("                    } else {");
                builder.AppendLine($"                        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
                builder.AppendLine($"                        global::System.Array.Copy({rentedBufferName},{actualVariableName},{countName});");
                builder.AppendLine("                    }");
            } else {
                builder.AppendLine($"                    {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({countName});");
                builder.AppendLine($"                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{countName});");
                builder.AppendLine($"                    global::System.MemoryExtensions.AsSpan({rentedBufferName},0,{countName}).CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName}));");
            }
            builder.AppendLine("                } finally {");
            builder.AppendLine($"                    global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({rentedBufferName},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
            builder.AppendLine("                }");
        }

        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
            builder.AppendLine("    else {");
            if(terminalKind==PipelineTerminalKind.ToArray) {
                builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            } else {
                builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
            }
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");

        if(terminalKind is PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
            builder.AppendLine($"__end{this.result}:;");
            if(terminalKind==PipelineTerminalKind.First) {
                builder.AppendLine($"if(!{foundName}) {{");
                builder.AppendLine("    throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
                builder.AppendLine("}");
            }
        }
        return builder.ToString().TrimEnd();
    }

    private bool ConcatFollowedByOneToOneStagesOnly(int binaryStageIndex) {
        for(var i = binaryStageIndex+1;i<pipeline.Stages.Count;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is not (PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)||st.UsesIndex) {
                return false;
            }
        }
        return true;
    }

    private string? RenderDirectSpanPipelineBinaryScalar(string actualVariableName,string actualTypeName) {
        if(this.RenderDirectSpanPipelineUnionIntersect(actualVariableName,actualTypeName) is { } unionIntersectResult) {
            return unionIntersectResult;
        }
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
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
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

            if(terminalKind==PipelineTerminalKind.Contains) {
                var comparerExpr = comparerValue is null ? null : $"({comparerValue})";
                string equalsCheck(string itm) {
                    if(comparerExpr is not null) {
                        return $"{comparerExpr}.Equals({itm}, {terminalValue})";
                    } else {
                        return $"global::System.Collections.Generic.EqualityComparer<{pipeline.ElementType}>.Default.Equals({itm}, {terminalValue})";
                    }
                }
                builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
                this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
                this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                builder.AppendLine($"    for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                builder.AppendLine($"        var __item{this.result}={firstSpanName}[__i{this.result}];");
                var filteredFirst = this.AppendBinaryBeforeStages(builder,$"__item{this.result}","        ",binaryStageIndex);
                var hitFirst = this.AppendBinaryAfterStages(builder,filteredFirst,"        ",binaryStageIndex);
                builder.AppendLine($"        if({equalsCheck(hitFirst)}) {{");
                builder.AppendLine($"            {actualVariableName}=true;");
                builder.AppendLine($"            break;");
                builder.AppendLine($"        }}");
                builder.AppendLine($"    }}");
                builder.AppendLine($"    if(!{actualVariableName}) {{");
                builder.AppendLine($"        using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"            var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"            for(var __j{this.result}=0;__j{this.result}<{secondSpanName}.Length;__j{this.result}++) {{");
                builder.AppendLine($"                var __item2{this.result}={secondSpanName}[__j{this.result}];");
                var hitSecond = this.AppendBinaryAfterStages(builder,$"__item2{this.result}","                ",binaryStageIndex);
                builder.AppendLine($"                if({equalsCheck(hitSecond)}) {{");
                builder.AppendLine($"                    {actualVariableName}=true;");
                builder.AppendLine($"                    break;");
                builder.AppendLine($"                }}");
                builder.AppendLine($"            }}");
                builder.AppendLine($"        }}");
                builder.AppendLine($"    }}");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }

            if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                if(binaryStage.Kind==PipelineStageKind.Concat) {
                    builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
                    builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                    builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
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
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                    builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                    builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                    string capExpr;
                    if(binaryStage.Func is not null){
                        capExpr=$"global::System.Math.Min({firstSpanName}.Length+{secondSpanName}.Length,32)";
                    }else{
                        capExpr=$"{firstSpanName}.Length+{secondSpanName}.Length";
                    }
                    var setArgs=RenderHashSetConstructorArgs(capExpr,binaryStage.Value);
                    builder.AppendLine($"        var __set{this.result}=new global::System.Collections.Generic.HashSet<{unionKeyType}>({setArgs});");
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

            if(terminalKind==PipelineTerminalKind.Sum) {
                if(binaryStage.Kind==PipelineStageKind.Concat) {
                    builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                    builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                    builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
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
                if(binaryStage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    var unionKeyType = binaryStage.KeyType??binaryStage.Func?.ReturnTypeName??binaryStage.OutputType;
                    builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                    builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                    builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                    builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                    builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                    string capExpr;
                    if(binaryStage.Func is not null){
                        capExpr=$"global::System.Math.Min({firstSpanName}.Length+{secondSpanName}.Length,32)";
                    }else{
                        capExpr=$"{firstSpanName}.Length+{secondSpanName}.Length";
                    }
                    var setArgs=RenderHashSetConstructorArgs(capExpr,binaryStage.Value);
                    builder.AppendLine($"        var __set{this.result}=new global::System.Collections.Generic.HashSet<{unionKeyType}>({setArgs});");
                    builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                    builder.AppendLine($"            var __item{this.result}={firstSpanName}[__i{this.result}];");
                    if(binaryStage.Func is not null) {
                        builder.AppendLine($"            {unionKeyType} __k{this.result}=default!;");
                        builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"UnionBy",$"__k{this.result}",$"__item{this.result}"),"            "));
                        builder.AppendLine($"            if(__set{this.result}.Add(__k{this.result})) {{");
                    } else {
                        builder.AppendLine($"            if(__set{this.result}.Add(__item{this.result})) {{");
                    }
                    var sumItem1 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                ",binaryStageIndex);
                    builder.AppendLine($"                {actualVariableName}+={sumItem1};");
                    builder.AppendLine("            }");
                    builder.AppendLine("        }");
                    builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
                    builder.AppendLine($"            var __item{this.result}={secondSpanName}[__i{this.result}];");
                    if(binaryStage.Func is not null) {
                        builder.AppendLine($"            {unionKeyType} __k{this.result}=default!;");
                        builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"UnionBy",$"__k{this.result}",$"__item{this.result}"),"            "));
                        builder.AppendLine($"            if(__set{this.result}.Add(__k{this.result})) {{");
                    } else {
                        builder.AppendLine($"            if(__set{this.result}.Add(__item{this.result})) {{");
                    }
                    var sumItem2 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                ",binaryStageIndex);
                    builder.AppendLine($"                {actualVariableName}+={sumItem2};");
                    builder.AppendLine("            }");
                    builder.AppendLine("        }");
                    builder.AppendLine("    }");
                    builder.AppendLine("}");
                    return builder.ToString().TrimEnd();
                }
            }

            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
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

            // 件数を先に確定して添字で書き込む経路なので、Concat の後ろが 1 件を 1 件へ写す段(Select など)だけのときに限る。
            // Where・Skip・Take などが続くと出力が詰まらず、添字がずれて既定値が残る。
            var concatKeepsOneToOne = this.ConcatFollowedByOneToOneStagesOnly(binaryStageIndex);
            if(terminalKind==PipelineTerminalKind.ToArray&&binaryStage.Kind==PipelineStageKind.Concat&&concatKeepsOneToOne) {
                var totalCountName = $"__totalCount{this.result}";
                var destSpanName = $"__destSpan{this.result}";
                var hasAfterStages = binaryStageIndex+1<pipeline.Stages.Count;
                builder.AppendLine($"{actualTypeName} {actualVariableName};");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"        var {totalCountName}={firstSpanName}.Length+{secondSpanName}.Length;");
                builder.AppendLine($"        if({totalCountName}==0) {{");
                builder.AppendLine($"            {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                builder.AppendLine("        } else {");
                builder.AppendLine($"            {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({totalCountName});");
                builder.AppendLine($"            var {destSpanName}=global::System.MemoryExtensions.AsSpan({actualVariableName});");
                if(!hasAfterStages) {
                    builder.AppendLine($"            {firstSpanName}.CopyTo({destSpanName}.Slice(0,{firstSpanName}.Length));");
                    builder.AppendLine($"            {secondSpanName}.CopyTo({destSpanName}.Slice({firstSpanName}.Length));");
                } else {
                    builder.AppendLine($"            for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                    builder.AppendLine($"                var __item{this.result}={firstSpanName}[__i{this.result}];");
                    var item1 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                ",binaryStageIndex);
                    builder.AppendLine($"                {destSpanName}[__i{this.result}]={item1};");
                    builder.AppendLine("            }");
                    builder.AppendLine($"            for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
                    builder.AppendLine($"                var __item{this.result}={secondSpanName}[__i{this.result}];");
                    var item2 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                ",binaryStageIndex);
                    builder.AppendLine($"                {destSpanName}[{firstSpanName}.Length+__i{this.result}]={item2};");
                    builder.AppendLine("            }");
                }
                builder.AppendLine("        }");
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }

            if(terminalKind==PipelineTerminalKind.ToList&&binaryStage.Kind==PipelineStageKind.Concat&&concatKeepsOneToOne) {
                var totalCountName = $"__totalCount{this.result}";
                var destSpanName = $"__destSpan{this.result}";
                var hasAfterStages = binaryStageIndex+1<pipeline.Stages.Count;
                builder.AppendLine($"{actualTypeName} {actualVariableName};");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"        var {totalCountName}={firstSpanName}.Length+{secondSpanName}.Length;");
                builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({totalCountName});");
                builder.AppendLine($"        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{totalCountName});");
                builder.AppendLine($"        var {destSpanName}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
                if(!hasAfterStages) {
                    builder.AppendLine($"        {firstSpanName}.CopyTo({destSpanName}.Slice(0,{firstSpanName}.Length));");
                    builder.AppendLine($"        {secondSpanName}.CopyTo({destSpanName}.Slice({firstSpanName}.Length));");
                } else {
                    builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                    builder.AppendLine($"            var __item{this.result}={firstSpanName}[__i{this.result}];");
                    var item1 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","            ",binaryStageIndex);
                    builder.AppendLine($"            {destSpanName}[__i{this.result}]={item1};");
                    builder.AppendLine("        }");
                    builder.AppendLine($"        for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
                    builder.AppendLine($"            var __item{this.result}={secondSpanName}[__i{this.result}];");
                    var item2 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","            ",binaryStageIndex);
                    builder.AppendLine($"            {destSpanName}[{firstSpanName}.Length+__i{this.result}]={item2};");
                    builder.AppendLine("        }");
                }
                builder.AppendLine("    }");
                builder.AppendLine("}");
                return builder.ToString().TrimEnd();
            }

            if((terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)&&(binaryStage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy)) {
                var unionKeyType = binaryStage.KeyType??binaryStage.Func?.ReturnTypeName??binaryStage.OutputType;
                var maxCountName = $"__maxCount{this.result}";
                var rentedBufferName = $"__rentedBuffer{this.result}";
                var countName = $"__count{this.result}";
                builder.AppendLine($"{actualTypeName} {actualVariableName};");
                builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
                builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
                builder.AppendLine($"    using(var {secondSrcName}={secondSrcExpr}) {{");
                builder.AppendLine($"        var {secondSpanName}={secondSpanExpr};");
                builder.AppendLine($"        var {maxCountName}={firstSpanName}.Length+{secondSpanName}.Length;");
                builder.AppendLine($"        if({maxCountName}==0) {{");
                if(terminalKind==PipelineTerminalKind.ToArray) {
                    builder.AppendLine($"            {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                } else {
                    builder.AppendLine($"            {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
                }
                builder.AppendLine("        } else {");
                builder.AppendLine($"            var {rentedBufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({maxCountName});");
                builder.AppendLine($"            var {countName}=0;");
                builder.AppendLine("            try {");
                string capExpr;
                if(binaryStage.Func is not null){
                    capExpr=$"global::System.Math.Min({maxCountName},32)";
                }else{
                    capExpr=maxCountName;
                }
                var setArgs=RenderHashSetConstructorArgs(capExpr,binaryStage.Value);
                builder.AppendLine($"                var __set{this.result}=new global::System.Collections.Generic.HashSet<{unionKeyType}>({setArgs});");
                builder.AppendLine($"                for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
                builder.AppendLine($"                    var __item{this.result}={firstSpanName}[__i{this.result}];");
                if(binaryStage.Func is not null) {
                    builder.AppendLine($"                    {unionKeyType} __k{this.result}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"UnionBy",$"__k{this.result}",$"__item{this.result}"),"                    "));
                    builder.AppendLine($"                    if(__set{this.result}.Add(__k{this.result})) {{");
                } else {
                    builder.AppendLine($"                    if(__set{this.result}.Add(__item{this.result})) {{");
                }
                var item1 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                        ",binaryStageIndex);
                builder.AppendLine($"                        {rentedBufferName}[{countName}++]={item1};");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine($"                for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
                builder.AppendLine($"                    var __item{this.result}={secondSpanName}[__i{this.result}];");
                if(binaryStage.Func is not null) {
                    builder.AppendLine($"                    {unionKeyType} __k{this.result}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"UnionBy",$"__k{this.result}",$"__item{this.result}"),"                    "));
                    builder.AppendLine($"                    if(__set{this.result}.Add(__k{this.result})) {{");
                } else {
                    builder.AppendLine($"                    if(__set{this.result}.Add(__item{this.result})) {{");
                }
                var item2 = this.AppendBinaryAfterStages(builder,$"__item{this.result}","                        ",binaryStageIndex);
                builder.AppendLine($"                        {rentedBufferName}[{countName}++]={item2};");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                if(terminalKind==PipelineTerminalKind.ToArray) {
                    builder.AppendLine($"                if({countName}==0) {{");
                    builder.AppendLine($"                    {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                    builder.AppendLine("                } else {");
                    builder.AppendLine($"                    {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
                    builder.AppendLine($"                    global::System.Array.Copy({rentedBufferName},{actualVariableName},{countName});");
                    builder.AppendLine("                }");
                } else {
                    builder.AppendLine($"                {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({countName});");
                    builder.AppendLine($"                global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{countName});");
                    builder.AppendLine($"                global::System.MemoryExtensions.AsSpan({rentedBufferName},0,{countName}).CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName}));");
                }
                builder.AppendLine("            } finally {");
                builder.AppendLine($"                global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({rentedBufferName},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
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
                keyType=binaryStage.OutputType;
            }
        }
        var foundName = $"__found{this.result}";

        if(binaryStage.Kind is PipelineStageKind.Intersect or PipelineStageKind.IntersectBy) {
            if(terminalKind is not (PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Sum or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault)) {
                return null;
            }
            var rentedBufferName = $"__rentedBuffer{this.result}";
            var countName = $"__count{this.result}";
            var maxCountName = $"__maxCount{this.result}";
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                builder.AppendLine($"{actualTypeName} {actualVariableName};");
            } else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                var minMaxKeyType = func1?.ReturnTypeName??"object";
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"{minMaxKeyType} __bestKey{this.result}=default!;");
                builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{minMaxKeyType}>.Default;");
            } else if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{actualTypeName}>.Default;");
            } else if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"var __targetIndex{this.result}={terminalValue};");
                builder.AppendLine($"var __elemCount{this.result}=0;");
                if(terminalKind==PipelineTerminalKind.ElementAt) {
                    builder.AppendLine($"if(__targetIndex{this.result}<0) throw new global::System.ArgumentOutOfRangeException(\"index\");");
                }
            } else {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            }
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                builder.AppendLine($"var {foundName}=false;");
            }
            builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
            builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
            builder.AppendLine($"    if({firstSpanName}.Length>0) {{");
            builder.AppendLine($"        using(var {secondSrcName}={secondSrcExpr}) {{");
            builder.AppendLine($"            var {secondSpanName}={secondSpanExpr};");
            builder.AppendLine($"            if({secondSpanName}.Length>0) {{");
            if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                builder.AppendLine($"                var {maxCountName}=global::System.Math.Min({firstSpanName}.Length,{secondSpanName}.Length);");
                builder.AppendLine($"                var {rentedBufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({maxCountName});");
                builder.AppendLine($"                var {countName}=0;");
                builder.AppendLine("                try {");
            }
            var setIndent = terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList ? "                    " : "                ";
            string capExpr;
            if(binaryStage.Func is not null){
                capExpr=$"global::System.Math.Min({secondSpanName}.Length,32)";
            }else{
                capExpr=$"{secondSpanName}.Length";
            }
            var setArgs=RenderHashSetConstructorArgs(capExpr,binaryStage.Value);
            builder.AppendLine($"{setIndent}var __set{this.result}=new global::System.Collections.Generic.HashSet<{keyType}>({setArgs});");
        this.AppendBinaryAfterInit(builder,setIndent,binaryStageIndex);
            builder.AppendLine($"{setIndent}for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
            // IntersectBy の第2引数は要素ではなく鍵そのものなので、鍵選択関数は通さずそのまま集合へ足す。
            builder.AppendLine($"{setIndent}    __set{this.result}.Add({secondSpanName}[__i{this.result}]);");
            builder.AppendLine($"{setIndent}}}");
            builder.AppendLine($"{setIndent}for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
            builder.AppendLine($"{setIndent}    var __item{this.result}={firstSpanName}[__i{this.result}];");
            var filteredItem = this.AppendBinaryBeforeStages(builder,$"__item{this.result}",$"{setIndent}    ",binaryStageIndex);
            if(binaryStage.Func is not null) {
                builder.AppendLine($"{setIndent}    {keyType} __k{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"IntersectBy",$"__k{this.result}",filteredItem),$"{setIndent}    "));
                builder.AppendLine($"{setIndent}    if(__set{this.result}.Remove(__k{this.result})) {{");
            } else {
                builder.AppendLine($"{setIndent}    if(__set{this.result}.Remove({filteredItem})) {{");
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        {actualVariableName}=true;");
                builder.AppendLine($"{setIndent}        break;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        {actualVariableName}++;");
            } else if(terminalKind==PipelineTerminalKind.Sum) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        {actualVariableName}+={hitItem};");
            } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        {rentedBufferName}[{countName}++]={hitItem};");
            } else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                var isMax = terminalKind==PipelineTerminalKind.MaxBy;
                string compOp;
                if(isMax) {
                    compOp=">0";
                } else {
                    compOp="<0";
                }
                var minMaxKeyType = func1?.ReturnTypeName??"object";
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        {minMaxKeyType} __key{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"keySelector",$"__key{this.result}",hitItem),$"{setIndent}        "));
                builder.AppendLine($"{setIndent}        if(!{foundName}) {{");
                builder.AppendLine($"{setIndent}            {actualVariableName}={hitItem};");
                builder.AppendLine($"{setIndent}            __bestKey{this.result}=__key{this.result};");
                builder.AppendLine($"{setIndent}            {foundName}=true;");
                builder.AppendLine($"{setIndent}        }} else if(__comparer{this.result}.Compare(__key{this.result},__bestKey{this.result}){compOp}) {{");
                builder.AppendLine($"{setIndent}            {actualVariableName}={hitItem};");
                builder.AppendLine($"{setIndent}            __bestKey{this.result}=__key{this.result};");
                builder.AppendLine($"{setIndent}        }}");
            } else if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
                var isMax = terminalKind==PipelineTerminalKind.Max;
                string compOp;
                if(isMax) {
                    compOp=">0";
                } else {
                    compOp="<0";
                }
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        if(!{foundName}) {{");
                builder.AppendLine($"{setIndent}            {actualVariableName}={hitItem};");
                builder.AppendLine($"{setIndent}            {foundName}=true;");
                builder.AppendLine($"{setIndent}        }} else if(__comparer{this.result}.Compare({hitItem},{actualVariableName}){compOp}) {{");
                builder.AppendLine($"{setIndent}            {actualVariableName}={hitItem};");
                builder.AppendLine($"{setIndent}        }}");
            } else if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        if(__elemCount{this.result}++==__targetIndex{this.result}) {{");
                builder.AppendLine($"{setIndent}            {actualVariableName}={hitItem};");
                builder.AppendLine($"{setIndent}            {foundName}=true;");
                builder.AppendLine($"{setIndent}            break;");
                builder.AppendLine($"{setIndent}        }}");
            } else {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{setIndent}        ",binaryStageIndex);
                builder.AppendLine($"{setIndent}        {actualVariableName}={hitItem};");
                builder.AppendLine($"{setIndent}        {foundName}=true;");
                builder.AppendLine($"{setIndent}        break;");
            }
            builder.AppendLine($"{setIndent}    }}");
            builder.AppendLine($"{setIndent}}}");
            if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                if(terminalKind==PipelineTerminalKind.ToArray) {
                    builder.AppendLine($"                    if({countName}==0) {{");
                    builder.AppendLine($"                        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                    builder.AppendLine("                    } else {");
                    builder.AppendLine($"                        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
                    builder.AppendLine($"                        global::System.Array.Copy({rentedBufferName},{actualVariableName},{countName});");
                    builder.AppendLine("                    }");
                } else {
                    builder.AppendLine($"                    {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({countName});");
                    builder.AppendLine($"                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{countName});");
                    builder.AppendLine($"                    global::System.MemoryExtensions.AsSpan({rentedBufferName},0,{countName}).CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName}));");
                }
                builder.AppendLine("                } finally {");
                builder.AppendLine($"                    global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({rentedBufferName},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
                builder.AppendLine("                }");
            }
            builder.AppendLine("            }");
            if(terminalKind is PipelineTerminalKind.ToArray) {
                builder.AppendLine("            else {");
                builder.AppendLine($"                {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                builder.AppendLine("            }");
            } else if(terminalKind is PipelineTerminalKind.ToList) {
                builder.AppendLine("            else {");
                builder.AppendLine($"                {actualVariableName}=new();");
                builder.AppendLine("            }");
            }
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            if(terminalKind is PipelineTerminalKind.ToArray) {
                builder.AppendLine("    else {");
                builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                builder.AppendLine("    }");
            } else if(terminalKind is PipelineTerminalKind.ToList) {
                builder.AppendLine("    else {");
                builder.AppendLine($"        {actualVariableName}=new();");
                builder.AppendLine("    }");
            }
            builder.AppendLine("}");
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                builder.AppendLine($"if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            } else if(terminalKind==PipelineTerminalKind.ElementAt) {
                builder.AppendLine($"if(!{foundName}) throw new global::System.ArgumentOutOfRangeException(\"index\");");
            } else if(terminalKind==PipelineTerminalKind.ElementAt) {
                builder.AppendLine($"if(!{foundName}) throw new global::System.ArgumentOutOfRangeException(\"index\");");
            }
            return builder.ToString().TrimEnd();
        }

        if(binaryStage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
            if(terminalKind is not (PipelineTerminalKind.Any or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Sum or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault)) {
                return null;
            }
            var rentedBufferName = $"__rentedBuffer{this.result}";
            var countName = $"__count{this.result}";
            if(terminalKind==PipelineTerminalKind.Any) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
            } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                builder.AppendLine($"{actualTypeName} {actualVariableName};");
            } else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                var minMaxKeyType = func1?.ReturnTypeName??"object";
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"{minMaxKeyType} __bestKey{this.result}=default!;");
                builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{minMaxKeyType}>.Default;");
            } else if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{actualTypeName}>.Default;");
            } else if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
                builder.AppendLine($"var __targetIndex{this.result}={terminalValue};");
                builder.AppendLine($"var __elemCount{this.result}=0;");
                if(terminalKind==PipelineTerminalKind.ElementAt) {
                    builder.AppendLine($"if(__targetIndex{this.result}<0) throw new global::System.ArgumentOutOfRangeException(\"index\");");
                }
            } else {
                builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            }
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                builder.AppendLine($"var {foundName}=false;");
            }
            builder.AppendLine($"using(var {firstSrcName}={firstSrcExpr}) {{");
            builder.AppendLine($"    var {firstSpanName}={firstSpanExpr};");
        this.AppendBinaryBeforeSlices(builder,firstSpanName,"    ",binaryStageIndex);
        this.AppendBinaryBeforeInit(builder,"    ",binaryStageIndex);
            builder.AppendLine($"    if({firstSpanName}.Length>0) {{");
            if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                builder.AppendLine($"        var {rentedBufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({firstSpanName}.Length);");
                builder.AppendLine($"        var {countName}=0;");
                builder.AppendLine("        try {");
            }
            var exceptIndent = terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList ? "            " : "        ";
            builder.AppendLine($"{exceptIndent}using(var {secondSrcName}={secondSrcExpr}) {{");
            builder.AppendLine($"{exceptIndent}    var {secondSpanName}={secondSpanExpr};");
            string capExpr;
            if(binaryStage.Func is not null){
                capExpr=$"global::System.Math.Min({secondSpanName}.Length,32)";
            }else{
                capExpr=$"{secondSpanName}.Length";
            }
            var exceptSetArgs=RenderHashSetConstructorArgs(capExpr,binaryStage.Value);
            builder.AppendLine($"{exceptIndent}    var __set{this.result}=new global::System.Collections.Generic.HashSet<{keyType}>({exceptSetArgs});");
        this.AppendBinaryAfterInit(builder,$"{exceptIndent}    ",binaryStageIndex);
            builder.AppendLine($"{exceptIndent}    for(var __i{this.result}=0;__i{this.result}<{secondSpanName}.Length;__i{this.result}++) {{");
            // ExceptBy の第2引数は要素ではなく鍵そのものなので、鍵選択関数は通さずそのまま集合へ足す。
            builder.AppendLine($"{exceptIndent}        __set{this.result}.Add({secondSpanName}[__i{this.result}]);");
            builder.AppendLine($"{exceptIndent}    }}");
            builder.AppendLine($"{exceptIndent}    for(var __i{this.result}=0;__i{this.result}<{firstSpanName}.Length;__i{this.result}++) {{");
            builder.AppendLine($"{exceptIndent}        var __item{this.result}={firstSpanName}[__i{this.result}];");
            var filteredItem = this.AppendBinaryBeforeStages(builder,$"__item{this.result}",$"{exceptIndent}        ",binaryStageIndex);
            if(binaryStage.Func is not null) {
                builder.AppendLine($"{exceptIndent}        {keyType} __k{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(binaryStage.Func,"ExceptBy",$"__k{this.result}",filteredItem),$"{exceptIndent}        "));
                builder.AppendLine($"{exceptIndent}        if(__set{this.result}.Add(__k{this.result})) {{");
            } else {
                builder.AppendLine($"{exceptIndent}        if(__set{this.result}.Add({filteredItem})) {{");
            }
            if(terminalKind==PipelineTerminalKind.Any) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            {actualVariableName}=true;");
                builder.AppendLine($"{exceptIndent}            break;");
            } else if(terminalKind==PipelineTerminalKind.Contains) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                var comparerExpr = comparerValue is null ? null : $"({comparerValue})";
                if(comparerExpr is not null) {
                    builder.AppendLine($"{exceptIndent}            if({comparerExpr}.Equals({hitItem}, {terminalValue})) {{");
                } else {
                    builder.AppendLine($"{exceptIndent}            if(global::System.Collections.Generic.EqualityComparer<{pipeline.ElementType}>.Default.Equals({hitItem}, {terminalValue})) {{");
                }
                builder.AppendLine($"{exceptIndent}                {actualVariableName}=true;");
                builder.AppendLine($"{exceptIndent}                break;");
                builder.AppendLine($"{exceptIndent}            }}");
            } else if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            {actualVariableName}++;");
            } else if(terminalKind==PipelineTerminalKind.Sum) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            {actualVariableName}+={hitItem};");
            } else if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            {rentedBufferName}[{countName}++]={hitItem};");
            } else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                var isMax = terminalKind==PipelineTerminalKind.MaxBy;
                string compOp;
                if(isMax) {
                    compOp=">0";
                } else {
                    compOp="<0";
                }
                var minMaxKeyType = func1?.ReturnTypeName??"object";
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            {minMaxKeyType} __key{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"keySelector",$"__key{this.result}",hitItem),$"{exceptIndent}            "));
                builder.AppendLine($"{exceptIndent}            if(!{foundName}) {{");
                builder.AppendLine($"{exceptIndent}                {actualVariableName}={hitItem};");
                builder.AppendLine($"{exceptIndent}                __bestKey{this.result}=__key{this.result};");
                builder.AppendLine($"{exceptIndent}                {foundName}=true;");
                builder.AppendLine($"{exceptIndent}            }} else if(__comparer{this.result}.Compare(__key{this.result},__bestKey{this.result}){compOp}) {{");
                builder.AppendLine($"{exceptIndent}                {actualVariableName}={hitItem};");
                builder.AppendLine($"{exceptIndent}                __bestKey{this.result}=__key{this.result};");
                builder.AppendLine($"{exceptIndent}            }}");
            } else if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
                var isMax = terminalKind==PipelineTerminalKind.Max;
                string compOp;
                if(isMax) {
                    compOp=">0";
                } else {
                    compOp="<0";
                }
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            if(!{foundName}) {{");
                builder.AppendLine($"{exceptIndent}                {actualVariableName}={hitItem};");
                builder.AppendLine($"{exceptIndent}                {foundName}=true;");
                builder.AppendLine($"{exceptIndent}            }} else if(__comparer{this.result}.Compare({hitItem},{actualVariableName}){compOp}) {{");
                builder.AppendLine($"{exceptIndent}                {actualVariableName}={hitItem};");
                builder.AppendLine($"{exceptIndent}            }}");
            } else if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            if(__elemCount{this.result}++==__targetIndex{this.result}) {{");
                builder.AppendLine($"{exceptIndent}                {actualVariableName}={hitItem};");
                builder.AppendLine($"{exceptIndent}                {foundName}=true;");
                builder.AppendLine($"{exceptIndent}                break;");
                builder.AppendLine($"{exceptIndent}            }}");
            } else {
                var hitItem = this.AppendBinaryAfterStages(builder,filteredItem,$"{exceptIndent}            ",binaryStageIndex);
                builder.AppendLine($"{exceptIndent}            {actualVariableName}={hitItem};");
                builder.AppendLine($"{exceptIndent}            {foundName}=true;");
                builder.AppendLine($"{exceptIndent}            break;");
            }
            builder.AppendLine($"{exceptIndent}        }}");
            builder.AppendLine($"{exceptIndent}    }}");
            builder.AppendLine($"{exceptIndent}}}");
            if(terminalKind is PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList) {
                if(terminalKind==PipelineTerminalKind.ToArray) {
                    builder.AppendLine($"            if({countName}==0) {{");
                    builder.AppendLine($"                {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                    builder.AppendLine("            } else {");
                    builder.AppendLine($"                {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({countName});");
                    builder.AppendLine($"                global::System.Array.Copy({rentedBufferName},{actualVariableName},{countName});");
                    builder.AppendLine("            }");
                } else {
                    builder.AppendLine($"            {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({countName});");
                    builder.AppendLine($"            global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{countName});");
                    builder.AppendLine($"            global::System.MemoryExtensions.AsSpan({rentedBufferName},0,{countName}).CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName}));");
                }
                builder.AppendLine("        } finally {");
                builder.AppendLine($"            global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({rentedBufferName},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
                builder.AppendLine("        }");
            }
            builder.AppendLine("    }");
            if(terminalKind is PipelineTerminalKind.ToArray) {
                builder.AppendLine("    else {");
                builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
                builder.AppendLine("    }");
            } else if(terminalKind is PipelineTerminalKind.ToList) {
                builder.AppendLine("    else {");
                builder.AppendLine($"        {actualVariableName}=new();");
                builder.AppendLine("    }");
            }
            builder.AppendLine("}");
            if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
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

    private string AppendDirectSpanPipelineStages(StringBuilder builder,string currentName,string indent,int startIndex=0){
        for(var index = startIndex;index<pipeline.Stages.Count;index++){
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
                case PipelineStageKind.Distinct: {
                    if(skipDistinct) {
                        break;
                    }
                    var keyTypeDistinct = stage.KeyType??stage.OutputType;
                    string ctorArg;
                    if(string.IsNullOrEmpty(stage.Value)) {
                        ctorArg=$"{spanName}.Length";
                    } else {
                        ctorArg=$"{spanName}.Length, {stage.Value}";
                    }
                    builder.AppendLine($"{indent}var __pipelineSet{this.result}_{index+1}=new global::System.Collections.Generic.HashSet<{keyTypeDistinct}>({ctorArg});");
                    break;
                }
                case PipelineStageKind.DistinctBy: {
                    if(skipDistinct) {
                        break;
                    }
                    var keyTypeDistinctBy = stage.KeyType??stage.Func?.ReturnTypeName??"object";
                    var capExpr = $"global::System.Math.Min({spanName}.Length,32)";
                    string ctorArg;
                    if(string.IsNullOrEmpty(stage.Value)) {
                        ctorArg=capExpr;
                    } else {
                        ctorArg=$"{capExpr},{stage.Value}";
                    }
                    builder.AppendLine($"{indent}var __pipelineSet{this.result}_{index+1}=new global::System.Collections.Generic.HashSet<{keyTypeDistinctBy}>({ctorArg});");
                    break;
                }
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
                case PipelineStageKind.Reverse:
                    break;
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
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
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

    private string? RenderDirectSpanPipelineAverage(string actualVariableName,string actualTypeName) {
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var countName = $"__count{this.result}";
        var sumName = $"__sum{this.result}";
        string accType;
        string zeroLiteral;
        if(actualTypeName=="decimal") {
            accType="decimal";
            zeroLiteral="0m";
        } else {
            accType="double";
            zeroLiteral="0.0";
        }
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        builder.AppendLine($"    long {countName}=0;");
        builder.AppendLine($"    {accType} {sumName}={zeroLiteral};");
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {countName}++;");
        builder.AppendLine($"        {sumName}+={currentName};");
        builder.AppendLine("    }");
        builder.AppendLine($"    if({countName}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        builder.AppendLine($"    {actualVariableName}=({actualTypeName})({sumName}/{countName});");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineMinMax(string actualVariableName,string actualTypeName,bool isMax) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryMinMax) {
            return binaryMinMax;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var hasValueName = $"__hasValue{this.result}";
        var valName = $"__val{this.result}";
        var compOp = "<0";
        if(isMax) {
            compOp=">0";
        }
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        builder.AppendLine($"    var {hasValueName}=false;");
        builder.AppendLine($"    {actualTypeName} {valName}=default!;");
        builder.AppendLine($"    var __comparer{this.result}=global::System.Collections.Generic.Comparer<{actualTypeName}>.Default;");
        for(var __pIdx=0;__pIdx<prefixPrepends.Count;__pIdx++){
            var __prep=prefixPrepends[__pIdx];
            var __prepItemName=$"__prepItem{this.result}_{__pIdx}";
            builder.AppendLine($"    var {__prepItemName}={__prep.Value};");
            builder.AppendLine($"    if((object?){__prepItemName} is not null) {{\r\n        if(!{hasValueName}) {{\r\n            {valName}={__prepItemName};\r\n            {hasValueName}=true;\r\n        }} else if(__comparer{this.result}.Compare({__prepItemName},{valName}){compOp}) {{\r\n            {valName}={__prepItemName};\r\n        }}\r\n    }}");
        }
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        if((object?){currentName} is null) continue;");
        builder.AppendLine($"        if(!{hasValueName}) {{");
        builder.AppendLine($"            {valName}={currentName};");
        builder.AppendLine($"            {hasValueName}=true;");
        builder.AppendLine($"        }} else if(__comparer{this.result}.Compare({currentName},{valName}){compOp}) {{");
        builder.AppendLine($"            {valName}={currentName};");
        builder.AppendLine($"        }}");
        builder.AppendLine("    }");
        for(var __aIdx=0;__aIdx<prefixAppends.Count;__aIdx++){
            var __app=prefixAppends[__aIdx];
            var __appItemName=$"__appItem{this.result}_{__aIdx}";
            builder.AppendLine($"    var {__appItemName}={__app.Value};");
            builder.AppendLine($"    if((object?){__appItemName} is not null) {{\r\n        if(!{hasValueName}) {{\r\n            {valName}={__appItemName};\r\n            {hasValueName}=true;\r\n        }} else if(__comparer{this.result}.Compare({__appItemName},{valName}){compOp}) {{\r\n            {valName}={__appItemName};\r\n        }}\r\n    }}");
        }
        builder.AppendLine($"    if(!{hasValueName}) {{");
        builder.AppendLine($"        if((object?)default({actualTypeName}) is not null) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        builder.AppendLine($"    }}");
        builder.AppendLine($"    {actualVariableName}={valName};");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineElementAt(string actualVariableName,string actualTypeName,bool orDefault) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryElementAt) {
            return binaryElementAt;
        }
        if(pipeline.Stages.Any(static s => s.Kind is PipelineStageKind.Prepend or PipelineStageKind.Append)) {
            return null;
        }
        var seenFilter = false;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            var stage = pipeline.Stages[i];
            if(this.IsDirectSpanFilterStage(stage)) {
                seenFilter=true;
            } else if(seenFilter&&stage.Kind==PipelineStageKind.Reverse) {
                return null;
            } else {
            }
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var isReversed = this.IsPrefixReversed();
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var countName = $"__count{this.result}";
        var foundVar = $"__found{this.result}";
        var targetIndexVar = $"__targetIndex{this.result}";
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        builder.AppendLine($"var {targetIndexVar}={terminalValue};");
        builder.AppendLine($"if({targetIndexVar}>=0) {{");
        builder.AppendLine($"    using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"        var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"        ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"        ",sliceIndices);
        builder.AppendLine($"        var {countName}=0;");
        builder.AppendLine($"        var {foundVar}=false;");
        if(isReversed) {
            builder.AppendLine($"        for(var {indexName}={spanName}.Length-1;{indexName}>=0;{indexName}--) {{");
        } else {
            builder.AppendLine($"        for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        }
        builder.AppendLine($"            var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"            ",sliceIndices);
        builder.AppendLine($"            if({countName}=={targetIndexVar}) {{");
        builder.AppendLine($"                {actualVariableName}={currentName};");
        builder.AppendLine($"                {foundVar}=true;");
        builder.AppendLine($"                break;");
        builder.AppendLine($"            }}");
        builder.AppendLine($"            {countName}++;");
        builder.AppendLine($"        }}");
        if(!orDefault) {
            builder.AppendLine($"        if(!{foundVar}) throw new global::System.ArgumentOutOfRangeException(\"index\");");
        }
        builder.AppendLine($"    }}");
        builder.AppendLine($"}} else {{");
        if(!orDefault) {
            builder.AppendLine($"    throw new global::System.ArgumentOutOfRangeException(\"index\");");
        } else {
            builder.AppendLine($"    {actualVariableName}=default!;");
        }
        builder.AppendLine($"}}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineMinMaxBy(string actualVariableName,string actualTypeName,bool isMax) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryMinMaxBy) {
            return binaryMinMaxBy;
        }
        if(func1 is null) {
            return null;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var keyType = func1.ReturnTypeName;
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var prefixPrepends = this.GetPrefixPrependStages();
        var prefixAppends = this.GetPrefixAppendStages();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var hasValueName = $"__hasValue{this.result}";
        var bestItemName = $"__bestItem{this.result}";
        var bestKeyName = $"__bestKey{this.result}";
        var currentKeyName = $"__currentKey{this.result}";
        var compOp = "<0";
        if(isMax) {
            compOp=">0";
        }
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        builder.AppendLine($"    var {hasValueName}=false;");
        builder.AppendLine($"    {actualTypeName} {bestItemName}=default!;");
        builder.AppendLine($"    {keyType} {bestKeyName}=default!;");
        builder.AppendLine($"    var __comparer{this.result}=global::System.Collections.Generic.Comparer<{keyType}>.Default;");
        for(var __pIdx=0;__pIdx<prefixPrepends.Count;__pIdx++){
            var __prep=prefixPrepends[__pIdx];
            var __prepItemName=$"__prepItem{this.result}_{__pIdx}";
            var __prepKeyName=$"__prepKey{this.result}_{__pIdx}";
            builder.AppendLine($"    var {__prepItemName}={__prep.Value};");
            builder.AppendLine($"    {keyType} {__prepKeyName}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(func1,"keySelector",__prepKeyName,__prepItemName),"    "));
            builder.AppendLine($"    if(!{hasValueName}) {{\r\n        {bestItemName}={__prepItemName};\r\n        {bestKeyName}={__prepKeyName};\r\n        {hasValueName}=true;\r\n    }} else if(__comparer{this.result}.Compare({__prepKeyName},{bestKeyName}){compOp}) {{\r\n        {bestItemName}={__prepItemName};\r\n        {bestKeyName}={__prepKeyName};\r\n    }}");
        }
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        builder.AppendLine($"        {keyType} {currentKeyName}=default!;");
        builder.AppendLine(IndentLines(this.FuncAssignStatic(func1,"keySelector",currentKeyName,currentName),"        "));
        builder.AppendLine($"        if(!{hasValueName}) {{");
        builder.AppendLine($"            {bestItemName}={currentName};");
        builder.AppendLine($"            {bestKeyName}={currentKeyName};");
        builder.AppendLine($"            {hasValueName}=true;");
        builder.AppendLine($"        }} else if(__comparer{this.result}.Compare({currentKeyName},{bestKeyName}){compOp}) {{");
        builder.AppendLine($"            {bestItemName}={currentName};");
        builder.AppendLine($"            {bestKeyName}={currentKeyName};");
        builder.AppendLine($"        }}");
        builder.AppendLine("    }");
        for(var __aIdx=0;__aIdx<prefixAppends.Count;__aIdx++){
            var __app=prefixAppends[__aIdx];
            var __appItemName=$"__appItem{this.result}_{__aIdx}";
            var __appKeyName=$"__appKey{this.result}_{__aIdx}";
            builder.AppendLine($"    var {__appItemName}={__app.Value};");
            builder.AppendLine($"    {keyType} {__appKeyName}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(func1,"keySelector",__appKeyName,__appItemName),"    "));
            builder.AppendLine($"    if(!{hasValueName}) {{\r\n        {bestItemName}={__appItemName};\r\n        {bestKeyName}={__appKeyName};\r\n        {hasValueName}=true;\r\n    }} else if(__comparer{this.result}.Compare({__appKeyName},{bestKeyName}){compOp}) {{\r\n        {bestItemName}={__appItemName};\r\n        {bestKeyName}={__appKeyName};\r\n    }}");
        }
        builder.AppendLine($"    if(!{hasValueName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        builder.AppendLine($"    {actualVariableName}={bestItemName};");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineAggregate(string actualVariableName,string actualTypeName) {
        if(func1 is null) {
            return null;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        if(this.TryGetDirectSourceSpanSetupForScalar(sourceName,out var sourceExpression,out var directSpanExpression) is null) {
            return null;
        }
        var accType = func1.ReturnTypeName;
        var hasSeed = terminalValue is not null;
        var spanName = $"__pipelineSpan{this.result}";
        var sliceIndices = this.GetPrefixSliceStageIndices();
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var hasValueName = $"__hasValue{this.result}";
        var accName = $"__acc{this.result}";
        var nextAccName = $"__nextAcc{this.result}";
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        this.AppendDirectSpanPipelineSlices(builder,spanName,"    ",sliceIndices);
        this.AppendDirectSpanPipelineScalarInit(builder,spanName,"    ",sliceIndices);
        if(hasSeed) {
            builder.AppendLine($"    {accType} {accName}={terminalValue};");
        } else {
            builder.AppendLine($"    var {hasValueName}=false;");
            builder.AppendLine($"    {accType} {accName}=default!;");
        }
        builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
        builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
        var currentName = this.AppendDirectSpanPipelineScalarStages(builder,sourceItemName,"        ",sliceIndices);
        if(hasSeed) {
            builder.AppendLine($"        {accType} {nextAccName}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(func1,"func",nextAccName,accName,currentName),"        "));
            builder.AppendLine($"        {accName}={nextAccName};");
        } else {
            builder.AppendLine($"        if(!{hasValueName}) {{");
            builder.AppendLine($"            {accName}=({accType})(object?){currentName}!;");
            builder.AppendLine($"            {hasValueName}=true;");
            builder.AppendLine($"        }} else {{");
            builder.AppendLine($"            {accType} {nextAccName}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(func1,"func",nextAccName,accName,currentName),"            "));
            builder.AppendLine($"            {accName}={nextAccName};");
            builder.AppendLine($"        }}");
        }
        builder.AppendLine("    }");
        if(!hasSeed) {
            builder.AppendLine($"    if(!{hasValueName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        }
        if(func2 is not null) {
            builder.AppendLine($"    {actualTypeName} __finalResult{this.result}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(func2,"resultSelector",$"__finalResult{this.result}",accName),"    "));
            builder.AppendLine($"    {actualVariableName}=__finalResult{this.result};");
        } else {
            builder.AppendLine($"    {actualVariableName}={accName};");
        }
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private bool CanUseDirectSpanPipelineDefaultIfEmpty(out PipelineStage defaultIfEmptyStage) {
        defaultIfEmptyStage=null!;
        if(!this.TryGetSingleDefaultIfEmptyStage(out var st)||st is null) {
            return false;
        }
        defaultIfEmptyStage=st;
        foreach(var stage in pipeline.Stages) {
            if(stage.Kind is PipelineStageKind.DefaultIfEmpty or PipelineStageKind.AsEnumerable or PipelineStageKind.Cast) {
                continue;
            }
            if(stage.Kind==PipelineStageKind.Select&&!stage.UsesIndex&&stage.Func is { } func&&CanUseDirectSpanPipelineFunc(func)) {
                continue;
            }
            return false;
        }
        return true;
    }

    private string? RenderDirectSpanPipelineDefaultIfEmptyToList(string actualVariableName,string actualTypeName) {
        if(!this.CanUseDirectSpanPipelineDefaultIfEmpty(out var defaultIfEmptyStage)) {
            return null;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        string sourceExpression;
        try {
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var directSpanExpression = pipeline.Source.GetDirectSpanExpression(sourceName)??string.Empty;
        if(directSpanExpression.Length==0) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var defaultValue = defaultIfEmptyStage.Value??"default!";
        var hasAfterStages = pipeline.Stages.Any(static s => s.Kind==PipelineStageKind.Select);
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    if({spanName}.Length==0) {{");
        if(hasAfterStages) {
            builder.AppendLine($"        {sourceItemName}={defaultValue};");
            var mappedDefault = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"        ");
            builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}> {{ {mappedDefault} }};");
        } else {
            builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}> {{ {defaultValue} }};");
        }
        builder.AppendLine("    } else {");
        builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({spanName}.Length);");
        builder.AppendLine($"        global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{spanName}.Length);");
        builder.AppendLine($"        var __destSpan=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
        if(!hasAfterStages) {
            builder.AppendLine($"        {spanName}.CopyTo(__destSpan);");
        } else {
            builder.AppendLine($"        for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
            builder.AppendLine($"            var {sourceItemName}={spanName}[{indexName}];");
            var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"            ");
            builder.AppendLine($"            __destSpan[{indexName}]={currentName};");
            builder.AppendLine("        }");
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineDefaultIfEmptyToArray(string actualVariableName,string actualTypeName) {
        if(!this.CanUseDirectSpanPipelineDefaultIfEmpty(out var defaultIfEmptyStage)) {
            return null;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        string sourceExpression;
        try {
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var directSpanExpression = pipeline.Source.GetDirectSpanExpression(sourceName)??string.Empty;
        if(directSpanExpression.Length==0) {
            return null;
        }
        var spanName = $"__pipelineSpan{this.result}";
        var indexName = $"__pipelineIndex{this.result}";
        var sourceItemName = $"__pipelineItem{this.result}_0";
        var defaultValue = defaultIfEmptyStage.Value??"default!";
        var hasAfterStages = pipeline.Stages.Any(static s => s.Kind==PipelineStageKind.Select);
        var builder = new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    if({spanName}.Length==0) {{");
        if(hasAfterStages) {
            builder.AppendLine($"        {sourceItemName}={defaultValue};");
            var mappedDefault = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"        ");
            builder.AppendLine($"        {actualVariableName}=new {pipeline.ElementType}[] {{ {mappedDefault} }};");
        } else {
            builder.AppendLine($"        {actualVariableName}=new {pipeline.ElementType}[] {{ {defaultValue} }};");
        }
        builder.AppendLine("    } else {");
        builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({spanName}.Length);");
        if(!hasAfterStages) {
            builder.AppendLine($"        {spanName}.CopyTo(global::System.MemoryExtensions.AsSpan({actualVariableName}));");
        } else {
            builder.AppendLine($"        for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
            builder.AppendLine($"            var {sourceItemName}={spanName}[{indexName}];");
            var currentName = this.AppendDirectSpanPipelineStages(builder,sourceItemName,"            ");
            builder.AppendLine($"            {actualVariableName}[{indexName}]={currentName};");
            builder.AppendLine("        }");
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToList(string actualVariableName,string actualTypeName) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryList) {
            return binaryList;
        }
        if(this.RenderDirectSpanPipelineDefaultIfEmptyToList(actualVariableName,actualTypeName) is { } defaultIfEmptyList) {
            return defaultIfEmptyList;
        }
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

    private bool CanUseDirectSpanPipelineOrder(out int orderStageIndex){
        orderStageIndex=-1;
        if(terminalKind is not (PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)){
            return false;
        }
        var count=0;
        for(var i=0;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending){
                if(stage.UsesIndex){
                    return false;
                }
                if(stage.Func is { } func&&!CanUseDirectSpanPipelineFunc(func)){
                    return false;
                }
                orderStageIndex=i;
                count++;
            }
        }
        if(count!=1){
            orderStageIndex=-1;
            return false;
        }
        for(var i=0;i<orderStageIndex;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is not (PipelineStageKind.AsEnumerable or PipelineStageKind.Cast)){
                orderStageIndex=-1;
                return false;
            }
        }
        for(var i=orderStageIndex+1;i<pipeline.Stages.Count;i++){
            var stage=pipeline.Stages[i];
            if(stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)){
                orderStageIndex=-1;
                return false;
            }
            if(stage.Kind==PipelineStageKind.Select){
                if(stage.UsesIndex||stage.Func is not { } selectFunc||!CanUseDirectSpanPipelineFunc(selectFunc)){
                    orderStageIndex=-1;
                    return false;
                }
            }
        }
        return true;
    }

    private bool CanUseGroupByAggregateWithOrder(out int groupByIndex,out int orderStageIndex,out int takeStageIndex){
        groupByIndex=-1;
        orderStageIndex=-1;
        takeStageIndex=-1;
        if(terminalKind is not (PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList)){
            return false;
        }
        for(var i=0;i<pipeline.Stages.Count;i++){
            if(pipeline.Stages[i].Kind==PipelineStageKind.GroupBy){
                if(groupByIndex>=0){
                    return false;
                }
                groupByIndex=i;
            }
        }
        if(groupByIndex<0){
            return false;
        }
        var stage=pipeline.Stages[groupByIndex];
        if(stage.AggregatePlan is null||stage.Func is null){
            return false;
        }
        for(var i=0;i<groupByIndex;i++){
            var st=pipeline.Stages[i];
            if(st.Kind is not (PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)
                ||st.UsesIndex
                ||(st.Func is not null&&!CanUseDirectSpanPipelineFunc(st.Func))){
                return false;
            }
        }
        for(var i=groupByIndex+1;i<pipeline.Stages.Count;i++){
            var st=pipeline.Stages[i];
            if(st.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending){
                if(orderStageIndex>=0||st.UsesIndex||(st.Func is not null&&!CanUseDirectSpanPipelineFunc(st.Func))){
                    return false;
                }
                orderStageIndex=i;
            }else if(st.Kind==PipelineStageKind.Take){
                if(takeStageIndex>=0||st.UsesIndex){
                    return false;
                }
                takeStageIndex=i;
            }else if(st.Kind is PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable){
                if(st.UsesIndex||(st.Func is not null&&!CanUseDirectSpanPipelineFunc(st.Func))){
                    return false;
                }
            }else{
                return false;
            }
        }
        if(orderStageIndex<0){
            return false;
        }
        if(takeStageIndex>=0&&takeStageIndex<orderStageIndex){
            return false;
        }
        return true;
    }

    private string? RenderDirectSpanPipelineOrderBy(string actualVariableName,string actualTypeName){
        if(!this.CanUseDirectSpanPipelineOrder(out var orderStageIndex)){
            return null;
        }
        var sourceName=$"__pipelineSrc{this.result}";
        string sourceExpression;
        string directSpanExpression;
        try{
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        }catch(NotSupportedException){
            return null;
        }
        directSpanExpression=pipeline.Source.GetDirectSpanExpression(sourceName)??string.Empty;
        if(directSpanExpression.Length==0){
            return null;
        }

        var orderStage=pipeline.Stages[orderStageIndex];
        var isDescending=orderStage.Kind==PipelineStageKind.OrderByDescending;
        var keyType=orderStage.KeyType;
        if(keyType is null){
            if(orderStage.Func?.ReturnTypeName is { } retType){
                keyType=retType;
            }else{
                keyType=pipeline.ElementType;
            }
        }

        var spanName=$"__pipelineSpan{this.result}";
        var nName=$"__pipelineLen{this.result}";
        var indexesName=$"__pipelineIndexes{this.result}";
        var keysName=$"__pipelineKeys{this.result}";
        var iName=$"__pipelineI{this.result}";
        var comparerStructName=$"{this.Struct}_OrderDirectComparer";
        var comparerVarName=$"__orderDirectComp{this.result}";
        string comparerArg;
        if(string.IsNullOrEmpty(orderStage.Value)){
            comparerArg="null";
        }else{
            comparerArg=orderStage.Value!;
        }
        var builder=new StringBuilder();
        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    var {spanName}={directSpanExpression};");
        builder.AppendLine($"    if({spanName}.Length==0) {{");
        if(terminalKind==PipelineTerminalKind.ToArray){
            builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
        }else{
            builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
        }
        builder.AppendLine("    } else {");
        builder.AppendLine($"        var {nName}={spanName}.Length;");
        builder.AppendLine($"        var {indexesName}=global::System.Buffers.ArrayPool<int>.Shared.Rent({nName});");
        builder.AppendLine($"        var {keysName}=global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent({nName});");
        builder.AppendLine("        try {");
        builder.AppendLine($"            for(var {iName}=0;{iName}<{nName};{iName}++) {{");
        builder.AppendLine($"                {indexesName}[{iName}]={iName};");
        builder.AppendLine($"                var __srcItem{this.result}={spanName}[{iName}];");
        if(orderStage.Func is not null){
            builder.AppendLine($"                {keyType} __keyVal{this.result}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(orderStage.Func,"OrderBy",$"__keyVal{this.result}",$"__srcItem{this.result}"),"                "));
            builder.AppendLine($"                {keysName}[{iName}]=__keyVal{this.result};");
        }else{
            builder.AppendLine($"                {keysName}[{iName}]=__srcItem{this.result};");
        }
        builder.AppendLine("            }");
        builder.AppendLine($"            var {comparerVarName}=new {comparerStructName}({keysName},{comparerArg});");
        builder.AppendLine($"            global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes({indexesName},{nName},{comparerVarName});");
        if(terminalKind==PipelineTerminalKind.ToArray){
            builder.AppendLine($"            {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({nName});");
            builder.AppendLine($"            for(var {iName}=0;{iName}<{nName};{iName}++) {{");
            builder.AppendLine($"                var __sortedIdx{this.result}={indexesName}[{iName}];");
            builder.AppendLine($"                var __item{this.result}={spanName}[__sortedIdx{this.result}];");
            var afterName=this.AppendDirectSpanPipelineStages(builder,$"__item{this.result}","                ",orderStageIndex+1);
            builder.AppendLine($"                {actualVariableName}[{iName}]={afterName};");
            builder.AppendLine("            }");
        }else{
            builder.AppendLine($"            {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({nName});");
            builder.AppendLine($"            for(var {iName}=0;{iName}<{nName};{iName}++) {{");
            builder.AppendLine($"                var __sortedIdx{this.result}={indexesName}[{iName}];");
            builder.AppendLine($"                var __item{this.result}={spanName}[__sortedIdx{this.result}];");
            var afterName=this.AppendDirectSpanPipelineStages(builder,$"__item{this.result}","                ",orderStageIndex+1);
            builder.AppendLine($"                {actualVariableName}.Add({afterName});");
            builder.AppendLine("            }");
        }
        builder.AppendLine("        } finally {");
        builder.AppendLine($"            global::System.Buffers.ArrayPool<int>.Shared.Return({indexesName});");
        builder.AppendLine($"            global::System.Buffers.ArrayPool<{keyType}>.Shared.Return({keysName});");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string? RenderDirectSpanPipelineToArray(string actualVariableName,string actualTypeName) {
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryArray) {
            return binaryArray;
        }
        if(this.RenderDirectSpanPipelineDefaultIfEmptyToArray(actualVariableName,actualTypeName) is { } defaultIfEmptyArray) {
            return defaultIfEmptyArray;
        }
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
        var isReversed = this.IsPipelineReversed();
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
        var isReversed = this.IsPipelineReversed();
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
        var isReversed = this.IsPipelineReversed();
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
        if(this.RenderDirectSpanPipelineBinaryScalar(actualVariableName,actualTypeName) is { } binaryContains) {
            return binaryContains;
        }
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
        if(pipeline.Stages.Any(static s => s.Kind is PipelineStageKind.Reverse or PipelineStageKind.Prepend or PipelineStageKind.Append)) {
            return null;
        }
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
            if(st.Kind==PipelineStageKind.Where&&!st.UsesIndex&&st.Func is { } wf&&CanUseDirectSpanPipelineFunc(wf)) {
                afterStages.Add(st);
                continue;
            }
            if(st.Kind is PipelineStageKind.Skip or PipelineStageKind.Take&&st.Value is not null) {
                afterStages.Add(st);
                continue;
            }
            return false;
        }
        return true;
    }

    private string AppendPostGroupByStages(StringBuilder builder,string currentName,string indent,int groupByIndex,string continueStatement = "continue;") {
        for(var index = groupByIndex+1;index<pipeline.Stages.Count;index++) {
            var stage = pipeline.Stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.AsEnumerable:
                    break;
                case PipelineStageKind.Cast: {
                    var nextName = $"__postItem{this.result}_{index}";
                    builder.AppendLine($"{indent}var {nextName}=({stage.OutputType})(object?){currentName};");
                    currentName=nextName;
                    break;
                }
                case PipelineStageKind.Select: {
                    var nextName = $"__postItem{this.result}_{index}";
                    builder.AppendLine($"{indent}{stage.OutputType} {nextName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                    currentName=nextName;
                    break;
                }
                case PipelineStageKind.Where: {
                    var (preCode,condition) = this.FuncInvokeConditionStatic(stage.Func!,nameof(PipelineStageKind.Where),$"__condPostTemp{this.result}_{index}",currentName);
                    if(!string.IsNullOrEmpty(preCode)) {
                        builder.AppendLine(IndentLines(preCode,indent));
                    }
                    builder.AppendLine($"{indent}if(!{WrapConditionExpression(condition)}) {continueStatement}");
                    break;
                }
                default:
                    throw new NotSupportedException($"{stage.Kind} is not supported in post-GroupBy direct execution.");
            }
        }
        return currentName;
    }

    private string? TryRenderGroupByAggregateDirectExecution(string actualVariableName,string actualTypeName) {
        if(terminalKind is not (PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.Sum or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToList or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault)) {
            return null;
        }
        var groupByIndex = -1;
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            if(pipeline.Stages[i].Kind==PipelineStageKind.GroupBy) {
                if(groupByIndex>=0) {
                    return null;
                }
                groupByIndex=i;
            }
        }
        if(groupByIndex<0) {
            return null;
        }
        var stage = pipeline.Stages[groupByIndex];
        if(stage.AggregatePlan is not { } aggPlan) {
            return null;
        }
        if(stage.Func is null) {
            return null;
        }
        for(var i = 0;i<groupByIndex;i++) {
            var st = pipeline.Stages[i];
            if(st.Kind is not (PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)
                ||st.UsesIndex
                ||(st.Func is not null&&!CanUseDirectSpanPipelineFunc(st.Func))) {
                return null;
            }
        }
        var hasOrder = this.CanUseGroupByAggregateWithOrder(out _,out var orderStageIndex,out var takeStageIndex);
        var hasPostFilter = false;
        if(!hasOrder) {
            for(var i = groupByIndex+1;i<pipeline.Stages.Count;i++) {
                var st = pipeline.Stages[i];
                if(st.Kind is not (PipelineStageKind.Where or PipelineStageKind.Select or PipelineStageKind.Cast or PipelineStageKind.AsEnumerable)
                    ||st.UsesIndex
                    ||(st.Func is not null&&!CanUseDirectSpanPipelineFunc(st.Func))) {
                    return null;
                }
                if(st.Kind==PipelineStageKind.Where) {
                    hasPostFilter=true;
                }
            }
        }

        string sourceExpression;
        try {
            sourceExpression=pipeline.Source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }
        var sourceName = $"__pipelineSrc{this.result}";
        var directSpanExpression = pipeline.Source.GetDirectSpanExpression(sourceName);

        var allFuncs = new List<FuncFragment>();
        if(stage.Func is not null) {
            allFuncs.Add(stage.Func);
        }
        if(stage.SecondFunc is not null) {
            allFuncs.Add(stage.SecondFunc);
        }
        // 結果選択子の束縛済みローカル(__hoist)は ProjectedResultExpression が参照するので、これも宣言を出す。
        if(stage.ThirdFunc is not null) {
            allFuncs.Add(stage.ThirdFunc);
        }
        foreach(var item in aggPlan.Items) {
            if(item.InnerFunc is not null) {
                allFuncs.Add(item.InnerFunc);
            }
        }
        for(var i = 0;i<pipeline.Stages.Count;i++) {
            if(i==groupByIndex) {
                continue;
            }
            if(pipeline.Stages[i].Func is { } f) {
                allFuncs.Add(f);
            }
            if(pipeline.Stages[i].SecondFunc is { } f2) {
                allFuncs.Add(f2);
            }
        }
        var hoistedLocals = this.HoistedLocalDeclarations(allFuncs.ToArray());
        var staticDelegates = this.StaticDelegateLocalDeclarations(allFuncs.ToArray());

        string comparerExpr;
        if(string.IsNullOrWhiteSpace(stage.Value)||"null".Equals(stage.Value,StringComparison.Ordinal)) {
            comparerExpr="null";
        } else {
            comparerExpr=stage.Value!;
        }

        var keyTypeName = stage.KeyType??stage.Func!.ReturnTypeName;
        var accTypeName = aggPlan.AccumulatorType;
        var mapName = $"__groupAggMap{this.result}";
        var builder = new StringBuilder();

        if(!string.IsNullOrEmpty(hoistedLocals)) {
            builder.AppendLine(hoistedLocals);
        }
        if(!string.IsNullOrEmpty(staticDelegates)) {
            builder.AppendLine(staticDelegates);
        }

        builder.AppendLine($"{actualTypeName} {actualVariableName};");
        builder.AppendLine($"using(var {sourceName}={sourceExpression}) {{");
        builder.AppendLine($"    int __initCap{this.result};");
        builder.AppendLine($"    if({sourceName}.TryGetNonEnumeratedCount(out var __srcCnt{this.result})) {{");
        builder.AppendLine($"        __initCap{this.result}=global::System.Math.Min(__srcCnt{this.result},1024);");
        builder.AppendLine("    } else {");
        builder.AppendLine($"        __initCap{this.result}=0;");
        builder.AppendLine("    }");
        builder.AppendLine($"    using var {mapName}=new global::Seamless.Linq.DictionarySlim<{keyTypeName},{accTypeName}>(__initCap{this.result},{comparerExpr});");

        string indent;
        var sourceItemName = $"__sourceItem{this.result}";
        if(directSpanExpression is not null) {
            var spanName = $"__pipelineSpan{this.result}";
            var indexName = $"__pipelineIndex{this.result}";
            builder.AppendLine($"    var {spanName}={directSpanExpression};");
            builder.AppendLine($"    for(var {indexName}=0;{indexName}<{spanName}.Length;{indexName}++) {{");
            builder.AppendLine($"        var {sourceItemName}={spanName}[{indexName}];");
            indent="        ";
        } else {
            builder.AppendLine($"    while({sourceName}.TryGetNext(out var {sourceItemName})) {{");
            indent="        ";
        }

        var currentName = sourceItemName;
        for(var index = 0;index<groupByIndex;index++) {
            var st = pipeline.Stages[index];
            if(st.Kind==PipelineStageKind.Where) {
                var (preCode,condition) = this.FuncInvokeConditionStatic(st.Func!,nameof(PipelineStageKind.Where),$"__condPreTemp{this.result}_{index}",currentName);
                if(!string.IsNullOrEmpty(preCode)) {
                    builder.AppendLine(IndentLines(preCode,indent));
                }
                builder.AppendLine($"{indent}if(!{WrapConditionExpression(condition)}) continue;");
            } else if(st.Kind==PipelineStageKind.Select) {
                var nextName = $"__preItem{this.result}_{index+1}";
                builder.AppendLine($"{indent}{st.OutputType} {nextName}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(st.Func!,nameof(PipelineStageKind.Select),nextName,currentName),indent));
                currentName=nextName;
            } else if(st.Kind==PipelineStageKind.Cast) {
                var nextName = $"__preItem{this.result}_{index+1}";
                builder.AppendLine($"{indent}var {nextName}=({st.OutputType})(object?){currentName};");
                currentName=nextName;
            }
        }

        var keyName = $"__k{this.result}";
        builder.AppendLine($"{indent}{keyTypeName} {keyName}=default!;");
        builder.AppendLine(IndentLines(this.FuncAssignStatic(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),indent));

        var groupElementName = $"__groupElem{this.result}";
        builder.AppendLine($"{indent}{stage.InnerElementType} {groupElementName}=default!;");
        if(stage.SecondFunc is { } elemSelector) {
            builder.AppendLine(IndentLines(this.FuncAssignStatic(elemSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),indent));
        } else {
            builder.AppendLine($"{indent}{groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
        }
        builder.AppendLine($"{indent}ref var __acc=ref {mapName}.GetValueRefOrAddDefault({keyName},out var __exists);");
        builder.AppendLine(aggPlan.RenderAccumulatorUpdate("__acc",groupElementName,indent));

        builder.AppendLine("    }");

        if(hasOrder){
            var orderStage=pipeline.Stages[orderStageIndex];
            var orderKeyType=orderStage.KeyType;
            if(orderKeyType is null){
                if(orderStage.Func?.ReturnTypeName is { } retType){
                    orderKeyType=retType;
                }else{
                    orderKeyType=orderStage.InputType??pipeline.ElementType;
                }
            }
            var comparerStructName=$"{this.Struct}_OrderDirectComparer";
            var comparerVarName=$"__orderDirectComp{this.result}";
            string comparerArg;
            if(string.IsNullOrEmpty(orderStage.Value)){
                comparerArg="null";
            }else{
                comparerArg=orderStage.Value!;
            }
            var nName=$"__groupCount{this.result}";
            var indexesName=$"__indexes{this.result}";
            var keysName=$"__sortKeys{this.result}";
            var iName=$"__i{this.result}";
            var outIdxName=$"__outIdx{this.result}";

            builder.AppendLine($"    if({mapName}.Count==0) {{");
            if(terminalKind==PipelineTerminalKind.ToArray){
                builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            }else{
                builder.AppendLine($"        {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
            }
            builder.AppendLine("    } else {");
            builder.AppendLine($"        var {nName}={mapName}.Count;");
            string finalCountName;
            if(takeStageIndex>=0){
                var takeStage=pipeline.Stages[takeStageIndex];
                var takeCountExpr=takeStage.Value!;
                finalCountName=$"__finalCount{this.result}";
                builder.AppendLine($"        var {finalCountName}=global::System.Math.Max(0,global::System.Math.Min({nName},{takeCountExpr}));");
            }else{
                finalCountName=nName;
            }
            builder.AppendLine($"        if({finalCountName}==0) {{");
            if(terminalKind==PipelineTerminalKind.ToArray){
                builder.AppendLine($"            {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            }else{
                builder.AppendLine($"            {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>();");
            }
            builder.AppendLine("        } else {");
            if(terminalKind==PipelineTerminalKind.ToArray){
                builder.AppendLine($"            {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({finalCountName});");
            }else{
                builder.AppendLine($"            {actualVariableName}=new global::System.Collections.Generic.List<{pipeline.ElementType}>({finalCountName});");
                builder.AppendLine($"            global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{finalCountName});");
                builder.AppendLine($"            var __destSpan{this.result}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
            }
            builder.AppendLine($"            var {indexesName}=global::System.Buffers.ArrayPool<int>.Shared.Rent({nName});");
            builder.AppendLine($"            var {keysName}=global::System.Buffers.ArrayPool<{orderKeyType}>.Shared.Rent({nName});");
            builder.AppendLine("            try {");
            builder.AppendLine($"                for(var {iName}=0;{iName}<{nName};{iName}++) {{");
            builder.AppendLine($"                    {indexesName}[{iName}]={iName};");
            builder.AppendLine($"                    {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"                    {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");

            var currentOrderInput="__resItem";
            for(var stIdx=groupByIndex+1;stIdx<orderStageIndex;stIdx++){
                var midStage=pipeline.Stages[stIdx];
                if(midStage.Kind==PipelineStageKind.Select){
                    var nextName=$"__midItem{this.result}_{stIdx}";
                    builder.AppendLine($"                    {midStage.OutputType} {nextName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(midStage.Func!,nameof(PipelineStageKind.Select),nextName,currentOrderInput),"                    "));
                    currentOrderInput=nextName;
                }else if(midStage.Kind==PipelineStageKind.Cast){
                    var nextName=$"__midItem{this.result}_{stIdx}";
                    builder.AppendLine($"                    var {nextName}=({midStage.OutputType})(object?){currentOrderInput};");
                    currentOrderInput=nextName;
                }
            }

            if(orderStage.Func is not null){
                builder.AppendLine($"                    {orderKeyType} __keyVal{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(orderStage.Func,nameof(PipelineStageKind.OrderBy),$"__keyVal{this.result}",currentOrderInput),"                    "));
                builder.AppendLine($"                    {keysName}[{iName}]=__keyVal{this.result};");
            }else{
                builder.AppendLine($"                    {keysName}[{iName}]={currentOrderInput};");
            }
            builder.AppendLine("                }");
            builder.AppendLine($"                var {comparerVarName}=new {comparerStructName}({keysName},{comparerArg});");
            builder.AppendLine($"                global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes({indexesName},{nName},{comparerVarName});");
            builder.AppendLine($"                for(var {outIdxName}=0;{outIdxName}<{finalCountName};{outIdxName}++) {{");
            builder.AppendLine($"                    var __sortedIdx{this.result}={indexesName}[{outIdxName}];");
            builder.AppendLine($"                    {mapName}.GetKeyAndValue(__sortedIdx{this.result},out var __key,out var __acc);");
            builder.AppendLine($"                    {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");

            var currentFinalItem="__resItem";
            for(var stIdx=groupByIndex+1;stIdx<pipeline.Stages.Count;stIdx++){
                if(stIdx==orderStageIndex||stIdx==takeStageIndex){
                    continue;
                }
                var midStage=pipeline.Stages[stIdx];
                if(midStage.Kind==PipelineStageKind.Select){
                    var nextName=$"__finalItem{this.result}_{stIdx}";
                    builder.AppendLine($"                    {midStage.OutputType} {nextName}=default!;");
                    builder.AppendLine(IndentLines(this.FuncAssignStatic(midStage.Func!,nameof(PipelineStageKind.Select),nextName,currentFinalItem),"                    "));
                    currentFinalItem=nextName;
                }else if(midStage.Kind==PipelineStageKind.Cast){
                    var nextName=$"__finalItem{this.result}_{stIdx}";
                    builder.AppendLine($"                    var {nextName}=({midStage.OutputType})(object?){currentFinalItem};");
                    currentFinalItem=nextName;
                }
            }
            if(terminalKind==PipelineTerminalKind.ToArray){
                builder.AppendLine($"                    {actualVariableName}[{outIdxName}]={currentFinalItem};");
            }else{
                builder.AppendLine($"                    __destSpan{this.result}[{outIdxName}]={currentFinalItem};");
            }
            builder.AppendLine("                }");
            builder.AppendLine("            } finally {");
            builder.AppendLine($"                global::System.Buffers.ArrayPool<int>.Shared.Return({indexesName});");
            builder.AppendLine($"                global::System.Buffers.ArrayPool<{orderKeyType}>.Shared.Return({keysName});");
            builder.AppendLine("            }");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }

        if(!hasPostFilter&&(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount)) {
            builder.AppendLine($"    {actualVariableName}=({actualTypeName}){mapName}.Count;");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(!hasPostFilter&&terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"    {actualVariableName}={mapName}.Count>0;");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(!hasPostFilter&&terminalKind==PipelineTerminalKind.ToArray) {
            var iName = $"__i{this.result}";
            builder.AppendLine($"    if({mapName}.Count==0) {{");
            builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            builder.AppendLine("    } else {");
            builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({mapName}.Count);");
            builder.AppendLine($"        for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"            {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"            {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","            ",groupByIndex);
            builder.AppendLine($"            {actualVariableName}[{iName}]={afterName};");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(!hasPostFilter&&terminalKind==PipelineTerminalKind.ToList) {
            var iName = $"__i{this.result}";
            builder.AppendLine($"    {actualVariableName}=new({mapName}.Count);");
            builder.AppendLine($"    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({actualVariableName},{mapName}.Count);");
            builder.AppendLine($"    var __destSpan{this.result}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({actualVariableName});");
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        __destSpan{this.result}[{iName}]={afterName};");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Sum) {
            var iName = $"__i{this.result}";
            builder.AppendLine($"    {actualVariableName}=default!;");
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        {actualVariableName}+={afterName};");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
            var foundName = $"__found{this.result}";
            builder.AppendLine($"    {actualVariableName}=default!;");
            builder.AppendLine($"    var {foundName}=false;");
            var iName = $"__i{this.result}";
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        {actualVariableName}={afterName};");
            builder.AppendLine($"        {foundName}=true;");
            builder.AppendLine("        break;");
            builder.AppendLine("    }");
            if(terminalKind==PipelineTerminalKind.First) {
                builder.AppendLine($"    if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
            var foundName = $"__found{this.result}";
            builder.AppendLine($"    {actualVariableName}=default!;");
            builder.AppendLine($"    var {foundName}=false;");
            var iName = $"__i{this.result}";
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        {actualVariableName}={afterName};");
            builder.AppendLine($"        {foundName}=true;");
            builder.AppendLine("    }");
            if(terminalKind==PipelineTerminalKind.Last) {
                builder.AppendLine($"    if(!{foundName}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            builder.AppendLine($"    {actualVariableName}=0;");
            var iName = $"__i{this.result}";
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        {actualVariableName}++;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"    {actualVariableName}=false;");
            var iName = $"__i{this.result}";
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        {actualVariableName}=true;");
            builder.AppendLine("        break;");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToArray) {
            var poolBuffer = $"__poolBuffer{this.result}";
            var poolCount = $"__poolCount{this.result}";
            var poolNext = $"__poolNext{this.result}";
            var iName = $"__i{this.result}";
            builder.AppendLine($"    var {poolBuffer}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent(16);");
            builder.AppendLine($"    var {poolCount}=0;");
            builder.AppendLine("    try {");
            builder.AppendLine($"        for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"            {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"            {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","            ",groupByIndex);
            builder.AppendLine($"            if({poolCount}=={poolBuffer}.Length) {{");
            builder.AppendLine($"                var {poolNext}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({poolBuffer}.Length*2);");
            builder.AppendLine($"                global::System.Array.Copy({poolBuffer},{poolNext},{poolCount});");
            builder.AppendLine($"                global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({poolBuffer},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
            builder.AppendLine($"                {poolBuffer}={poolNext};");
            builder.AppendLine("            }");
            builder.AppendLine($"            {poolBuffer}[{poolCount}++]={afterName};");
            builder.AppendLine("        }");
            builder.AppendLine($"        if({poolCount}==0) {{");
            builder.AppendLine($"            {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            builder.AppendLine("        } else {");
            builder.AppendLine($"            {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({poolCount});");
            builder.AppendLine($"            global::System.Array.Copy({poolBuffer},{actualVariableName},{poolCount});");
            builder.AppendLine("        }");
            builder.AppendLine("    } finally {");
            builder.AppendLine($"        global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({poolBuffer},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.ToList) {
            var iName = $"__i{this.result}";
            builder.AppendLine($"    {actualVariableName}=new();");
            builder.AppendLine($"    for(var {iName}=0;{iName}<{mapName}.Count;{iName}++) {{");
            builder.AppendLine($"        {mapName}.GetKeyAndValue({iName},out var __key,out var __acc);");
            builder.AppendLine($"        {stage.OutputType} __resItem={aggPlan.ProjectedResultExpressionWithoutReceiver};");
            var afterName = this.AppendPostGroupByStages(builder,"__resItem","        ",groupByIndex);
            builder.AppendLine($"        {actualVariableName}.Add({afterName});");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString().TrimEnd();
        }
        builder.AppendLine("}");
        return null;
    }

    private string? TryRenderConsumeBasedDirectScalar(string actualVariableName,string actualTypeName) {
        if(terminalKind is not (PipelineTerminalKind.Sum or PipelineTerminalKind.Average or PipelineTerminalKind.Min or PipelineTerminalKind.Max or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault or PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy or PipelineTerminalKind.Aggregate or PipelineTerminalKind.Count or PipelineTerminalKind.LongCount or PipelineTerminalKind.Any or PipelineTerminalKind.All or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault or PipelineTerminalKind.Contains or PipelineTerminalKind.ToList or PipelineTerminalKind.ToArray or PipelineTerminalKind.ToHashSet)) {
            return null;
        }
        var placeholder = $"__FLINQ_CONSUME_{this.result}__";
        string? consumeStatement = null;
        List<PipelineStage>? afterStages = null;

        // パス1: SelectMany ステージを持つパイプラインの場合、pipeline.GetConsumeExecutionStatement を使って入れ子ループを展開
        if(pipeline.Stages.Any(static s => s.Kind==PipelineStageKind.SelectMany)) {
            try {
                var fullStatement = pipeline.GetConsumeExecutionStatement(placeholder);
                if(!string.IsNullOrWhiteSpace(fullStatement)
                    &&!fullStatement.Contains($"__ce{pipeline.FragmentIndex}.TryGetNext")
                    &&!fullStatement.Contains("GetRemainingEnumerator")
                    &&fullStatement.Contains($"{placeholder}.Consume(")) {
                    consumeStatement=fullStatement;
                    afterStages=new List<PipelineStage>();
                } else {
                }
            } catch(NotSupportedException) {
            }
        } else {
        }

        if(consumeStatement is null) {
            if(pipeline.Source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase or CountBy or AggregateBy or SetBase or SetByBase or DistinctBase or DistinctByBase)) {
                return null;
            }
            if(!this.CanUseConsumeBasedDirectScalar(out afterStages)) {
                return null;
            }
            try {
                consumeStatement=pipeline.Source.GetConsumeExecutionStatement(placeholder);
            } catch(NotSupportedException) {
                return null;
            }
            if(string.IsNullOrWhiteSpace(consumeStatement)
                ||consumeStatement.Contains($"__ce{pipeline.Source.FragmentIndex}.TryGetNext")
                ||consumeStatement.Contains("GetRemainingEnumerator")
                ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
                return null;
            } else {
            }
        }

        afterStages??=new List<PipelineStage>();
        var builder = new StringBuilder();
        var endLabel = $"__consumeEnd{this.result}";
        var foundVar = $"__consumeFound{this.result}";

        var afterFuncs=afterStages.Where(static st => st.Func is not null).Select(static st => st.Func!).ToArray();
        if(afterFuncs.Length>0){
            var hoisted=this.HoistedLocalDeclarations(afterFuncs);
            if(!string.IsNullOrEmpty(hoisted)){
                builder.AppendLine(hoisted);
            }
            var staticDelegates=this.StaticDelegateLocalDeclarations(afterFuncs);
            if(!string.IsNullOrEmpty(staticDelegates)){
                builder.AppendLine(staticDelegates);
            }
        }

        var listBufferName = $"__consumeList{this.result}";
        var poolBufferName = $"__consumePoolBuffer{this.result}";
        var poolCountName = $"__consumePoolCount{this.result}";
        var poolNextName = $"__consumePoolNext{this.result}";
        for(var i = 0;i<afterStages.Count;i++) {
            var st = afterStages[i];
            if(st.Kind==PipelineStageKind.Skip) {
                builder.AppendLine($"var __stageSkip{this.result}_{i+1}=0;");
            } else if(st.Kind==PipelineStageKind.Take) {
                builder.AppendLine($"var __stageTake{this.result}_{i+1}=0;");
            } else {
            }
        }
        if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=0;");
        } else if(terminalKind==PipelineTerminalKind.Any) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
        } else if(terminalKind==PipelineTerminalKind.Contains) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=false;");
        } else if(terminalKind==PipelineTerminalKind.All) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=true;");
        } else if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var {foundVar}=false;");
        } else if(terminalKind==PipelineTerminalKind.Sum) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
        } else if(terminalKind==PipelineTerminalKind.Average) {
            var accType = actualTypeName=="decimal" ? "decimal" : "double";
            var zeroLit = actualTypeName=="decimal" ? "0m" : "0.0";
            builder.AppendLine($"long __count{this.result}=0;");
            builder.AppendLine($"{accType} __sum{this.result}={zeroLit};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
        } else if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var {foundVar}=false;");
            builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{actualTypeName}>.Default;");
        } else if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var {foundVar}=false;");
            builder.AppendLine($"var __targetIndex{this.result}={terminalValue};");
            builder.AppendLine($"var __count{this.result}=0;");
        } else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
            var keyType = func1?.ReturnTypeName??"object";
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var {foundVar}=false;");
            builder.AppendLine($"{keyType} __bestKey{this.result}=default!;");
            builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{keyType}>.Default;");
        } else if(terminalKind==PipelineTerminalKind.Aggregate) {
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            var accType = func1?.ReturnTypeName??actualTypeName;
            var hasSeed = terminalValue is not null;
            if(hasSeed) {
                builder.AppendLine($"{accType} __acc{this.result}={terminalValue};");
            } else {
                builder.AppendLine($"var {foundVar}=false;");
                builder.AppendLine($"{accType} __acc{this.result}=default!;");
            }
        } else if(terminalKind==PipelineTerminalKind.ToList) {
            builder.AppendLine($"{actualTypeName} {actualVariableName}=new();");
        } else if(terminalKind==PipelineTerminalKind.ToHashSet) {
            var comparerArg = string.IsNullOrEmpty(comparerValue) ? string.Empty : comparerValue;
            builder.AppendLine($"{actualTypeName} {actualVariableName}=new({comparerArg});");
        } else if(terminalKind==PipelineTerminalKind.ToArray) {
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"var {poolBufferName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent(16);");
            builder.AppendLine($"var {poolCountName}=0;");
            builder.AppendLine("try {");
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
                } else if(st.Kind==PipelineStageKind.Where) {
                    var condName = $"__stageCond{this.result}_{i+1}";
                    sb.AppendLine($"{indent}bool {condName}=default!;");
                    sb.AppendLine(IndentLines(this.FuncAssignStatic(st.Func!,nameof(PipelineStageKind.Where),condName,currentItem),indent));
                    sb.AppendLine($"{indent}if(!{condName}) continue;");
                } else if(st.Kind==PipelineStageKind.Skip) {
                    var skipVar = $"__stageSkip{this.result}_{i+1}";
                    sb.AppendLine($"{indent}if({skipVar}<{st.Value}) {{");
                    sb.AppendLine($"{indent}    {skipVar}++;");
                    sb.AppendLine($"{indent}    continue;");
                    sb.AppendLine($"{indent}}}");
                } else if(st.Kind==PipelineStageKind.Take) {
                    var takeVar = $"__stageTake{this.result}_{i+1}";
                    sb.AppendLine($"{indent}if({takeVar}>={st.Value}) {{");
                    sb.AppendLine($"{indent}    goto {endLabel};");
                    sb.AppendLine($"{indent}}}");
                    sb.AppendLine($"{indent}{takeVar}++;");
                }
            }
            if(terminalKind is PipelineTerminalKind.Count or PipelineTerminalKind.LongCount) {
                sb.Append($"{indent}{actualVariableName}++;");
            } else if(terminalKind==PipelineTerminalKind.Sum) {
                sb.Append($"{indent}{actualVariableName}+={currentItem};");
            } else if(terminalKind==PipelineTerminalKind.Average) {
                sb.AppendLine($"{indent}__count{this.result}++;");
                sb.Append($"{indent}__sum{this.result}+={currentItem};");
            } else if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
                var compOp = "<0";
                if(terminalKind==PipelineTerminalKind.Max) {
                    compOp=">0";
                }
                sb.AppendLine($"{indent}if((object?){currentItem} is not null) {{");
                sb.AppendLine($"{indent}    if(!{foundVar}) {{");
                sb.AppendLine($"{indent}        {actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}        {foundVar}=true;");
                sb.AppendLine($"{indent}    }} else if(__comparer{this.result}.Compare({currentItem},{actualVariableName}){compOp}) {{");
                sb.AppendLine($"{indent}        {actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}    }}");
                sb.Append($"{indent}}}");
            } else if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
                sb.AppendLine($"{indent}if(__count{this.result}==__targetIndex{this.result}) {{");
                sb.AppendLine($"{indent}    {actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}    {foundVar}=true;");
                sb.AppendLine($"{indent}    goto {endLabel};");
                sb.AppendLine($"{indent}}}");
                sb.Append($"{indent}__count{this.result}++;");
            } else if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
                var keyType = func1?.ReturnTypeName??"object";
                var keyName = $"__key{this.result}";
                var compOp = "<0";
                if(terminalKind==PipelineTerminalKind.MaxBy) {
                    compOp=">0";
                }
                sb.AppendLine($"{indent}{keyType} {keyName}=default!;");
                sb.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"keySelector",keyName,currentItem),indent));
                sb.AppendLine($"{indent}if(!{foundVar}) {{");
                sb.AppendLine($"{indent}    {actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}    __bestKey{this.result}={keyName};");
                sb.AppendLine($"{indent}    {foundVar}=true;");
                sb.AppendLine($"{indent}}} else if(__comparer{this.result}.Compare({keyName},__bestKey{this.result}){compOp}) {{");
                sb.AppendLine($"{indent}    {actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}    __bestKey{this.result}={keyName};");
                sb.AppendLine($"{indent}}}");
            } else if(terminalKind==PipelineTerminalKind.Aggregate) {
                var accType = func1?.ReturnTypeName??actualTypeName;
                var hasSeed = terminalValue is not null;
                var nextAccName = $"__nextAcc{this.result}";
                if(hasSeed) {
                    sb.AppendLine($"{indent}{accType} {nextAccName}=default!;");
                    sb.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"func",nextAccName,$"__acc{this.result}",currentItem),indent));
                    sb.AppendLine($"{indent}__acc{this.result}={nextAccName};");
                } else {
                    sb.AppendLine($"{indent}if(!{foundVar}) {{");
                    sb.AppendLine($"{indent}    __acc{this.result}=({accType})(object?){currentItem}!;");
                    sb.AppendLine($"{indent}    {foundVar}=true;");
                    sb.AppendLine($"{indent}}} else {{");
                    sb.AppendLine($"{indent}    {accType} {nextAccName}=default!;");
                    sb.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"func",nextAccName,$"__acc{this.result}",currentItem),$"{indent}    "));
                    sb.AppendLine($"{indent}    __acc{this.result}={nextAccName};");
                    sb.AppendLine($"{indent}}}");
                }
            } else if(terminalKind==PipelineTerminalKind.Any) {
                sb.AppendLine($"{indent}{actualVariableName}=true;");
                sb.Append($"{indent}goto {endLabel};");
            } else if(terminalKind==PipelineTerminalKind.Contains) {
                var comparerExpr = comparerValue is null ? null : $"({comparerValue})";
                if(comparerExpr is not null) {
                    sb.AppendLine($"{indent}if({comparerExpr}.Equals({currentItem}, {terminalValue})) {{");
                } else {
                    sb.AppendLine($"{indent}if(global::System.Collections.Generic.EqualityComparer<{pipeline.ElementType}>.Default.Equals({currentItem}, {terminalValue})) {{");
                }
                sb.AppendLine($"{indent}    {actualVariableName}=true;");
                sb.AppendLine($"{indent}    goto {endLabel};");
                sb.Append($"{indent}}}");
            } else if(terminalKind==PipelineTerminalKind.All) {
                var condName = $"__allCond{this.result}";
                sb.AppendLine($"{indent}bool {condName}=default!;");
                sb.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"All",condName,currentItem),indent));
                sb.AppendLine($"{indent}if(!{condName}) {{");
                sb.AppendLine($"{indent}    {actualVariableName}=false;");
                sb.AppendLine($"{indent}    goto {endLabel};");
                sb.Append($"{indent}}}");
            } else if(terminalKind is PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault) {
                sb.AppendLine($"{indent}{actualVariableName}={currentItem};");
                sb.AppendLine($"{indent}{foundVar}=true;");
                sb.Append($"{indent}goto {endLabel};");
            } else if(terminalKind is PipelineTerminalKind.Last or PipelineTerminalKind.LastOrDefault) {
                sb.AppendLine($"{indent}{actualVariableName}={currentItem};");
                sb.Append($"{indent}{foundVar}=true;");
            } else if(terminalKind==PipelineTerminalKind.ToList) {
                sb.Append($"{indent}{actualVariableName}.Add({currentItem});");
            } else if(terminalKind==PipelineTerminalKind.ToHashSet) {
                sb.Append($"{indent}{actualVariableName}.Add({currentItem});");
            } else if(terminalKind==PipelineTerminalKind.ToArray) {
                sb.AppendLine($"{indent}if({poolCountName}=={poolBufferName}.Length) {{");
                sb.AppendLine($"{indent}    var {poolNextName}=global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Rent({poolBufferName}.Length*2);");
                sb.AppendLine($"{indent}    global::System.Array.Copy({poolBufferName},{poolNextName},{poolCountName});");
                sb.AppendLine($"{indent}    global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({poolBufferName},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
                sb.AppendLine($"{indent}    {poolBufferName}={poolNextName};");
                sb.AppendLine($"{indent}}}");
                sb.Append($"{indent}{poolBufferName}[{poolCountName}++]={currentItem};");
            }
            return sb.ToString();
        });

        builder.AppendLine(replacedStatement);

        if(terminalKind==PipelineTerminalKind.ToArray) {
            builder.AppendLine($"    if({poolCountName}==0) {{");
            builder.AppendLine($"        {actualVariableName}=global::System.Array.Empty<{pipeline.ElementType}>();");
            builder.AppendLine("    } else {");
            builder.AppendLine($"        {actualVariableName}=global::System.GC.AllocateUninitializedArray<{pipeline.ElementType}>({poolCountName});");
            builder.AppendLine($"        global::System.Array.Copy({poolBufferName},{actualVariableName},{poolCountName});");
            builder.AppendLine("    }");
            builder.AppendLine("} finally {");
            builder.AppendLine($"    global::System.Buffers.ArrayPool<{pipeline.ElementType}>.Shared.Return({poolBufferName},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{pipeline.ElementType}>());");
            builder.AppendLine("}");
        }

        var hasEarlyExit = terminalKind is PipelineTerminalKind.Any or PipelineTerminalKind.Contains or PipelineTerminalKind.All or PipelineTerminalKind.First or PipelineTerminalKind.FirstOrDefault or PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault;
        var hasTakeStage = afterStages.Any(static st => st.Kind==PipelineStageKind.Take);
        if(hasEarlyExit||hasTakeStage) {
            builder.AppendLine($"{endLabel}:;");
            if(terminalKind==PipelineTerminalKind.First) {
                builder.AppendLine($"if(!{foundVar}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            if(terminalKind==PipelineTerminalKind.ElementAt) {
                builder.AppendLine($"if(!{foundVar}) throw new global::System.ArgumentOutOfRangeException(\"index\");");
            }
        }
        if(terminalKind==PipelineTerminalKind.Last) {
            builder.AppendLine($"if(!{foundVar}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        }
        if(terminalKind==PipelineTerminalKind.Average) {
            builder.AppendLine($"if(__count{this.result}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine($"{actualVariableName}=({actualTypeName})(__sum{this.result}/__count{this.result});");
        }
        if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
            builder.AppendLine($"if(!{foundVar}) {{");
            builder.AppendLine($"    if((object?)default({actualTypeName}) is not null) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine($"}}");
        }
        if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
            builder.AppendLine($"if(!{foundVar}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
        }
        if(terminalKind==PipelineTerminalKind.Aggregate) {
            var hasSeed = terminalValue is not null;
            if(!hasSeed) {
                builder.AppendLine($"if(!{foundVar}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            if(func2 is not null) {
                builder.AppendLine($"{actualTypeName} __finalResult{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(func2,"resultSelector",$"__finalResult{this.result}",$"__acc{this.result}"),""));
                builder.AppendLine($"{actualVariableName}=__finalResult{this.result};");
            } else {
                builder.AppendLine($"{actualVariableName}=__acc{this.result};");
            }
        }

        return builder.ToString().TrimEnd();
    }

    public string GetDirectExecutionStatement(string actualVariableName,string actualTypeName) {
        if(this.TryRenderGroupByAggregateDirectExecution(actualVariableName,actualTypeName) is { } directGroupBy) {
            return directGroupBy;
        }
        var builder = new StringBuilder();
        if(terminalKind==PipelineTerminalKind.ToList) {
            if(this.RenderDirectSpanPipelineOrderBy(actualVariableName,actualTypeName) is { } directSpanOrderByList){
                return directSpanOrderByList;
            }else{
            }
            if(this.RenderDirectSpanPipelineToList(actualVariableName,actualTypeName) is { } directSpanPipeline) {
                return directSpanPipeline;
            }
            if(this.RenderDirectSpanPipelineToListWithFilter(actualVariableName,actualTypeName) is { } directSpanFilteredList) {
                return directSpanFilteredList;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } directConsumeList) {
                return directConsumeList;
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
            if(this.RenderDirectSpanPipelineOrderBy(actualVariableName,actualTypeName) is { } directSpanOrderByArray){
                return directSpanOrderByArray;
            }else{
            }
            if(this.RenderDirectSpanPipelineToArray(actualVariableName,actualTypeName) is { } directSpanPipeline) {
                return directSpanPipeline;
            }
            if(this.RenderDirectSpanPipelineToArrayWithFilter(actualVariableName,actualTypeName) is { } directSpanFilteredArray) {
                return directSpanFilteredArray;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } directConsumeArray) {
                return directConsumeArray;
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
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } directConsumeHashSet) {
                return directConsumeHashSet;
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
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeAll) {
                return consumeAll;
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
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeContains) {
                return consumeContains;
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
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeLast) {
                return consumeLast;
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
        if(terminalKind==PipelineTerminalKind.Average) {
            if(this.RenderDirectSpanPipelineAverage(actualVariableName,actualTypeName) is { } directSpanAverage) {
                return directSpanAverage;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeAverage) {
                return consumeAverage;
            }
            var accType = actualTypeName=="decimal" ? "decimal" : "double";
            var zeroLit = actualTypeName=="decimal" ? "0m" : "0.0";
            builder.AppendLine($"long __count{this.result}=0;");
            builder.AppendLine($"{accType} __sum{this.result}={zeroLit};");
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    __count{this.result}++;");
            builder.AppendLine($"    __sum{this.result}+=__item{this.result};");
            builder.AppendLine("}");
            builder.AppendLine($"if(__count{this.result}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine($"{actualVariableName}=({actualTypeName})(__sum{this.result}/__count{this.result});");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.Min or PipelineTerminalKind.Max) {
            var isMax = terminalKind==PipelineTerminalKind.Max;
            if(this.RenderDirectSpanPipelineMinMax(actualVariableName,actualTypeName,isMax) is { } directSpanMinMax) {
                return directSpanMinMax;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeMinMax) {
                return consumeMinMax;
            }
            var compOp = "<0";
            if(isMax) {
                compOp=">0";
            }
            builder.AppendLine($"var __found{this.result}=false;");
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{actualTypeName}>.Default;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    if((object?)__item{this.result} is not null) {{");
            builder.AppendLine($"        if(!__found{this.result}) {{");
            builder.AppendLine($"            {actualVariableName}=__item{this.result};");
            builder.AppendLine($"            __found{this.result}=true;");
            builder.AppendLine($"        }} else if(__comparer{this.result}.Compare(__item{this.result},{actualVariableName}){compOp}) {{");
            builder.AppendLine($"            {actualVariableName}=__item{this.result};");
            builder.AppendLine($"        }}");
            builder.AppendLine($"    }}");
            builder.AppendLine($"}}");
            builder.AppendLine($"if(!__found{this.result}) {{");
            builder.AppendLine($"    if((object?)default({actualTypeName}) is not null) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            builder.AppendLine($"}}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.ElementAt or PipelineTerminalKind.ElementAtOrDefault) {
            var orDefault = terminalKind==PipelineTerminalKind.ElementAtOrDefault;
            if(this.RenderDirectSpanPipelineElementAt(actualVariableName,actualTypeName,orDefault) is { } directSpanElementAt) {
                return directSpanElementAt;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeElementAt) {
                return consumeElementAt;
            }
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"var __targetIndex{this.result}={terminalValue};");
            builder.AppendLine($"if(__targetIndex{this.result}>=0) {{");
            builder.AppendLine($"    var __found{this.result}=false;");
            builder.AppendLine($"    var __count{this.result}=0;");
            builder.AppendLine($"    using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"    while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"        if(__count{this.result}==__targetIndex{this.result}) {{");
            builder.AppendLine($"            {actualVariableName}=__item{this.result};");
            builder.AppendLine($"            __found{this.result}=true;");
            builder.AppendLine($"            break;");
            builder.AppendLine($"        }}");
            builder.AppendLine($"        __count{this.result}++;");
            builder.AppendLine($"    }}");
            if(!orDefault) {
                builder.AppendLine($"    if(!__found{this.result}) throw new global::System.ArgumentOutOfRangeException(\"index\");");
            }
            builder.AppendLine($"}} else {{");
            if(!orDefault) {
                builder.AppendLine($"    throw new global::System.ArgumentOutOfRangeException(\"index\");");
            } else {
                builder.AppendLine($"    {actualVariableName}=default!;");
            }
            builder.AppendLine($"}}");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind is PipelineTerminalKind.MinBy or PipelineTerminalKind.MaxBy) {
            var isMax = terminalKind==PipelineTerminalKind.MaxBy;
            if(this.RenderDirectSpanPipelineMinMaxBy(actualVariableName,actualTypeName,isMax) is { } directSpanMinMaxBy) {
                return directSpanMinMaxBy;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeMinMaxBy) {
                return consumeMinMaxBy;
            }
            var keyType = func1?.ReturnTypeName??"object";
            var compOp = "<0";
            if(isMax) {
                compOp=">0";
            }
            builder.AppendLine($"var __found{this.result}=false;");
            builder.AppendLine($"{actualTypeName} {actualVariableName}=default!;");
            builder.AppendLine($"{keyType} __bestKey{this.result}=default!;");
            builder.AppendLine($"var __comparer{this.result}=global::System.Collections.Generic.Comparer<{keyType}>.Default;");
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            builder.AppendLine($"    {keyType} __key{this.result}=default!;");
            builder.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"keySelector",$"__key{this.result}",$"__item{this.result}"),"    "));
            builder.AppendLine($"    if(!__found{this.result}) {{");
            builder.AppendLine($"        {actualVariableName}=__item{this.result};");
            builder.AppendLine($"        __bestKey{this.result}=__key{this.result};");
            builder.AppendLine($"        __found{this.result}=true;");
            builder.AppendLine($"    }} else if(__comparer{this.result}.Compare(__key{this.result},__bestKey{this.result}){compOp}) {{");
            builder.AppendLine($"        {actualVariableName}=__item{this.result};");
            builder.AppendLine($"        __bestKey{this.result}=__key{this.result};");
            builder.AppendLine($"    }}");
            builder.AppendLine($"}}");
            builder.AppendLine($"if(!__found{this.result}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            return builder.ToString().TrimEnd();
        }
        if(terminalKind==PipelineTerminalKind.Aggregate) {
            if(this.RenderDirectSpanPipelineAggregate(actualVariableName,actualTypeName) is { } directSpanAggregate) {
                return directSpanAggregate;
            }
            if(this.TryRenderConsumeBasedDirectScalar(actualVariableName,actualTypeName) is { } consumeAggregate) {
                return consumeAggregate;
            }
            var accType = func1?.ReturnTypeName??actualTypeName;
            var hasSeed = terminalValue is not null;
            builder.AppendLine($"{actualTypeName} {actualVariableName};");
            if(hasSeed) {
                builder.AppendLine($"{accType} __acc{this.result}={terminalValue};");
            } else {
                builder.AppendLine($"var __found{this.result}=false;");
                builder.AppendLine($"{accType} __acc{this.result}=default!;");
            }
            builder.AppendLine($"using var {this.EnumeratorName()}={pipeline.GetEnumeratorExpression};");
            builder.AppendLine($"while({this.EnumeratorName()}.TryGetNext(out var __item{this.result})) {{");
            if(hasSeed) {
                builder.AppendLine($"    {accType} __nextAcc{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"func",$"__nextAcc{this.result}",$"__acc{this.result}",$"__item{this.result}"),"    "));
                builder.AppendLine($"    __acc{this.result}=__nextAcc{this.result};");
            } else {
                builder.AppendLine($"    if(!__found{this.result}) {{");
                builder.AppendLine($"        __acc{this.result}=({accType})(object?)__item{this.result}!;");
                builder.AppendLine($"        __found{this.result}=true;");
                builder.AppendLine($"    }} else {{");
                builder.AppendLine($"        {accType} __nextAcc{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(func1!,"func",$"__nextAcc{this.result}",$"__acc{this.result}",$"__item{this.result}"),"        "));
                builder.AppendLine($"        __acc{this.result}=__nextAcc{this.result};");
                builder.AppendLine($"    }}");
            }
            builder.AppendLine($"}}");
            if(!hasSeed) {
                builder.AppendLine($"if(!__found{this.result}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();");
            }
            if(func2 is not null) {
                builder.AppendLine($"{actualTypeName} __finalResult{this.result}=default!;");
                builder.AppendLine(IndentLines(this.FuncAssignStatic(func2,"resultSelector",$"__finalResult{this.result}",$"__acc{this.result}"),""));
                builder.AppendLine($"{actualVariableName}=__finalResult{this.result};");
            } else {
                builder.AppendLine($"{actualVariableName}=__acc{this.result};");
            }
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
