namespace Seamless.FLinq.Linq.Fragments;

// 生成 enumerator struct のメンバーと段の判定。
internal sealed partial class PipelineEnumerable {
    public override string GetEnumeratorPrimaryConstructorParameters {
        get {
            var parameters = new List<string> { $"TEnum {this.e}" };
            foreach(var (second,index) in this.SecondSources.Select(static (second,index) => (second,index))) {
                parameters.Add($"{SecondTypeParameterName(index)} {this.SecondEnumeratorName(index)}");
            }
            foreach(var (stage,index) in this.CountStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"int {this.CountName(index)}");
            }
            foreach(var (stage,index) in this.RangeStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Range {this.RangeName(this.RangeOrdinal(index))}");
            }
            foreach(var (stage,index) in this.ComparerStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Collections.Generic.IEqualityComparer<{ComparerKeyType(stage)}>? {this.ComparerName(index)}");
            }
            foreach(var (stage,index) in this.OrderComparerStages().Select(static (stage,index) => (stage,index))) {
                parameters.Add($"global::System.Collections.Generic.IComparer<{ComparerKeyType(stage)}>? {this.OrderComparerName(index)}");
            }
            parameters.AddRange(this.ClosureParametersWithoutPrefix());
            return string.Join(", ",parameters);
        }
    }
    public override string GetEnumeratorFields {
        get {
            var builder = new StringBuilder();
            var countIndex = 0;
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select&&stage.UsesIndex) {
                    builder.AppendLine($"private int {this.StateName(index)} = -1;");
                } else if(stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(countIndex++);
                    builder.AppendLine($"private int {countName}Value;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.SelectMany) {
                    if(stage.InnerIsArray) {
                        builder.AppendLine($"private {stage.InnerElementType}[] {this.InnerArrayName(index)} = global::System.Array.Empty<{stage.InnerElementType}>();");
                        builder.AppendLine($"private int {this.InnerIndexName(index)} = 0;");
                        builder.AppendLine($"private bool {this.InnerHasValueName(index)} = false;");
                    } else {
                        builder.AppendLine($"private global::System.Collections.Generic.IEnumerator<{stage.InnerElementType}>? {this.InnerEnumeratorName(index)} = null;");
                    }
                    builder.AppendLine($"private {stage.InputType} {this.OuterName(index)} = default!;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {this.StateName(index)} = -1;");
                    }
                } else if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SetName(index)};");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SetName(index)};");
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SecondSetName(index)};");
                    builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SetName(index)};");
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend) {
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine(this.OrderIndexComparerType(index));
                    builder.AppendLine($"private {stage.InputType}[]? {this.OrderValuesName(index)} = null;");
                    builder.AppendLine($"private int[]? {this.OrderIndexesName(index)} = null;");
                    builder.AppendLine($"private int {this.OrderCountName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.GroupBy) {
                    builder.AppendLine(this.GroupingType(index,stage));
                    builder.AppendLine($"private global::System.Collections.Generic.List<{this.GroupValueType(index,stage)}>? {this.GroupItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.Reverse) {
                    builder.AppendLine($"private {stage.OutputType}[]? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.BufferCountName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.DefaultIfEmpty) {
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind==PipelineStageKind.TakeWhile) {
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {this.IndexName(index)} = -1;");
                    }
                } else if(stage.Kind==PipelineStageKind.SkipWhile) {
                    builder.AppendLine($"private bool {this.StateName(index)} = true;");
                    if(stage.UsesIndex) {
                        builder.AppendLine($"private int {this.IndexName(index)} = -1;");
                    }
                } else if(stage.Kind==PipelineStageKind.SkipLast) {
                    // ArrayPool ベースの ValueQueue。フィールド初期化子は置かず、初回 Enqueue の
                    // 直前まで借りない（未列挙のまま捨てられたときに取りこぼさないため）。
                    builder.AppendLine($"private global::Seamless.Linq.ValueQueue<{stage.OutputType}> {this.QueueName(index)};");
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind==PipelineStageKind.TakeLast) {
                    builder.AppendLine($"private {stage.OutputType}[]? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.BufferCountName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.TakeRange) {
                    builder.AppendLine($"private {stage.OutputType}[]? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.BufferCountName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateEndName(index)} = 0;");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetEnumeratorConstructor {
        get {
            var builder = new StringBuilder();
            var countIndex = 0;
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(countIndex++);
                    builder.AppendLine($"this.{countName}Value = global::System.Math.Max(0, {countName});");
                } else if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"this.{this.SecondSetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetTryGetNextBody => this.BuildLoop(this.e,"current=this.Current={0};\nreturn true;","current=default!;\nreturn false;");
    protected override string GetTryGetNonEnumeratedCountBody {
        get {
            if(this.SingleDefaultIfEmpty) {
                return $$"""
                    if(this.{{this.StateName(0)}}){
                        return this.{{this.e}}.TryGetNonEnumeratedCount(out count);
                    }
                    if(this.{{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                        count=__count==0 ? 1 : __count;
                        return true;
                    }
                    count=0;
                    return false;
                    """;
            }
            return this.StagesCountPreserving
                ? $"return this.{this.e}.TryGetNonEnumeratedCount(out count);"
                : base.GetTryGetNonEnumeratedCountBody;
        }
    }
    protected override string GetTryGetSpanBody {
        get {
            if(this.SingleDefaultIfEmpty) {
                return $$"""
                    if(this.{{this.StateName(0)}}){
                        return this.{{this.e}}.TryGetSpan(out span);
                    }
                    if(this.{{this.e}}.TryGetSpan(out span)&&span.Length!=0){
                        return true;
                    }
                    span=default;
                    return false;
                    """;
            }
            return base.GetTryGetSpanBody;
        }
    }
    protected override string GetTryCopyToBody {
        get {
            if(this.SingleDefaultIfEmpty) {
                return $$"""
                    if(this.{{this.StateName(0)}}){
                        return this.{{this.e}}.TryCopyTo(destination,__offset);
                    }
                    if(this.{{this.e}}.TryGetSpan(out var __span)&&__span.Length!=0){
                        var __spanStart=__offset.GetOffset(__span.Length);
                        if(__spanStart<0||__spanStart>__span.Length||destination.Length>__span.Length-__spanStart){
                            return false;
                        }
                        __span.Slice(__spanStart,destination.Length).CopyTo(destination);
                        return true;
                    }
                    if(this.{{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                        if(__count==0){
                            var __defaultStart=__offset.GetOffset(1);
                            if(__defaultStart<0||__defaultStart>1||destination.Length>1-__defaultStart){
                                return false;
                            }
                            if(destination.Length!=0){
                                destination[0]={{stages[0].Value}};
                            }
                            return true;
                        }
                        var __copyStart=__offset.GetOffset(__count);
                        if(__copyStart<0||__copyStart>__count||destination.Length>__count-__copyStart){
                            return false;
                        }
                        return destination.Length==0||this.{{this.e}}.TryCopyTo(destination,__offset);
                    }
                    if(this.{{this.e}}.TryCopyTo(destination,__offset)){
                        return true;
                    }
                    return false;
                    """;
            }
            if(!this.StagesCountPreserving) {
                return base.GetTryCopyToBody;
            }

            var success = """
                if(__skip>0) {{
                    __skip--;
                    continue;
                }}
                destination[__i++] = {0};
                if(__i==destination.Length) {{
                    return true;
                }}
                """;
            var body = this.BuildLoop(this.e,success,"return __i==destination.Length;");
            return $$"""
                if(!this.{{this.e}}.TryGetNonEnumeratedCount(out var __count)){
                    return false;
                }
                var __start = __offset.GetOffset(__count);
                if(__start < 0 || __start > __count || destination.Length > __count - __start){
                    return false;
                }
                if(destination.Length==0){
                    return true;
                }
                var __skip = __start;
                var __i=0;
                {{body}}
                """;
        }
    }
    protected override string GetEnumeratorDisposeBody {
        get {
            var builder = new StringBuilder();
            for(var index = 0;index<stages.Count;index++) {
                if(stages[index].Kind==PipelineStageKind.SelectMany&&!stages[index].InnerIsArray) {
                    builder.AppendLine($"if(this.{this.InnerEnumeratorName(index)} is not null) {{");
                    builder.AppendLine($"    this.{this.InnerEnumeratorName(index)}.Dispose();");
                    builder.AppendLine("}");
                } else if(stages[index].Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy or PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"this.{this.SetName(index)}.Dispose();");
                } else if(stages[index].Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"this.{this.SecondSetName(index)}.Dispose();");
                    builder.AppendLine($"this.{this.SetName(index)}.Dispose();");
                } else if(stages[index].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine($"if(this.{this.OrderValuesName(index)} is not null && this.{this.OrderValuesName(index)}.Length!=0) {{");
                    builder.AppendLine($"    global::System.Buffers.ArrayPool<{stages[index].InputType}>.Shared.Return(this.{this.OrderValuesName(index)},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{stages[index].InputType}>());");
                    builder.AppendLine($"    this.{this.OrderValuesName(index)}=null;");
                    builder.AppendLine("}");
                    builder.AppendLine($"if(this.{this.OrderIndexesName(index)} is not null && this.{this.OrderIndexesName(index)}.Length!=0) {{");
                    builder.AppendLine($"    global::System.Buffers.ArrayPool<int>.Shared.Return(this.{this.OrderIndexesName(index)},clearArray:false);");
                    builder.AppendLine($"    this.{this.OrderIndexesName(index)}=null;");
                    builder.AppendLine("}");
                } else if(stages[index].Kind is PipelineStageKind.Reverse or PipelineStageKind.TakeLast or PipelineStageKind.TakeRange) {
                    builder.AppendLine(PooledBufferSupport.Dispose(stages[index].OutputType,this.ReverseItemsName(index)));
                } else if(stages[index].Kind==PipelineStageKind.SkipLast) {
                    // default(ValueQueue<T>) は items が null で Dispose が no-op なので無条件で呼べる。
                    builder.AppendLine($"this.{this.QueueName(index)}.Dispose();");
                }
            }
            foreach(var index in this.SecondSources.Select(static (_,index) => index)) {
                builder.AppendLine($"{this.SecondEnumeratorName(index)}.Dispose();");
            }
            builder.AppendLine($"{this.e}.Dispose();");
            return builder.ToString().TrimEnd();
        }
    }

    internal string BuildDirectLoop(string enumeratorName,string successStatement,string endStatement) => this.BuildLoop(enumeratorName,successStatement,endStatement);

    private IEnumerable<FuncFragment> Funcs => stages.SelectMany(static stage => new[] { stage.Func,stage.SecondFunc,stage.ThirdFunc }).Where(static func => func is not null).Cast<FuncFragment>();
    private IEnumerable<Enumerable> SecondSources => stages.Select(static stage => stage.SecondSource).Where(static source => source is not null).Cast<Enumerable>();
    private string SecondSourcesStructs {
        get {
            var result = string.Empty;
            foreach(var source in this.SecondSources.Select(static source => source.GetEnumeratorStruct)) {
                result=JoinStaticMethods(result,source);
            }
            return result;
        }
    }

    private IEnumerable<PipelineStage> CountStages() =>
        stages.Where(static stage => stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip||stage.Kind==PipelineStageKind.SkipLast||stage.Kind==PipelineStageKind.TakeLast);

    private IEnumerable<PipelineStage> ComparerStages() =>
        stages.Where(static stage => stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy or PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy or PipelineStageKind.GroupBy);

    private IEnumerable<PipelineStage> OrderComparerStages() =>
        stages.Where(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending);

    private string EqualityComparerExpression(PipelineStage stage) =>
        ComparerExpressionSupport.RenderEqualityComparer(stage.Value!,ComparerKeyType(stage));

    private string OrderComparerExpression(PipelineStage stage) =>
        ComparerExpressionSupport.RenderOrderComparer(stage.Value!,ComparerKeyType(stage));

    private static bool IsBinarySetStage(PipelineStageKind kind) =>
        kind is PipelineStageKind.Union
            or PipelineStageKind.Except
            or PipelineStageKind.Intersect
            or PipelineStageKind.UnionBy
            or PipelineStageKind.ExceptBy
            or PipelineStageKind.IntersectBy;

    private static bool IsPrimaryOrderStage(PipelineStageKind kind) =>
        kind is PipelineStageKind.OrderBy
            or PipelineStageKind.OrderByDescending;
}
