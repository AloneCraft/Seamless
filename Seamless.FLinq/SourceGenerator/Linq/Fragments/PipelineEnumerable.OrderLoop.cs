namespace Seamless.FLinq;

// 並べ替え段のバッファ構築とソート。
internal sealed partial class PipelineEnumerable {
    private string BuildOrderLoop(string enumeratorName,string successStatement,string endStatement,int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        var lastOrderIndex = orderIndex+orderStages.Length-1;
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.OrderValuesName(orderIndex)} is null) {{");
        if(this.CanUseSortedDefaultOrderFastPath(orderIndex,primaryOrder,orderStages)) {
            this.AppendSortedDefaultOrderFastPath(builder,enumeratorName,successStatement,endStatement,orderIndex,lastOrderIndex,primaryOrder);
        }
        builder.AppendLine($"    var {this.OrderInitialCapacityLocalName(orderIndex)}=16;");
        builder.AppendLine($"    if({enumeratorName}.TryGetNonEnumeratedCount(out var __orderSourceCount{orderIndex}) && __orderSourceCount{orderIndex}>16) {{");
        builder.AppendLine($"        {this.OrderInitialCapacityLocalName(orderIndex)}=__orderSourceCount{orderIndex};");
        builder.AppendLine("    }");
        this.AppendOrderBufferBuildStart(builder,orderIndex);
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<orderIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        this.AppendOrderBufferedValue(builder,orderIndex,currentName,"            ");
        builder.AppendLine("        }");
        this.AppendOrderBufferBuildEnd(builder,orderIndex,orderStages);
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

    private void AppendOrderBufferBuildStart(StringBuilder builder,int orderIndex) {
        var valueType = stages[orderIndex].InputType;
        var valuesLocal = this.OrderValuesLocalName(orderIndex);
        var indexesLocal = this.OrderIndexesLocalName(orderIndex);
        var returnValuesLocal = this.OrderReturnValuesLocalName(orderIndex);
        var returnIndexesLocal = this.OrderReturnIndexesLocalName(orderIndex);
        builder.AppendLine($"    var {valuesLocal}=global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent({this.OrderInitialCapacityLocalName(orderIndex)});");
        builder.AppendLine($"    var {indexesLocal}=global::System.Buffers.ArrayPool<int>.Shared.Rent({this.OrderInitialCapacityLocalName(orderIndex)});");
        builder.AppendLine($"    var {returnValuesLocal}=true;");
        builder.AppendLine($"    var {returnIndexesLocal}=true;");
        builder.AppendLine($"    var {this.OrderCountLocalName(orderIndex)}=0;");
        foreach(var orderStage in this.OrderStages(orderIndex)) {
            builder.AppendLine($"    {ComparerKeyType(orderStage)}[]? {this.OrderKeysLocalName(orderIndex,orderStage)}=null;");
        }
        builder.AppendLine("    try{");
    }

    private void AppendOrderBufferBuildEnd(StringBuilder builder,int orderIndex,IReadOnlyList<PipelineStage> orderStages) {
        this.AppendOrderSort(builder,orderIndex,orderStages);
        builder.AppendLine($"        this.{this.OrderValuesName(orderIndex)}={this.OrderValuesLocalName(orderIndex)};");
        builder.AppendLine($"        this.{this.OrderIndexesName(orderIndex)}={this.OrderIndexesLocalName(orderIndex)};");
        builder.AppendLine($"        this.{this.OrderCountName(orderIndex)}={this.OrderCountLocalName(orderIndex)};");
        builder.AppendLine($"        this.{this.StateName(orderIndex)}=0;");
        builder.AppendLine($"        {this.OrderReturnValuesLocalName(orderIndex)}=false;");
        builder.AppendLine($"        {this.OrderReturnIndexesLocalName(orderIndex)}=false;");
        builder.AppendLine("    }finally{");
        foreach(var orderStage in orderStages) {
            var keysLocal = this.OrderKeysLocalName(orderIndex,orderStage);
            builder.AppendLine($"        if({keysLocal} is not null) {{");
            builder.AppendLine($"            global::System.Buffers.ArrayPool<{ComparerKeyType(orderStage)}>.Shared.Return({keysLocal},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{ComparerKeyType(orderStage)}>());");
            builder.AppendLine("        }");
        }
        builder.AppendLine($"        if({this.OrderReturnIndexesLocalName(orderIndex)}) {{");
        builder.AppendLine($"            global::System.Buffers.ArrayPool<int>.Shared.Return({this.OrderIndexesLocalName(orderIndex)},clearArray:false);");
        builder.AppendLine("        }");
        builder.AppendLine($"        if({this.OrderReturnValuesLocalName(orderIndex)}) {{");
        builder.AppendLine($"            global::System.Buffers.ArrayPool<{stages[orderIndex].InputType}>.Shared.Return({this.OrderValuesLocalName(orderIndex)},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{stages[orderIndex].InputType}>());");
        builder.AppendLine("        }");
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
        builder.AppendLine($"        if({countLocal}>0){{");
        if(this.CanUseDirectValueOrderSort(orderStages)) {
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
            var indent = this.CanUseDirectValueOrderSort(orderStages) ? "                " : "            ";
            builder.AppendLine($"{indent}{this.OrderKeysLocalName(orderIndex,orderStage)}=global::System.Buffers.ArrayPool<{ComparerKeyType(orderStage)}>.Shared.Rent({countLocal});");
        }
        var fallbackIndent = this.CanUseDirectValueOrderSort(orderStages) ? "                " : "            ";
        builder.AppendLine($"{fallbackIndent}for(var __orderKeyIndex{orderIndex}=0;__orderKeyIndex{orderIndex}<{countLocal};__orderKeyIndex{orderIndex}++){{");
        builder.AppendLine($"{fallbackIndent}    {indexesLocal}[__orderKeyIndex{orderIndex}]=__orderKeyIndex{orderIndex};");
        builder.AppendLine($"{fallbackIndent}    var {valueName}={valuesLocal}[__orderKeyIndex{orderIndex}];");
        foreach(var orderStage in orderStages) {
            var keyName = this.OrderKeyLocalName(orderIndex,orderStage);
            builder.AppendLine($"{fallbackIndent}    {ComparerKeyType(orderStage)} {keyName}=default!;");
            if(orderStage.Func is null) {
                builder.AppendLine($"{fallbackIndent}    {keyName}={valueName};");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(orderStage.Func,orderStage.Kind.ToString(),keyName,valueName),fallbackIndent+"    "));
            }
            builder.AppendLine($"{fallbackIndent}    {this.OrderKeysLocalName(orderIndex,orderStage)}![__orderKeyIndex{orderIndex}]={keyName};");
        }
        builder.AppendLine($"{fallbackIndent}}}");
        var comparerArgs = orderStages
            .SelectMany(orderStage => {
                var absoluteIndex = this.OrderStageAbsoluteIndex(orderIndex,orderStage);
                var args = new List<string> { $"{this.OrderKeysLocalName(orderIndex,orderStage)}!" };
                if(!CanUseDirectDefaultOrderCompare(orderStage)) {
                    args.Add(this.OrderComparerName(this.OrderComparerOrdinal(absoluteIndex)));
                }
                return args;
            });
        builder.AppendLine($"{fallbackIndent}global::Seamless.Linq.GeneratedCodeHelpers.SortIndexes({indexesLocal},{countLocal},new {this.OrderIndexComparerName(orderIndex)}({string.Join(",",comparerArgs)}));");
        if(this.CanUseDirectValueOrderSort(orderStages)) {
            builder.AppendLine("            }");
        }
        builder.AppendLine("        }");
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
}
