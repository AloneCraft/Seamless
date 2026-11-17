using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

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
            foreach(var (stage,index) in this.ElementStages()){
                parameters.Add($"{stage.OutputType} {this.ElementArgumentName(index)}");
            }
            parameters.AddRange(this.ClosureParametersWithoutPrefix());
            return string.Join(", ",parameters);
        }
    }
    public override string GetEnumeratorFields {
        get {
            var builder = new StringBuilder();
            if(this.SupportsOrderedMaterialization){
                // ValueEnumerable.Enumeratorのコピーだけへ設定し、元の通常列挙は変更しない。
                builder.AppendLine($"private bool {OrderedMaterializationFlag}={this.skipSingletonOrderKeyEvaluation.ToString().ToLowerInvariant()};");
                builder.AppendLine($"private bool {OrderedFirstFlag}={this.useOrderedFirst.ToString().ToLowerInvariant()};");
                builder.AppendLine($"private bool {OrderedFirstCompletedFlag}=false;");
                builder.AppendLine($"public void {OrderedMaterializationMethod}(){{this.{OrderedMaterializationFlag}=true;}}");
                builder.AppendLine($"public void {OrderedConsumerMethod}(bool materialize,bool first){{this.{OrderedMaterializationFlag}=materialize;this.{OrderedFirstFlag}=first;}}");
                if(source is PipelineEnumerable{SupportsOrderedMaterialization:true}){
                    builder.AppendLine($"public TEnum {OrderedSourceProperty}{{get=>this.{this.e};set=>this.{this.e}=value;}}");
                }
            }
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select&&stage.UsesIndex) {
                    builder.AppendLine($"private int {this.StateName(index)} = -1;");
                } else if(stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(this.CountOrdinal(index));
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
                    if(this.UsesInlineIntersectSet(stage)){
                        var setName=this.SecondSetName(index);
                        builder.AppendLine(InlineIntersectSetDefinition(setName));
                        builder.AppendLine($"private {setName}Inline {setName};");
                    }else{
                        builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SecondSetName(index)};");
                    }
                    if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
                        builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SetName(index)};");
                    }
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend) {
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine(this.OrderIndexComparerType(index));
                    if(this.CanUseTopKOrder(index,this.OrderStages(index).ToArray(),out _)) {
                        builder.AppendLine(this.OrderTopKComparerType(index));
                    }
                    builder.AppendLine($"private {stage.InputType}[]? {this.OrderValuesName(index)} = null;");
                    builder.AppendLine($"private int[]? {this.OrderIndexesName(index)} = null;");
                    builder.AppendLine($"private int {this.OrderCountName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.GroupBy) {
                    var hasPostOrderBy = stages.Skip(index+1).Any(static s => s.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending);
                    if(!hasPostOrderBy) {
                        builder.AppendLine($"private bool {this.GroupBuiltName(index)} = false;");
                        if(this.UsesInlineParityCounts(stage)){
                            builder.AppendLine($"private (int Zero,int One,int First,int Cursor) {this.GroupAggMapName(index)};");
                        } else if(stage.AggregatePlan is { } aggPlan) {
                            if(this.TryGetSmallKeyRange(stage, out _)) {
                                builder.AppendLine($"private int[]? {this.GroupDirectKeysName(index)} = null;");
                                builder.AppendLine($"private {aggPlan.AccumulatorType}[]? {this.GroupDirectAccsName(index)} = null;");
                                builder.AppendLine($"private int {this.GroupDirectCountName(index)} = 0;");
                                builder.AppendLine($"private int {this.GroupDirectCursorName(index)} = 0;");
                            } else {
                                builder.AppendLine($"private global::Seamless.Linq.DictionarySlim<{ComparerKeyType(stage)},{aggPlan.AccumulatorType}> {this.GroupAggMapName(index)};");
                                builder.AppendLine($"private global::Seamless.Linq.DictionarySlim<{ComparerKeyType(stage)},{aggPlan.AccumulatorType}>.Enumerator {this.GroupAggEnumName(index)};");
                            }
                        } else {
                            builder.AppendLine($"private global::Seamless.Linq.Grouping<{ComparerKeyType(stage)},{stage.InnerElementType}>? {this.GroupCursorName(index)} = null;");
                            builder.AppendLine($"private global::Seamless.Linq.Grouping<{ComparerKeyType(stage)},{stage.InnerElementType}>? {this.GroupFirstName(index)} = null;");
                        }
                    }
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
            if(this.SetAllocation.Length>0){builder.AppendLine($"private bool {this.SetsReadyName};");}
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetEnumeratorConstructor {
        get {
            var builder = new StringBuilder();
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(this.CountOrdinal(index));
                    builder.AppendLine($"this.{countName}Value = global::System.Math.Max(0, {countName});");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    // 重複除去・集合演算の集合は、最初の TryGetNext で作る(列挙子の構築時には作らない)。
    // 遅延のまま返した結果(ValueEnumerable)は列挙のたびに列挙子の構造体を写すので、構築時に作ると写しどうしが同じ集合を使い、
    // 1回目の列挙の Dispose で返した集合を2回目の列挙が使ってしまう。
    private string SetsReadyName => $"__setsReady{this.item}";
    private string SetAllocation {
        get {
            var builder = new StringBuilder();
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    // 前の段が Where/Select だけなら、集合を根の件数で作る(足しながら広げ直さない。途中で止まる並びでは 0)。
                    var distinctCount = $"__distinctCount{this.item}_{index}";
                    var distinctCapacity = stages.Take(index).All(static before => before.Kind is PipelineStageKind.Where or PipelineStageKind.Select)
                        ? $"this.{this.e}.TryGetNonEnumeratedCount(out var {distinctCount})?{distinctCount}:0"
                        : "0";
                    builder.AppendLine($"this.{this.SetName(index)} = new({distinctCapacity},{this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    // 両方のソースの件数が列挙せずに分かる分だけ、集合を最初からその大きさで作る(足しながら広げ直さない)。
                    var firstCount = $"__unionFirstCount{this.item}_{index}";
                    var secondCount = $"__unionSecondCount{this.item}_{index}";
                    // 前の段が Where/Select だけのときに限る(Take などで途中で止まる並びでは根の件数が集合へ入る数より桁違いに大きくなり得る)。
                    var firstCountExpression = stages.Take(index).All(static before => before.Kind is PipelineStageKind.Where or PipelineStageKind.Select)
                        ? $"(this.{this.e}.TryGetNonEnumeratedCount(out var {firstCount})?{firstCount}:0)"
                        : "0";
                    builder.AppendLine($"this.{this.SetName(index)} = new({firstCountExpression}+(this.{this.SecondEnumeratorName(this.SecondOrdinal(index))}.TryGetNonEnumeratedCount(out var {secondCount})?{secondCount}:0),{this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    if(!this.UsesInlineIntersectSet(stage)){
                        // 第2ソースの件数が列挙せずに分かれば、集合を最初からその大きさで作る。
                        var secondCount = $"__secondCount{this.item}_{index}";
                        builder.AppendLine($"this.{this.SecondSetName(index)} = new(this.{this.SecondEnumeratorName(this.SecondOrdinal(index))}.TryGetNonEnumeratedCount(out var {secondCount})?{secondCount}:0,{this.ComparerName(this.ComparerOrdinal(index))});");
                    }
                    if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
                        // 出したものの集合は第1ソースの要素が入るので、前の段が Where/Select だけなら根の件数で作る
                        // (Take などで途中で止まる並びでは根の件数が集合へ入る数より桁違いに大きくなり得る)。
                        var outputCount = $"__exceptOutputCount{this.item}_{index}";
                        var outputCapacity = stages.Take(index).All(static before => before.Kind is PipelineStageKind.Where or PipelineStageKind.Select)
                            ? $"this.{this.e}.TryGetNonEnumeratedCount(out var {outputCount})?{outputCount}:0"
                            : "0";
                        builder.AppendLine($"this.{this.SetName(index)} = new({outputCapacity},{this.ComparerName(this.ComparerOrdinal(index))});");
                    }
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetTryGetNextBody {
        get {
            var loop = this.BuildLoop(this.e,"current=this.Current={0};\nreturn true;","current=default!;\nreturn false;");
            if(this.SupportsOrderedMaterialization){loop=this.BuildOrderedFirstLoop()+"\n"+loop;}
            var guard = this.TakeEntryGuard();
            var body = guard.Length==0 ? loop : guard+"\n"+loop;
            var sets = this.SetAllocation;
            if(sets.Length>0){body=$"if(!this.{this.SetsReadyName}) {{\nthis.{this.SetsReadyName}=true;\n{sets}\n}}\n{body}";}
            return body;
        }
    }

    // Take の上限に達したら、次の要素を上流から取る前に(上流の列挙子・ラムダを動かす前に)終わる。
    // System.Linq は上限に達した次の要素の上流を動かさない。ループの中の判定だけだと、次の要素を取って上流のラムダを実行した後に気づく。
    // Take より後ろが「入力1つにつき出力が0〜1個」の段だけのときに限る(Take の後ろに OrderBy・SelectMany・Concat などがあると、
    // Take が上限に達した後も、溜めた分や足した分を返す必要がある)。
    private string TakeEntryGuard() {
        var builder = new StringBuilder();
        for(var index = 0;index<stages.Count;index++) {
            if(stages[index].Kind!=PipelineStageKind.Take||stages[index].UsesIndex||stages[index].Value is null) {
                continue;
            }
            if(!stages.Skip(index+1).All(PipelineStageTraits.IsStreamingAtMostOne)) {
                continue;
            }
            builder.AppendLine($"if(this.{this.StateName(index)}>=this.{this.CountName(this.CountOrdinal(index))}Value) {{");
            builder.AppendLine("    current=default!;");
            builder.AppendLine("    return false;");
            builder.AppendLine("}");
        }
        return builder.ToString().TrimEnd();
    }
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
            return this.StagesElementCountPreserving
                ? $"return this.{this.e}.TryGetNonEnumeratedCount(out count);"
                : base.GetTryGetNonEnumeratedCountBody;
        }
    }
    protected override string GetTryGetSpanBody {
        get {
            if(this.SingleDefaultIfEmpty) {
                // 要素(または既定値)を 1 つ返したあとは span を出さない。TryGetSpan の答えが途中で変わると、最初の判定で「span 経路でない」と
                // 決めた消費側(Reverse など)が、あとから引き直して空の span を読み、例外になる。false なら消費側は TryGetNext に戻る。
                return $$"""
                    if(this.{{this.StateName(0)}}){
                        span=default;
                        return false;
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
                        return false;
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
                    builder.AppendLine($"this.{this.SetName(index)}?.Dispose();");
                } else if(stages[index].Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    if(!this.UsesInlineIntersectSet(stages[index])){
                        builder.AppendLine($"this.{this.SecondSetName(index)}?.Dispose();");
                    }
                    if(stages[index].Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
                        builder.AppendLine($"this.{this.SetName(index)}?.Dispose();");
                    }
                } else if(stages[index].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine($"if(this.{this.OrderValuesName(index)} is not null && this.{this.OrderValuesName(index)}.Length!=0) {{");
                    builder.AppendLine($"    global::System.Buffers.ArrayPool<{stages[index].InputType}>.Shared.Return(this.{this.OrderValuesName(index)},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{stages[index].InputType}>());");
                    builder.AppendLine($"    this.{this.OrderValuesName(index)}=null;");
                    builder.AppendLine("}");
                    builder.AppendLine($"if(this.{this.OrderIndexesName(index)} is not null && this.{this.OrderIndexesName(index)}.Length!=0) {{");
                    builder.AppendLine($"    global::System.Buffers.ArrayPool<int>.Shared.Return(this.{this.OrderIndexesName(index)},clearArray:false);");
                    builder.AppendLine($"    this.{this.OrderIndexesName(index)}=null;");
                    builder.AppendLine("}");
                } else if(stages[index] is {Kind:PipelineStageKind.GroupBy,AggregatePlan:not null}
                    &&!this.UsesInlineParityCounts(stages[index])
                    &&!stages.Skip(index+1).Any(static stage=>stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending)) {
                    if(this.TryGetSmallKeyRange(stages[index], out _)) {
                        builder.AppendLine($"if(this.{this.GroupDirectKeysName(index)} is not null) {{");
                        builder.AppendLine($"    global::System.Buffers.ArrayPool<int>.Shared.Return(this.{this.GroupDirectKeysName(index)},clearArray:false);");
                        builder.AppendLine($"    this.{this.GroupDirectKeysName(index)}=null;");
                        builder.AppendLine("}");
                        builder.AppendLine($"if(this.{this.GroupDirectAccsName(index)} is not null) {{");
                        builder.AppendLine($"    global::System.Buffers.ArrayPool<{stages[index].AggregatePlan!.AccumulatorType}>.Shared.Return(this.{this.GroupDirectAccsName(index)},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{stages[index].AggregatePlan!.AccumulatorType}>());");
                        builder.AppendLine($"    this.{this.GroupDirectAccsName(index)}=null;");
                        builder.AppendLine("}");
                    } else {
                        builder.AppendLine($"this.{this.GroupAggMapName(index)}.Dispose();");
                    }
                    // null reset omitted for struct
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

    private IEnumerable<FuncFragment> Funcs => stages.SelectMany(static stage => {
        var baseFuncs = new[] { stage.Func,stage.SecondFunc,stage.ThirdFunc }.Where(static func => func is not null);
        if(stage.AggregatePlan is { } aggPlan){
            return baseFuncs.Concat(aggPlan.Items.Select(static item => item.InnerFunc).Where(static func => func is not null));
        }else{
            return baseFuncs;
        }
    }).Cast<FuncFragment>();
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

    private bool UsesInlineIntersectSet(PipelineStage stage)=>
        stages.Count==1&&stage.Kind==PipelineStageKind.Intersect&&stage.Value=="null"&&ComparerKeyType(stage)=="int"
        &&stage.SecondSource is PipelineEnumerable {ElementType:"int"} second
        &&second.PipelineStages.Count==1&&second.UsesInlineParityCounts(second.PipelineStages[0]);

    // 偶奇集計は最大2件を返す。第2列を読み切ってから、左の初出順で一致を取り除く。
    private static string InlineIntersectSetDefinition(string name)=>$$"""
        private struct {{name}}Inline {
            private int first,second,occupied;

            public void Add(int value){
                if(this.occupied==0){
                    this.first=value;
                    this.occupied=1;
                }else if(this.first!=value){
                    this.second=value;
                    this.occupied=3;
                }
            }

            public bool Remove(int value){
                if((this.occupied&1)!=0&&this.first==value){
                    this.occupied&=~1;
                    return true;
                }
                if((this.occupied&2)!=0&&this.second==value){
                    this.occupied&=~2;
                    return true;
                }
                return false;
            }
        }
        """;

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
