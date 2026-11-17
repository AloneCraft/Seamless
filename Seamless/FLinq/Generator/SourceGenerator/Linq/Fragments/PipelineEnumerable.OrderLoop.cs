using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

// 並べ替え段のバッファ構築とソート。
internal sealed partial class PipelineEnumerable {
    private bool skipSingletonOrderKeyEvaluation;
    private bool useOrderedFirst;
    private const string OrderedMaterializationFlag="__skipSingletonOrderKeyEvaluation";
    private const string OrderedFirstFlag="__orderedFirst";
    private const string OrderedFirstCompletedFlag="__orderedFirstCompleted";
    internal const string OrderedMaterializationMethod="__UseOrderedMaterialization";
    internal const string OrderedConsumerMethod="__SetOrderedConsumer";
    internal const string OrderedSourceProperty="__OrderedSourceEnumerator";
    internal enum OrderedConsumerDemand{Enumerate,Materialize,First}
    internal bool SupportsOrderedMaterialization=>stages.Count>0&&stages.All(static stage=>stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending);

    internal static PipelineEnumerable CreateWithOrderSourceDemand(int index,Enumerable source,IReadOnlyList<PipelineStage> stages,string elementType,string? parallelSlots=null,string? orderHint=null){
        var boundSource=BindOrderedSource(source,stages,OrderedConsumerDemand.Enumerate);
        return new PipelineEnumerable(index,boundSource,stages,elementType){ParallelSlots=parallelSlots,OrderHint=orderHint};
    }

    private static Enumerable BindOrderedSource(Enumerable source,IReadOnlyList<PipelineStage> stages,OrderedConsumerDemand demand){
        // Order の通常列挙も入力を ToArray で読む。Where 等の前処理があれば通常列挙の境界になる。
        if(stages.Count>0&&stages[0].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending
            &&source is PipelineEnumerable{SupportsOrderedMaterialization:true} orderedSource){
            if(demand==OrderedConsumerDemand.First){
                return orderedSource.ForOrderedConsumer(OrderedConsumerDemand.Enumerate);
            }
            return orderedSource.ForOrderedConsumer(OrderedConsumerDemand.Materialize);
        }
        return source;
    }

    internal PipelineEnumerable ForOrderedMaterialization()=>this.ForOrderedConsumer(OrderedConsumerDemand.Materialize);

    internal PipelineEnumerable ForOrderedConsumer(OrderedConsumerDemand demand){
        // 構造体の初期需要だけを変え、元の alias や capture/source 式は再評価しない。
        var boundSource=BindOrderedSource(source,stages,demand);
        var pipeline=new PipelineEnumerable(this.FragmentIndex,boundSource,stages,elementType){
            skipSingletonOrderKeyEvaluation=demand==OrderedConsumerDemand.Materialize,
            useOrderedFirst=demand==OrderedConsumerDemand.First,
            SourceContainsAnchor=this.SourceContainsAnchor,
            ParallelSlots=this.ParallelSlots,
            OrderHint=this.OrderHint
        };
        pipeline.ApplyRenderContext(this.RenderContext);
        return pipeline;
    }

    private string BuildOrderedFirstLoop(){
        var builder=new StringBuilder();
        var bestItem=$"__orderedFirstBest{this.item}";
        var candidate=$"__orderedFirstCandidate{this.item}";
        var improved=$"__orderedFirstImproved{this.item}";
        string BestKey(int level)=>$"__orderedFirstBestKey{this.item}_{level}";
        string CandidateKey(int level)=>$"__orderedFirstKey{this.item}_{level}";
        void AssignKey(int level,string destination,string value,string indent){
            var stage=stages[level];
            if(stage.Func is { } selector){
                builder.AppendLine(IndentBlock(this.FuncAssign(selector,stage.Kind.ToString(),destination,value),indent));
            }else{
                builder.AppendLine($"{indent}{destination}={value};");
            }
        }
        void CompareLevel(int level,string indent){
            var stage=stages[level];
            var keyType=ComparerKeyType(stage);
            var key=CandidateKey(level);
            var best=BestKey(level);
            builder.AppendLine($"{indent}{keyType} {key}=default!;");
            AssignKey(level,key,candidate,indent);
            var left=key;
            var right=best;
            // 単一キーの TryGetFirst は candidate,best の順で比較する。
            // ThenBy を持つ CachingComparer は降順時に引数を逆にする。
            var singleDescending=stages.Count==1&&stage.Descending;
            if(stage.Descending&&!singleDescending){left=best;right=key;}
            var comparer=$"(this.{this.OrderComparerName(this.OrderComparerOrdinal(level))}??global::System.Collections.Generic.Comparer<{keyType}>.Default)";
            var comparison=$"__orderedFirstCompare{this.item}_{level}";
            builder.AppendLine($"{indent}var {comparison}={comparer}.Compare({left},{right});");
            var improves=$"{comparison}<0";
            if(singleDescending){improves=$"{comparison}>0";}
            builder.AppendLine($"{indent}if({improves}){{");
            builder.AppendLine($"{indent}    {best}={key};");
            // CachingComparer は最良値の交代時に下位キーもすぐ更新する。
            for(var child=level+1;child<stages.Count;child++){
                AssignKey(child,BestKey(child),candidate,indent+"    ");
            }
            builder.AppendLine($"{indent}    {improved}=true;");
            if(level+1<stages.Count){
                builder.AppendLine($"{indent}}}else if({comparison}==0){{");
                CompareLevel(level+1,indent+"    ");
            }
            builder.AppendLine($"{indent}}}");
        }
        builder.AppendLine($"if(this.{OrderedFirstFlag}){{");
        builder.AppendLine($"    if(this.{OrderedFirstCompletedFlag}){{current=default!;return false;}}");
        builder.AppendLine($"    this.{OrderedFirstCompletedFlag}=true;");
        builder.AppendLine($"    if(!this.{this.e}.TryGetNext(out var {bestItem})){{current=default!;return false;}}");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        for(var level=0;level<stages.Count;level++){
            builder.AppendLine($"    {ComparerKeyType(stages[level])} {BestKey(level)}=default!;");
            AssignKey(level,BestKey(level),bestItem,"    ");
        }
        builder.AppendLine($"    while(this.{this.e}.TryGetNext(out var {candidate})){{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        builder.AppendLine($"        var {improved}=false;");
        CompareLevel(0,"        ");
        builder.AppendLine($"        if({improved}){{{bestItem}={candidate};}}");
        builder.AppendLine("    }");
        builder.AppendLine($"    current=this.Current={bestItem};");
        builder.AppendLine("    return true;");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string BuildOrderLoop(string enumeratorName,string successStatement,string endStatement,int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        var lastOrderIndex = orderIndex+orderStages.Length-1;
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.OrderValuesName(orderIndex)} is null) {{");
        if(this.CanUseSortedDefaultOrderFastPath(orderIndex,primaryOrder,orderStages)) {
            this.AppendSortedDefaultOrderFastPath(builder,enumeratorName,successStatement,endStatement,orderIndex,lastOrderIndex,primaryOrder);
        }
        var hasTopK = this.CanUseTopKOrder(orderIndex,orderStages,out var takeCountExpression);
        if(hasTopK) {
            this.AppendTopKOrderFastPath(builder,enumeratorName,orderIndex,orderStages,takeCountExpression);
            builder.AppendLine("    else {");
        }
        var indent = hasTopK ? "    " : "";
        builder.AppendLine($"{indent}    var {this.OrderInitialCapacityLocalName(orderIndex)}=16;");
        builder.AppendLine($"{indent}    if({enumeratorName}.TryGetNonEnumeratedCount(out var __orderSourceCount{orderIndex}) && __orderSourceCount{orderIndex}>16) {{");
        builder.AppendLine($"{indent}        {this.OrderInitialCapacityLocalName(orderIndex)}=__orderSourceCount{orderIndex};");
        builder.AppendLine("    }");
        this.AppendOrderBufferBuildStart(builder,orderIndex,indent);
        builder.AppendLine($"{indent}        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,$"{indent}        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<orderIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        this.AppendOrderBufferedValue(builder,orderIndex,currentName,$"{indent}            ");
        builder.AppendLine($"{indent}        }}");
        this.AppendOrderBufferBuildEnd(builder,orderIndex,orderStages,indent);
        if(hasTopK) {
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(orderIndex)}<this.{this.OrderCountName(orderIndex)}) {{");
        var sortedCurrentName = this.ItemName(lastOrderIndex+1);
        builder.AppendLine($"    var {sortedCurrentName}=this.{this.OrderValuesName(orderIndex)}[this.{this.OrderIndexesName(orderIndex)}[this.{this.StateName(orderIndex)}++]];");
        currentName=sortedCurrentName;
        for(var index = lastOrderIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private void AppendOrderBufferBuildStart(StringBuilder builder,int orderIndex,string indent="") {
        var valueType = stages[orderIndex].InputType;
        var valuesLocal = this.OrderValuesLocalName(orderIndex);
        var indexesLocal = this.OrderIndexesLocalName(orderIndex);
        var returnValuesLocal = this.OrderReturnValuesLocalName(orderIndex);
        var returnIndexesLocal = this.OrderReturnIndexesLocalName(orderIndex);
        builder.AppendLine($"{indent}    var {valuesLocal}=global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent({this.OrderInitialCapacityLocalName(orderIndex)});");
        builder.AppendLine($"{indent}    var {indexesLocal}=global::System.Buffers.ArrayPool<int>.Shared.Rent({this.OrderInitialCapacityLocalName(orderIndex)});");
        builder.AppendLine($"{indent}    var {returnValuesLocal}=true;");
        builder.AppendLine($"{indent}    var {returnIndexesLocal}=true;");
        builder.AppendLine($"{indent}    var {this.OrderCountLocalName(orderIndex)}=0;");
        foreach(var orderStage in this.OrderStages(orderIndex)) {
            builder.AppendLine($"{indent}    {ComparerKeyType(orderStage)}[]? {this.OrderKeysLocalName(orderIndex,orderStage)}=null;");
        }
        builder.AppendLine($"{indent}    try{{");
    }

    private void AppendOrderBufferBuildEnd(StringBuilder builder,int orderIndex,IReadOnlyList<PipelineStage> orderStages,string indent="") {
        this.AppendOrderSort(builder,orderIndex,orderStages);
        builder.AppendLine($"{indent}        this.{this.OrderValuesName(orderIndex)}={this.OrderValuesLocalName(orderIndex)};");
        builder.AppendLine($"{indent}        this.{this.OrderIndexesName(orderIndex)}={this.OrderIndexesLocalName(orderIndex)};");
        builder.AppendLine($"{indent}        this.{this.OrderCountName(orderIndex)}={this.OrderCountLocalName(orderIndex)};");
        builder.AppendLine($"{indent}        this.{this.StateName(orderIndex)}=0;");
        builder.AppendLine($"{indent}        {this.OrderReturnValuesLocalName(orderIndex)}=false;");
        builder.AppendLine($"{indent}        {this.OrderReturnIndexesLocalName(orderIndex)}=false;");
        builder.AppendLine($"{indent}    }}finally{{");
        foreach(var orderStage in orderStages) {
            var keysLocal = this.OrderKeysLocalName(orderIndex,orderStage);
            builder.AppendLine($"{indent}        if({keysLocal} is not null) {{");
            builder.AppendLine($"{indent}            global::System.Buffers.ArrayPool<{ComparerKeyType(orderStage)}>.Shared.Return({keysLocal},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{ComparerKeyType(orderStage)}>());");
            builder.AppendLine($"{indent}        }}");
        }
        builder.AppendLine($"{indent}        if({this.OrderReturnIndexesLocalName(orderIndex)}) {{");
        builder.AppendLine($"{indent}            global::System.Buffers.ArrayPool<int>.Shared.Return({this.OrderIndexesLocalName(orderIndex)},clearArray:false);");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}        if({this.OrderReturnValuesLocalName(orderIndex)}) {{");
        builder.AppendLine($"{indent}            global::System.Buffers.ArrayPool<{stages[orderIndex].InputType}>.Shared.Return({this.OrderValuesLocalName(orderIndex)},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{stages[orderIndex].InputType}>());");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine("    }");
    }

    private void AppendOrderBufferedValue(StringBuilder builder,int orderIndex,string currentName,string indent) {
        var valueType = stages[orderIndex].InputType;
        var valuesLocal = this.OrderValuesLocalName(orderIndex);
        var indexesLocal = this.OrderIndexesLocalName(orderIndex);
        var countLocal = this.OrderCountLocalName(orderIndex);
        var nextCapacity = this.OrderNextCapacityLocalName(orderIndex);
        var nextValues = this.OrderNextValuesLocalName(orderIndex);
        var nextIndexes = this.OrderNextIndexesLocalName(orderIndex);
        builder.AppendLine($"{indent}if({countLocal}=={valuesLocal}.Length){{");
        builder.AppendLine($"{indent}    var {nextCapacity}={valuesLocal}.Length*2;");
        builder.AppendLine($"{indent}    var {nextValues}=global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent({nextCapacity});");
        builder.AppendLine($"{indent}    global::System.Array.Copy({valuesLocal},{nextValues},{countLocal});");
        builder.AppendLine($"{indent}    global::System.Buffers.ArrayPool<{valueType}>.Shared.Return({valuesLocal},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{valueType}>());");
        builder.AppendLine($"{indent}    {valuesLocal}={nextValues};");
        builder.AppendLine($"{indent}    var {nextIndexes}=global::System.Buffers.ArrayPool<int>.Shared.Rent({nextCapacity});");
        builder.AppendLine($"{indent}    global::System.Array.Copy({indexesLocal},{nextIndexes},{countLocal});");
        builder.AppendLine($"{indent}    global::System.Buffers.ArrayPool<int>.Shared.Return({indexesLocal},clearArray:false);");
        builder.AppendLine($"{indent}    {indexesLocal}={nextIndexes};");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}{valuesLocal}[{countLocal}]={currentName};");
        builder.AppendLine($"{indent}{countLocal}++;");
    }

    private void AppendOrderSort(StringBuilder builder,int orderIndex,IReadOnlyList<PipelineStage> orderStages) {
        var valuesLocal = this.OrderValuesLocalName(orderIndex);
        var indexesLocal = this.OrderIndexesLocalName(orderIndex);
        var countLocal = this.OrderCountLocalName(orderIndex);
        var valueName = this.OrderValueForKeysLocalName(orderIndex);
        var lastOrderIndex = orderIndex+orderStages.Count-1;
        var usesPartialSort = this.TryGetOrderSortRange(lastOrderIndex,countLocal,out var minSortIndexExpression,out var maxSortIndexExpression);
        var useDirectValueSort = !usesPartialSort&&this.CanUseDirectValueOrderSort(orderStages);
        // 通常のordered列挙は1件でも鍵を評価する。plain ToArray/ToListだけは終端需要で省略する。
        builder.AppendLine($"        if({countLocal}==1){{");
        if((this.SupportsOrderedMaterialization||!this.skipSingletonOrderKeyEvaluation)&&orderStages.Any(static stage=>stage.Func is not null)){
            if(this.SupportsOrderedMaterialization){
                builder.AppendLine($"            if(!this.{OrderedMaterializationFlag}){{");
            }
            builder.AppendLine($"            var {valueName}={valuesLocal}[0];");
            foreach(var orderStage in orderStages){
                if(orderStage.Func is not { } selector){continue;}
                var keyName=this.OrderKeyLocalName(orderIndex,orderStage);
                builder.AppendLine($"            {ComparerKeyType(orderStage)} {keyName}=default!;");
                builder.AppendLine(IndentBlock(this.FuncAssign(selector,orderStage.Kind.ToString(),keyName,valueName),"            "));
                builder.AppendLine($"            _={keyName};");
            }
            if(this.SupportsOrderedMaterialization){
                builder.AppendLine("            }");
            }
        }
        builder.AppendLine($"            {indexesLocal}[0]=0;");
        builder.AppendLine($"        }}else if({countLocal}>1){{");
        var checksSorted = this.CanCheckBufferedValuesSorted(orderIndex,orderStages);
        if(checksSorted) {
            // 溜めた値が既に並べ替え後の順(昇順なら非減少、降順なら非増加)なら、元の順がそのまま安定な並べ替えの結果なので並べ替えない。
            // 並んでいなければ最初の逆転で抜けるので、並べ替える場合の追加の手間はわずか。
            var sortedName = $"__orderBufferedSorted{orderIndex}";
            var checkIndexName = $"__orderCheckIndex{orderIndex}";
            builder.AppendLine($"            var {sortedName}=true;");
            builder.AppendLine($"            for(var {checkIndexName}=1;{checkIndexName}<{countLocal};{checkIndexName}++){{");
            builder.AppendLine($"                if({this.BufferedOutOfOrderExpression(orderStages[0],valuesLocal,checkIndexName)}){{");
            builder.AppendLine($"                    {sortedName}=false;");
            builder.AppendLine("                    break;");
            builder.AppendLine("                }");
            builder.AppendLine("            }");
            builder.AppendLine($"            if({sortedName}){{");
            builder.AppendLine($"                for(var {checkIndexName}=0;{checkIndexName}<{countLocal};{checkIndexName}++){{");
            builder.AppendLine($"                    {indexesLocal}[{checkIndexName}]={checkIndexName};");
            builder.AppendLine("                }");
            builder.AppendLine("            }else{");
        }
        if(useDirectValueSort) {
            var valueType = stages[orderIndex].InputType;
            builder.AppendLine($"            var __orderType{orderIndex}=typeof({valueType});");
            builder.AppendLine($"            if(__orderType{orderIndex}.IsEnum||__orderType{orderIndex}==typeof(sbyte)||__orderType{orderIndex}==typeof(byte)||__orderType{orderIndex}==typeof(bool)||__orderType{orderIndex}==typeof(short)||__orderType{orderIndex}==typeof(ushort)||__orderType{orderIndex}==typeof(char)||__orderType{orderIndex}==typeof(int)||__orderType{orderIndex}==typeof(uint)||__orderType{orderIndex}==typeof(long)||__orderType{orderIndex}==typeof(ulong)||__orderType{orderIndex}==typeof(global::System.Int128)||__orderType{orderIndex}==typeof(global::System.UInt128)||__orderType{orderIndex}==typeof(nint)||__orderType{orderIndex}==typeof(nuint)){{");
            builder.AppendLine($"                global::System.Array.Sort({valuesLocal},0,{countLocal});");
            if(orderStages[0].Descending) {
                builder.AppendLine($"                global::System.Array.Reverse({valuesLocal},0,{countLocal});");
            }
            builder.AppendLine($"                for(var __orderDirectIndex{orderIndex}=0;__orderDirectIndex{orderIndex}<{countLocal};__orderDirectIndex{orderIndex}++){{");
            builder.AppendLine($"                    {indexesLocal}[__orderDirectIndex{orderIndex}]=__orderDirectIndex{orderIndex};");
            builder.AppendLine("                }");
            builder.AppendLine("            }else{");
        }
        foreach(var orderStage in orderStages) {
            var indent = useDirectValueSort ? "                " : "            ";
            builder.AppendLine($"{indent}{this.OrderKeysLocalName(orderIndex,orderStage)}=global::System.Buffers.ArrayPool<{ComparerKeyType(orderStage)}>.Shared.Rent({countLocal});");
        }
        var fallbackIndent = useDirectValueSort ? "                " : "            ";
        // EnumerableSorter は一次キー全件を先に計算し、その後に ThenBy のキー全件を計算する。
        foreach(var orderStage in orderStages) {
            builder.AppendLine($"{fallbackIndent}for(var __orderKeyIndex{orderIndex}=0;__orderKeyIndex{orderIndex}<{countLocal};__orderKeyIndex{orderIndex}++){{");
            if(ReferenceEquals(orderStage,orderStages[0])){
                builder.AppendLine($"{fallbackIndent}    {indexesLocal}[__orderKeyIndex{orderIndex}]=__orderKeyIndex{orderIndex};");
            }
            builder.AppendLine($"{fallbackIndent}    var {valueName}={valuesLocal}[__orderKeyIndex{orderIndex}];");
            var keyName = this.OrderKeyLocalName(orderIndex,orderStage);
            builder.AppendLine($"{fallbackIndent}    {ComparerKeyType(orderStage)} {keyName}=default!;");
            if(orderStage.Func is null) {
                builder.AppendLine($"{fallbackIndent}    {keyName}={valueName};");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(orderStage.Func,orderStage.Kind.ToString(),keyName,valueName),fallbackIndent+"    "));
            }
            builder.AppendLine($"{fallbackIndent}    {this.OrderKeysLocalName(orderIndex,orderStage)}![__orderKeyIndex{orderIndex}]={keyName};");
            builder.AppendLine($"{fallbackIndent}}}");
        }
        var comparerArgs = orderStages
            .SelectMany(orderStage => {
                var absoluteIndex = this.OrderStageAbsoluteIndex(orderIndex,orderStage);
                var args = new List<string> { $"{this.OrderKeysLocalName(orderIndex,orderStage)}!" };
                if(!CanUseDirectDefaultOrderCompare(orderStage)) {
                    args.Add(this.OrderComparerName(this.OrderComparerOrdinal(absoluteIndex)));
                }
                return args;
            });
        if(usesPartialSort) {
            builder.AppendLine($"{fallbackIndent}global::Seamless.Linq.GeneratedCodeHelpers.PartialSortIndexes({indexesLocal},{countLocal},{minSortIndexExpression},{maxSortIndexExpression},new {this.OrderIndexComparerName(orderIndex)}({string.Join(",",comparerArgs)}));");
        } else {
            var usesCountingSort=orderStages.Count==1
                &&!orderStages[0].Descending
                &&IsInt32Type(ComparerKeyType(orderStages[0]))
                &&ComparerExpressionSupport.IsDefaultOrderComparer(orderStages[0].Value!,ComparerKeyType(orderStages[0]));
            if(usesCountingSort){
                this.AppendSmallRangeIntOrderSort(builder,orderIndex,orderStages[0],fallbackIndent);
                builder.AppendLine($"{fallbackIndent}if(!__orderCountingSorted{orderIndex}){{");
            }
            builder.AppendLine($"{fallbackIndent}global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes({indexesLocal},{countLocal},new {this.OrderIndexComparerName(orderIndex)}({string.Join(",",comparerArgs)}));");
            if(usesCountingSort){
                builder.AppendLine($"{fallbackIndent}}}");
            }
        }
        if(useDirectValueSort) {
            builder.AppendLine("            }");
        }
        if(checksSorted) {
            builder.AppendLine("            }");
        }
        builder.AppendLine("        }");
    }

    // 全キーの評価後だけに適用し、同じキーの元の添字順と後段の短絡を維持する。
    private void AppendSmallRangeIntOrderSort(StringBuilder builder,int orderIndex,PipelineStage orderStage,string indent){
        var keysLocal=this.OrderKeysLocalName(orderIndex,orderStage);
        var indexesLocal=this.OrderIndexesLocalName(orderIndex);
        var countLocal=this.OrderCountLocalName(orderIndex);
        var sorted=$"__orderCountingSorted{orderIndex}";
        var min=$"__orderCountingMin{orderIndex}";
        var max=$"__orderCountingMax{orderIndex}";
        var index=$"__orderCountingIndex{orderIndex}";
        var key=$"__orderCountingKey{orderIndex}";
        var range=$"__orderCountingRange{orderIndex}";
        var buckets=$"__orderCountingBuckets{orderIndex}";
        var bucket=$"__orderCountingBucket{orderIndex}";
        var offset=$"__orderCountingOffset{orderIndex}";
        var size=$"__orderCountingSize{orderIndex}";
        builder.AppendLine($"{indent}var {sorted}=false;");
        builder.AppendLine($"{indent}if({countLocal}>=64){{");
        builder.AppendLine($"{indent}    var {min}={keysLocal}![0];");
        builder.AppendLine($"{indent}    var {max}={min};");
        builder.AppendLine($"{indent}    for(var {index}=1;{index}<{countLocal};{index}++){{");
        builder.AppendLine($"{indent}        var {key}={keysLocal}[{index}];");
        builder.AppendLine($"{indent}        if({key}<{min}){{{min}={key};}}");
        builder.AppendLine($"{indent}        if({key}>{max}){{{max}={key};}}");
        // 幅は縮まらないため、対象外が確定したらsnapshotの追加走査を打ち切る。
        builder.AppendLine($"{indent}        if({min}<0||(long){max}-{min}>=256){{break;}}");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    var {range}=(long){max}-{min}+1;");
        builder.AppendLine($"{indent}    if({min}>=0&&{range}<=256){{");
        builder.AppendLine($"{indent}        global::System.Span<int> {buckets}=stackalloc int[256];");
        builder.AppendLine($"{indent}        {buckets}.Clear();");
        builder.AppendLine($"{indent}        for(var {index}=0;{index}<{countLocal};{index}++){{");
        builder.AppendLine($"{indent}            {buckets}[{keysLocal}[{index}]-{min}]++;");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}        var {offset}=0;");
        builder.AppendLine($"{indent}        for(var {bucket}=0;{bucket}<{range};{bucket}++){{");
        builder.AppendLine($"{indent}            var {size}={buckets}[{bucket}];");
        builder.AppendLine($"{indent}            {buckets}[{bucket}]={offset};");
        builder.AppendLine($"{indent}            {offset}+={size};");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}        for(var {index}=0;{index}<{countLocal};{index}++){{");
        builder.AppendLine($"{indent}            var {bucket}={keysLocal}[{index}]-{min};");
        builder.AppendLine($"{indent}            {indexesLocal}[{buckets}[{bucket}]++]={index};");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}        {sorted}=true;");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");
    }

    // 順序段の直後に Skip/Take があるとき、並べ替えが必要な添字範囲 [minIndex, maxIndex] を求める。
    private bool TryGetOrderSortRange(int lastOrderIndex,string countLocal,out string minIndexExpression,out string maxIndexExpression) {
        string? skipExpression = null;
        string? takeExpression = null;
        for(var index = lastOrderIndex+1;index<stages.Count;index++) {
            var stage = stages[index];
            if(stage.Kind==PipelineStageKind.AsEnumerable) {
                continue;
            }
            if(stage.Kind==PipelineStageKind.Skip&&takeExpression is null) {
                var countField = $"this.{this.CountName(this.CountOrdinal(index))}Value";
                skipExpression=skipExpression is null ? countField : $"({skipExpression}+{countField})";
                continue;
            }
            if(stage.Kind==PipelineStageKind.Take) {
                var countField = $"this.{this.CountName(this.CountOrdinal(index))}Value";
                takeExpression=takeExpression is null ? countField : $"global::System.Math.Min({takeExpression},{countField})";
                break;
            }
            break;
        }
        if(takeExpression is null) {
            minIndexExpression=string.Empty;
            maxIndexExpression=string.Empty;
            return false;
        }
        if(skipExpression is null) {
            minIndexExpression="0";
            maxIndexExpression=$"{takeExpression}-1";
        } else {
            minIndexExpression=skipExpression;
            maxIndexExpression=$"(int)global::System.Math.Min((long){countLocal}-1,(long){skipExpression}+{takeExpression}-1)";
        }
        return true;
    }

    // 恒等キー・既定の比較子の1段の並べ替えで、ソースの span を見る昇順高速経路を使わないとき(前に段があるなど)は、
    // 溜めた値が既に並んでいるかを並べ替えの前に確かめる。Where を並べ替えの前へ押し下げても、並んだソースでは並べ替えを省ける。
    private bool CanCheckBufferedValuesSorted(int orderIndex,IReadOnlyList<PipelineStage> orderStages) =>
        orderStages.Count==1
        &&(orderStages[0].Func is null||orderStages[0].Func!.IsIdentity)
        &&string.Equals(orderStages[0].InputType,ComparerKeyType(orderStages[0]),StringComparison.Ordinal)
        &&orderStages[0].Value is { } comparer
        &&ComparerExpressionSupport.IsDefaultOrderComparer(comparer,ComparerKeyType(orderStages[0]))
        &&!this.CanUseSortedDefaultOrderFastPath(orderIndex,orderStages[0],orderStages);

    // 溜めた値の index 番目が直前と並べ替え後の順で逆転しているか(等しいときは逆転ではない)。
    private string BufferedOutOfOrderExpression(PipelineStage stage,string valuesName,string indexName) {
        var (later,earlier) = stage.Descending
            ? ($"{valuesName}[{indexName}-1]",$"{valuesName}[{indexName}]")
            : ($"{valuesName}[{indexName}]",$"{valuesName}[{indexName}-1]");
        return IsInt32Type(stage.InputType)
            ? $"{later}<{earlier}"
            : $"global::System.Collections.Generic.Comparer<{stage.InputType}>.Default.Compare({later},{earlier})<0";
    }

    private bool CanUseDirectValueOrderSort(IReadOnlyList<PipelineStage> orderStages) =>
        // 整数・enumは比較が等しい値同士を直接sortしても観測可能な値順が変わらない。
        // 参照型や任意comparerではoriginal indexによる安定順序を維持する。
        // 順序保証を外せる別APIなら、後者も値配列の直接sortへ拡張できる。
        orderStages.Count==1
        &&orderStages[0].Func is null
        &&string.Equals(orderStages[0].InputType,ComparerKeyType(orderStages[0]),StringComparison.Ordinal)
        &&ComparerExpressionSupport.IsDefaultOrderComparer(orderStages[0].Value!,ComparerKeyType(orderStages[0]));

    private int OrderStageAbsoluteIndex(int orderIndex,PipelineStage orderStage) {
        for(var index = orderIndex;index<stages.Count;index++) {
            if(ReferenceEquals(stages[index],orderStage)) {
                return index;
            }
        }
        return orderIndex;
    }

    private bool CanUseSortedDefaultOrderFastPath(int orderIndex,PipelineStage primaryOrder,IReadOnlyList<PipelineStage> orderStages) =>
        this.CanUseSortedDefaultOrderFastPathCore(orderIndex,primaryOrder,orderStages)
        &&(!primaryOrder.Descending||IsInt32Type(primaryOrder.InputType));

    private bool CanUseSortedDefaultOrderDirectConsumeFastPath(int orderIndex,PipelineStage primaryOrder,IReadOnlyList<PipelineStage> orderStages) =>
        this.CanUseSortedDefaultOrderFastPathCore(orderIndex,primaryOrder,orderStages);

    private bool CanUseSortedDefaultOrderFastPathCore(int orderIndex,PipelineStage primaryOrder,IReadOnlyList<PipelineStage> orderStages) =>
        orderIndex==0
        &&orderStages.Count==1
        &&source.CanTryProvideSpan
        &&string.Equals(primaryOrder.InputType,ComparerKeyType(primaryOrder),StringComparison.Ordinal)
        &&primaryOrder.Value is { } comparer
        &&ComparerExpressionSupport.IsDefaultOrderComparer(comparer,ComparerKeyType(primaryOrder))
        &&(primaryOrder.Func is null||primaryOrder.Func.IsIdentity)
        &&this.CanRunAfterSortedIntOrderFastPath(orderIndex);

    private bool CanRunAfterSortedIntOrderFastPath(int orderIndex) {
        for(var index = orderIndex+1;index<stages.Count;index++) {
            if(stages[index].Kind is not (PipelineStageKind.Where
                or PipelineStageKind.Select
                or PipelineStageKind.Take
                or PipelineStageKind.Skip
                or PipelineStageKind.TakeWhile
                or PipelineStageKind.SkipWhile
                or PipelineStageKind.Cast
                or PipelineStageKind.OfType
                or PipelineStageKind.Distinct
                or PipelineStageKind.DistinctBy
                or PipelineStageKind.AsEnumerable)) {
                return false;
            }
        }
        return true;
    }

    private void AppendSortedDefaultOrderFastPath(
        StringBuilder builder,
        string enumeratorName,
        string successStatement,
        string endStatement,
        int orderIndex,
        int lastOrderIndex,
        PipelineStage primaryOrder) {
        var activeSpanName = $"__orderActiveSpan{orderIndex}";
        var spanName = $"__orderSpan{orderIndex}";
        var sortedName = $"__orderSorted{orderIndex}";
        var nextIndexName = $"__orderNextIndex{orderIndex}";
        var sortedCurrentName = this.ItemName(lastOrderIndex+1);
        var indexExpression = primaryOrder.Descending
            ? $"{activeSpanName}.Length-1-{nextIndexName}"
            : nextIndexName;
        builder.AppendLine($"    if(this.{this.StateName(orderIndex)}!=0){{");
        builder.AppendLine($"        if({enumeratorName}.TryGetSpan(out var {activeSpanName})){{");
        builder.AppendLine($"            var {nextIndexName}=this.{this.StateName(orderIndex)}>0 ? this.{this.StateName(orderIndex)} : -this.{this.StateName(orderIndex)};");
        builder.AppendLine($"            while({nextIndexName}<{activeSpanName}.Length){{");
        builder.AppendLine($"                var {sortedCurrentName}={activeSpanName}[{indexExpression}];");
        builder.AppendLine($"                this.{this.StateName(orderIndex)}=this.{this.StateName(orderIndex)}>0 ? {nextIndexName}+1 : -({nextIndexName}+1);");
        builder.AppendLine($"                {nextIndexName}++;");
        this.AppendStagesAfterSortedOrder(builder,successStatement,lastOrderIndex,sortedCurrentName,"                ");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine(IndentBlock(endStatement,"        "));
        builder.AppendLine("    }");
        builder.AppendLine($"    if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"        var {sortedName}=true;");
        builder.AppendLine($"        for(var __i=1;__i<{spanName}.Length;__i++){{");
        builder.AppendLine($"            if({this.SortedOrderOutOfOrderExpression(primaryOrder,spanName,"__i")}){{");
        builder.AppendLine($"                {sortedName}=false;");
        builder.AppendLine("                break;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine($"        if({sortedName}){{");
        builder.AppendLine($"            if({spanName}.Length==0){{");
        builder.AppendLine(IndentBlock(endStatement,"                "));
        builder.AppendLine("            }");
        builder.AppendLine($"            var {nextIndexName}=0;");
        builder.AppendLine($"            while({nextIndexName}<{spanName}.Length){{");
        builder.AppendLine($"                var {sortedCurrentName}={spanName}[{(primaryOrder.Descending ? $"{spanName}.Length-1-{nextIndexName}" : nextIndexName)}];");
        builder.AppendLine($"                this.{this.StateName(orderIndex)}={(primaryOrder.Descending ? $"-({nextIndexName}+1)" : $"{nextIndexName}+1")};");
        builder.AppendLine($"                {nextIndexName}++;");
        this.AppendStagesAfterSortedOrder(builder,successStatement,lastOrderIndex,sortedCurrentName,"                ");
        builder.AppendLine("            }");
        builder.AppendLine(IndentBlock(endStatement,"            "));
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private void AppendStagesAfterSortedOrder(StringBuilder builder,string successStatement,int lastOrderIndex,string currentName,string indent) {
        for(var index = lastOrderIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),indent));
    }

    private string SortedOrderOutOfOrderExpression(PipelineStage stage,string spanName,string indexName) =>
        IsInt32Type(stage.InputType)
            ? $"{spanName}[{indexName}]<{spanName}[{indexName}-1]"
            : $"global::System.Collections.Generic.Comparer<{stage.InputType}>.Default.Compare({spanName}[{indexName}],{spanName}[{indexName}-1])<0";

    private bool CanUseTopKOrder(int orderIndex,IReadOnlyList<PipelineStage> orderStages,out string takeCountExpression) {
        takeCountExpression = string.Empty;
        if(orderStages.Count < 1 || orderStages.Count > 2) {
            return false;
        }
        var lastOrderIndex = orderIndex + orderStages.Count - 1;
        for(var index = lastOrderIndex + 1; index < stages.Count; index++) {
            var stage = stages[index];
            if(stage.Kind == PipelineStageKind.AsEnumerable) {
                continue;
            }
            if(stage.Kind == PipelineStageKind.Take) {
                takeCountExpression = $"this.{this.CountName(this.CountOrdinal(index))}Value";
                return true;
            }
            break;
        }
        return false;
    }

    private void AppendTopKOrderFastPath(
        StringBuilder builder,
        string enumeratorName,
        int orderIndex,
        IReadOnlyList<PipelineStage> orderStages,
        string takeCountExpression) {
        var valueType = stages[orderIndex].InputType;
        var primaryOrder = orderStages[0];
        var comparerType = this.OrderTopKComparerName(orderIndex);
        builder.AppendLine($"    if({takeCountExpression}<=64) {{");
        if(orderStages.Count == 1) {
            var keyType = ComparerKeyType(primaryOrder);
            var canDirect = CanUseDirectDefaultOrderCompare(primaryOrder);
            var comparerArg = canDirect ? string.Empty : this.OrderComparerName(this.OrderComparerOrdinal(orderIndex));
            builder.AppendLine($"        var __topKComparer{orderIndex} = new {comparerType}({comparerArg});");
            if(orderIndex == 0) {
                builder.AppendLine($"        if({enumeratorName}.TryGetSpan(out var __topKSpan{orderIndex})) {{");
                builder.AppendLine($"            var __topKCap = {takeCountExpression};");
                builder.AppendLine($"            if(__topKCap <= 0 || __topKSpan{orderIndex}.Length == 0) {{");
                builder.AppendLine($"                this.{this.OrderValuesName(orderIndex)} = global::System.Array.Empty<{valueType}>();");
                builder.AppendLine($"                this.{this.OrderIndexesName(orderIndex)} = global::System.Array.Empty<int>();");
                builder.AppendLine($"                this.{this.OrderCountName(orderIndex)} = 0;");
                builder.AppendLine($"                this.{this.StateName(orderIndex)} = 0;");
                builder.AppendLine("            } else {");
                builder.AppendLine("                global::System.Span<int> __topIndices = stackalloc int[64];");
                builder.AppendLine($"                global::System.Span<{keyType}> __topKeys = stackalloc {keyType}[64];");
                builder.AppendLine("                var __topKCount = 0;");
                builder.AppendLine($"                for(var __i = 0; __i < __topKSpan{orderIndex}.Length; __i++) {{");
                builder.AppendLine($"                    ref readonly var __item = ref __topKSpan{orderIndex}[__i];");
                builder.AppendLine($"                    {keyType} __key = default!;");
                if(primaryOrder.Func is null) {
                    builder.AppendLine("                    __key = __item;");
                } else {
                    builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),"__key","__item"),"                    "));
                }
                builder.AppendLine("                    if(__topKCount < __topKCap) {");
                builder.AppendLine("                        int __pos = __topKCount;");
                builder.AppendLine($"                        while(__pos > 0 && __topKComparer{orderIndex}.Compare(__key, __topKeys[__pos - 1]) < 0) {{");
                builder.AppendLine("                            __topIndices[__pos] = __topIndices[__pos - 1];");
                builder.AppendLine("                            __topKeys[__pos] = __topKeys[__pos - 1];");
                builder.AppendLine("                            __pos--;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        __topIndices[__pos] = __i;");
                builder.AppendLine("                        __topKeys[__pos] = __key;");
                builder.AppendLine("                        __topKCount++;");
                builder.AppendLine("                    } else {");
                builder.AppendLine($"                        if(__topKComparer{orderIndex}.Compare(__key, __topKeys[__topKCap - 1]) >= 0) continue;");
                builder.AppendLine("                        int __pos = __topKCap - 1;");
                builder.AppendLine($"                        while(__pos > 0 && __topKComparer{orderIndex}.Compare(__key, __topKeys[__pos - 1]) < 0) {{");
                builder.AppendLine("                            __topIndices[__pos] = __topIndices[__pos - 1];");
                builder.AppendLine("                            __topKeys[__pos] = __topKeys[__pos - 1];");
                builder.AppendLine("                            __pos--;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        __topIndices[__pos] = __i;");
                builder.AppendLine("                        __topKeys[__pos] = __key;");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine($"                var __topKValues{orderIndex} = global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent(__topKCount);");
                builder.AppendLine($"                var __topKIndexes{orderIndex} = global::System.Buffers.ArrayPool<int>.Shared.Rent(__topKCount);");
                builder.AppendLine("                for(var __i = 0; __i < __topKCount; __i++) {");
                builder.AppendLine($"                    __topKValues{orderIndex}[__i] = __topKSpan{orderIndex}[__topIndices[__i]];");
                builder.AppendLine($"                    __topKIndexes{orderIndex}[__i] = __i;");
                builder.AppendLine("                }");
                builder.AppendLine($"                this.{this.OrderValuesName(orderIndex)} = __topKValues{orderIndex};");
                builder.AppendLine($"                this.{this.OrderIndexesName(orderIndex)} = __topKIndexes{orderIndex};");
                builder.AppendLine($"                this.{this.OrderCountName(orderIndex)} = __topKCount;");
                builder.AppendLine($"                this.{this.StateName(orderIndex)} = 0;");
                builder.AppendLine("            }");
                builder.AppendLine("        } else {");
            }
            builder.AppendLine($"        var __topK{orderIndex} = new global::Seamless.Linq.Internal.TopKBuffer<{valueType},{keyType},{comparerType}>({takeCountExpression},__topKComparer{orderIndex});");
            builder.AppendLine("        try {");
            builder.AppendLine($"            while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"                ","IncrementPhysicalMoveNext");
            var currentName = this.ItemName(0);
            for(var index = 0;index<orderIndex;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index);
            }
            var keyName = this.OrderKeyLocalName(orderIndex,primaryOrder);
            builder.AppendLine($"                {keyType} {keyName} = default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"                {keyName} = {currentName};");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),keyName,currentName),"                "));
            }
            builder.AppendLine($"                __topK{orderIndex}.Push({currentName},{keyName});");
            builder.AppendLine("            }");
            builder.AppendLine($"            var __topKCount{orderIndex} = __topK{orderIndex}.Count;");
            builder.AppendLine($"            var __topKValues{orderIndex} = global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent(__topKCount{orderIndex});");
            builder.AppendLine($"            var __topKIndexes{orderIndex} = global::System.Buffers.ArrayPool<int>.Shared.Rent(__topKCount{orderIndex});");
            builder.AppendLine($"            __topK{orderIndex}.GetSpan().CopyTo(__topKValues{orderIndex});");
            builder.AppendLine($"            for(var __i = 0; __i < __topKCount{orderIndex}; __i++) {{");
            builder.AppendLine($"                __topKIndexes{orderIndex}[__i] = __i;");
            builder.AppendLine("            }");
            builder.AppendLine($"            this.{this.OrderValuesName(orderIndex)} = __topKValues{orderIndex};");
            builder.AppendLine($"            this.{this.OrderIndexesName(orderIndex)} = __topKIndexes{orderIndex};");
            builder.AppendLine($"            this.{this.OrderCountName(orderIndex)} = __topKCount{orderIndex};");
            builder.AppendLine($"            this.{this.StateName(orderIndex)} = 0;");
            builder.AppendLine("        } finally {");
            builder.AppendLine($"            __topK{orderIndex}.Dispose();");
            builder.AppendLine("        }");
            if(orderIndex == 0) {
                builder.AppendLine("        }");
            }
        } else if(orderStages.Count == 2) {
            var secondaryOrder = orderStages[1];
            var primaryKeyType = ComparerKeyType(primaryOrder);
            var secondaryKeyType = ComparerKeyType(secondaryOrder);
            var primaryCanDirect = CanUseDirectDefaultOrderCompare(primaryOrder);
            var secondaryCanDirect = CanUseDirectDefaultOrderCompare(secondaryOrder);
            var ctorArgs = new List<string>();
            if(!primaryCanDirect) ctorArgs.Add(this.OrderComparerName(this.OrderComparerOrdinal(orderIndex)));
            if(!secondaryCanDirect) ctorArgs.Add(this.OrderComparerName(this.OrderComparerOrdinal(this.OrderStageAbsoluteIndex(orderIndex,secondaryOrder))));
            var ctorArgsStr = string.Join(",",ctorArgs);
            builder.AppendLine($"        var __topKComparer{orderIndex} = new {comparerType}({ctorArgsStr});");
            if(orderIndex == 0) {
                builder.AppendLine($"        if({enumeratorName}.TryGetSpan(out var __topKSpan{orderIndex})) {{");
                builder.AppendLine($"            var __topKCap = {takeCountExpression};");
                builder.AppendLine($"            if(__topKCap <= 0 || __topKSpan{orderIndex}.Length == 0) {{");
                builder.AppendLine($"                this.{this.OrderValuesName(orderIndex)} = global::System.Array.Empty<{valueType}>();");
                builder.AppendLine($"                this.{this.OrderIndexesName(orderIndex)} = global::System.Array.Empty<int>();");
                builder.AppendLine($"                this.{this.OrderCountName(orderIndex)} = 0;");
                builder.AppendLine($"                this.{this.StateName(orderIndex)} = 0;");
                builder.AppendLine("            } else {");
                builder.AppendLine("                global::System.Span<int> __topIndices = stackalloc int[64];");
                builder.AppendLine($"                global::System.Span<{primaryKeyType}> __topPKeys = stackalloc {primaryKeyType}[64];");
                builder.AppendLine($"                global::System.Span<{secondaryKeyType}> __topSKeys = stackalloc {secondaryKeyType}[64];");
                builder.AppendLine("                var __topKCount = 0;");
                builder.AppendLine($"                for(var __i = 0; __i < __topKSpan{orderIndex}.Length; __i++) {{");
                builder.AppendLine($"                    ref readonly var __item = ref __topKSpan{orderIndex}[__i];");
                builder.AppendLine($"                    {primaryKeyType} __pkey = default!;");
                if(primaryOrder.Func is null) {
                    builder.AppendLine("                    __pkey = __item;");
                } else {
                    builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),"__pkey","__item"),"                    "));
                }
                builder.AppendLine($"                    {secondaryKeyType} __skey = default!;");
                if(secondaryOrder.Func is null) {
                    builder.AppendLine("                    __skey = __item;");
                } else {
                    builder.AppendLine(IndentBlock(this.FuncAssign(secondaryOrder.Func,secondaryOrder.Kind.ToString(),"__skey","__item"),"                    "));
                }
                builder.AppendLine("                    if(__topKCount < __topKCap) {");
                builder.AppendLine("                        int __pos = __topKCount;");
                builder.AppendLine($"                        while(__pos > 0 && __topKComparer{orderIndex}.Compare(__pkey, __skey, __topPKeys[__pos - 1], __topSKeys[__pos - 1]) < 0) {{");
                builder.AppendLine("                            __topIndices[__pos] = __topIndices[__pos - 1];");
                builder.AppendLine("                            __topPKeys[__pos] = __topPKeys[__pos - 1];");
                builder.AppendLine("                            __topSKeys[__pos] = __topSKeys[__pos - 1];");
                builder.AppendLine("                            __pos--;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        __topIndices[__pos] = __i;");
                builder.AppendLine("                        __topPKeys[__pos] = __pkey;");
                builder.AppendLine("                        __topSKeys[__pos] = __skey;");
                builder.AppendLine("                        __topKCount++;");
                builder.AppendLine("                    } else {");
                builder.AppendLine($"                        if(__topKComparer{orderIndex}.Compare(__pkey, __skey, __topPKeys[__topKCap - 1], __topSKeys[__topKCap - 1]) >= 0) continue;");
                builder.AppendLine("                        int __pos = __topKCap - 1;");
                builder.AppendLine($"                        while(__pos > 0 && __topKComparer{orderIndex}.Compare(__pkey, __skey, __topPKeys[__pos - 1], __topSKeys[__pos - 1]) < 0) {{");
                builder.AppendLine("                            __topIndices[__pos] = __topIndices[__pos - 1];");
                builder.AppendLine("                            __topPKeys[__pos] = __topPKeys[__pos - 1];");
                builder.AppendLine("                            __topSKeys[__pos] = __topSKeys[__pos - 1];");
                builder.AppendLine("                            __pos--;");
                builder.AppendLine("                        }");
                builder.AppendLine("                        __topIndices[__pos] = __i;");
                builder.AppendLine("                        __topPKeys[__pos] = __pkey;");
                builder.AppendLine("                        __topSKeys[__pos] = __skey;");
                builder.AppendLine("                    }");
                builder.AppendLine("                }");
                builder.AppendLine($"                var __topKValues{orderIndex} = global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent(__topKCount);");
                builder.AppendLine($"                var __topKIndexes{orderIndex} = global::System.Buffers.ArrayPool<int>.Shared.Rent(__topKCount);");
                builder.AppendLine("                for(var __i = 0; __i < __topKCount; __i++) {");
                builder.AppendLine($"                    __topKValues{orderIndex}[__i] = __topKSpan{orderIndex}[__topIndices[__i]];");
                builder.AppendLine($"                    __topKIndexes{orderIndex}[__i] = __i;");
                builder.AppendLine("                }");
                builder.AppendLine($"                this.{this.OrderValuesName(orderIndex)} = __topKValues{orderIndex};");
                builder.AppendLine($"                this.{this.OrderIndexesName(orderIndex)} = __topKIndexes{orderIndex};");
                builder.AppendLine($"                this.{this.OrderCountName(orderIndex)} = __topKCount;");
                builder.AppendLine($"                this.{this.StateName(orderIndex)} = 0;");
                builder.AppendLine("            }");
                builder.AppendLine("        } else {");
            }
            builder.AppendLine($"        var __topK{orderIndex} = new global::Seamless.Linq.Internal.TopKCombinedBuffer<{valueType},{primaryKeyType},{secondaryKeyType},{comparerType}>({takeCountExpression},__topKComparer{orderIndex});");
            builder.AppendLine("        try {");
            builder.AppendLine($"            while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"                ","IncrementPhysicalMoveNext");
            var currentName = this.ItemName(0);
            for(var index = 0;index<orderIndex;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index);
            }
            var pKeyName = this.OrderKeyLocalName(orderIndex,primaryOrder);
            builder.AppendLine($"                {primaryKeyType} {pKeyName} = default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"                {pKeyName} = {currentName};");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),pKeyName,currentName),"                "));
            }
            var sKeyName = this.OrderKeyLocalName(orderIndex,secondaryOrder);
            builder.AppendLine($"                {secondaryKeyType} {sKeyName} = default!;");
            if(secondaryOrder.Func is null) {
                builder.AppendLine($"                {sKeyName} = {currentName};");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(secondaryOrder.Func,secondaryOrder.Kind.ToString(),sKeyName,currentName),"                "));
            }
            builder.AppendLine($"                __topK{orderIndex}.Push({currentName},{pKeyName},{sKeyName});");
            builder.AppendLine("            }");
            builder.AppendLine($"            var __topKCount{orderIndex} = __topK{orderIndex}.Count;");
            builder.AppendLine($"            var __topKValues{orderIndex} = global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent(__topKCount{orderIndex});");
            builder.AppendLine($"            var __topKIndexes{orderIndex} = global::System.Buffers.ArrayPool<int>.Shared.Rent(__topKCount{orderIndex});");
            builder.AppendLine($"            __topK{orderIndex}.GetSpan().CopyTo(__topKValues{orderIndex});");
            builder.AppendLine($"            for(var __i = 0; __i < __topKCount{orderIndex}; __i++) {{");
            builder.AppendLine($"                __topKIndexes{orderIndex}[__i] = __i;");
            builder.AppendLine("            }");
            builder.AppendLine($"            this.{this.OrderValuesName(orderIndex)} = __topKValues{orderIndex};");
            builder.AppendLine($"            this.{this.OrderIndexesName(orderIndex)} = __topKIndexes{orderIndex};");
            builder.AppendLine($"            this.{this.OrderCountName(orderIndex)} = __topKCount{orderIndex};");
            builder.AppendLine($"            this.{this.StateName(orderIndex)} = 0;");
            builder.AppendLine("        } finally {");
            builder.AppendLine($"            __topK{orderIndex}.Dispose();");
            builder.AppendLine("        }");
            if(orderIndex == 0) {
                builder.AppendLine("        }");
            }
        }
        builder.AppendLine("    }");
    }
}
