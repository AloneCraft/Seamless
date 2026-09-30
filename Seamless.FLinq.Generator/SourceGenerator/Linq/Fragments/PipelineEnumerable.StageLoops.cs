namespace Seamless.FLinq.Generator;

// 段ごとのループ本体（GroupBy/Reverse/SelectMany/集合演算など）。
internal sealed partial class PipelineEnumerable {
    private string BuildGroupByLoop(string enumeratorName,string successStatement,string endStatement,int groupByIndex) {
        var stage = stages[groupByIndex];
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.GroupItemsName(groupByIndex)} is null) {{");
        builder.AppendLine($"    var {this.GroupItemsName(groupByIndex)}=new global::System.Collections.Generic.List<{this.GroupValueType(groupByIndex,stage)}>();");
        builder.AppendLine($"    var __groupMap=new global::System.Collections.Generic.Dictionary<{ComparerKeyType(stage)},{this.GroupingName(groupByIndex)}>({this.ComparerName(this.ComparerOrdinal(groupByIndex))});");
        builder.AppendLine($"    var __groupOrder=new global::System.Collections.Generic.List<{this.GroupingName(groupByIndex)}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<groupByIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var keyName = this.KeyName(groupByIndex);
        builder.AppendLine($"        {ComparerKeyType(stage)} {keyName}=default!;");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),"        "));
        var groupElementName = this.ItemName(groupByIndex+1);
        builder.AppendLine($"        {stage.InnerElementType} {groupElementName}=default!;");
        if(stage.SecondFunc is { } elementSelector) {
            builder.AppendLine(IndentBlock(this.FuncAssign(elementSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),"        "));
        } else {
            builder.AppendLine($"        {groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
        }
        builder.AppendLine($"        if(!__groupMap.TryGetValue({keyName},out var {this.GroupName(groupByIndex)})) {{");
        builder.AppendLine($"            {this.GroupName(groupByIndex)}=new {this.GroupingName(groupByIndex)}({keyName});");
        builder.AppendLine($"            __groupMap.Add({keyName},{this.GroupName(groupByIndex)});");
        builder.AppendLine($"            __groupOrder.Add({this.GroupName(groupByIndex)});");
        if(stage.ThirdFunc is null) {
            builder.AppendLine($"            {this.GroupItemsName(groupByIndex)}.Add({this.GroupName(groupByIndex)});");
        }
        builder.AppendLine("        }");
        builder.AppendLine($"        {this.GroupName(groupByIndex)}.Add({groupElementName});");
        builder.AppendLine("    }");
        if(stage.ThirdFunc is { } resultSelector) {
            builder.AppendLine($"    foreach(var {this.GroupName(groupByIndex)} in __groupOrder) {{");
            var resultName = this.ItemName(groupByIndex+2);
            builder.AppendLine($"        {stage.OutputType} {resultName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(resultSelector,nameof(PipelineStageKind.GroupBy),resultName,$"{this.GroupName(groupByIndex)}.Key",$"(global::System.Collections.Generic.IEnumerable<{stage.InnerElementType}>){this.GroupName(groupByIndex)}"),"        "));
            builder.AppendLine($"        {this.GroupItemsName(groupByIndex)}.Add({resultName});");
            builder.AppendLine("    }");
        }
        // GroupBy バッファ収集後、後続に OrderBy/ThenBy があればソートして融合する。
        // 順序段の並びと間に置ける段は PipelineBuilder.CanFuseGroupByThenOrder が保証する(順序段は GroupBy より後で連続し、間は Where だけ。OrderBy は 1 つ)。
        var orderAfterGroup = -1;
        for(var checkIdx = groupByIndex+1;checkIdx<stages.Count;checkIdx++) {
            if(stages[checkIdx].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                orderAfterGroup=checkIdx;
            }
        }
        if(orderAfterGroup>=0) {
            var keyStageIndexes = new List<int> { orderAfterGroup };
            for(var thenIdx = orderAfterGroup+1;thenIdx<stages.Count&&stages[thenIdx].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending;thenIdx++) {
                keyStageIndexes.Add(thenIdx);
            }
            foreach(var keyStageIndex in keyStageIndexes) {
                var keyStage = stages[keyStageIndex];
                builder.AppendLine($"    var {this.OrderComparerLocalName(keyStageIndex)}={this.OrderComparerName(this.OrderComparerOrdinal(keyStageIndex))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(keyStage)}>.Default;");
            }
            // List.Sort は不安定なので、キーが等しいグループは出現順(元の位置)で並べる。LINQ の OrderBy は安定ソート。
            var groupItems = this.GroupItemsName(groupByIndex);
            builder.AppendLine($"    var __gsrc={groupItems};");
            builder.AppendLine("    var __gidx=new int[__gsrc.Count];");
            builder.AppendLine("    for(var __gi=0;__gi<__gidx.Length;__gi++) __gidx[__gi]=__gi;");
            builder.AppendLine("    global::System.Array.Sort(__gidx,(__ga,__gb) => {");
            builder.AppendLine("        var left=__gsrc[__ga];");
            builder.AppendLine("        var right=__gsrc[__gb];");
            for(var keyOrdinal = 0;keyOrdinal<keyStageIndexes.Count;keyOrdinal++) {
                var keyStageIndex = keyStageIndexes[keyOrdinal];
                var keyStage = stages[keyStageIndex];
                var keyCmpLocal = this.OrderComparerLocalName(keyStageIndex);
                var leftKey = $"__lk{keyStageIndex}";
                var rightKey = $"__rk{keyStageIndex}";
                var cmpName = keyOrdinal==0 ? "__gcmp" : $"__gcmp{keyOrdinal+1}";
                builder.AppendLine($"        {ComparerKeyType(keyStage)} {leftKey}=default!;");
                if(keyStage.Func is null) {
                    builder.AppendLine($"        {leftKey}=left;");
                } else {
                    builder.AppendLine(IndentBlock(this.FuncAssign(keyStage.Func,keyStage.Kind.ToString(),leftKey,"left"),"        "));
                }
                builder.AppendLine($"        {ComparerKeyType(keyStage)} {rightKey}=default!;");
                if(keyStage.Func is null) {
                    builder.AppendLine($"        {rightKey}=right;");
                } else {
                    builder.AppendLine(IndentBlock(this.FuncAssign(keyStage.Func,keyStage.Kind.ToString(),rightKey,"right"),"        "));
                }
                builder.AppendLine($"        var {cmpName}={keyCmpLocal}.Compare({leftKey},{rightKey});");
                builder.AppendLine($"        if({cmpName}!=0) return {this.OrderCompareExpression(keyStage,cmpName)};");
            }
            builder.AppendLine("        return __ga.CompareTo(__gb);");
            builder.AppendLine("    });");
            builder.AppendLine($"    var __gsorted=new global::System.Collections.Generic.List<{this.GroupValueType(groupByIndex,stage)}>(__gsrc.Count);");
            builder.AppendLine("    foreach(var __gi in __gidx) __gsorted.Add(__gsrc[__gi]);");
            builder.AppendLine($"    {groupItems}=__gsorted;");
        }
        builder.AppendLine($"    this.{this.GroupItemsName(groupByIndex)}={this.GroupItemsName(groupByIndex)};");
        builder.AppendLine($"    this.{this.StateName(groupByIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(groupByIndex)}<this.{this.GroupItemsName(groupByIndex)}.Count) {{");
        // stages.Count+1 は任意の ItemName(index+1) より大きい値なので OrderBy が間にある場合でも名前衝突しない
        currentName=this.ItemName(stages.Count+1);
        builder.AppendLine($"    var {currentName}=this.{this.GroupItemsName(groupByIndex)}[this.{this.StateName(groupByIndex)}++];");
        for(var index = groupByIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildReverseLoop(string enumeratorName,string successStatement,string endStatement,int reverseIndex) {
        var stage = stages[reverseIndex];
        var elementType = stage.OutputType;
        var fieldName = this.ReverseItemsName(reverseIndex);
        var countField = this.BufferCountName(reverseIndex);
        var suffix = $"{this.item}_{reverseIndex}";
        var returnLocal = $"__bufReturn{suffix}";
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{fieldName} is null) {{");
        builder.AppendLine(IndentBlock(PooledBufferSupport.BuildStart(enumeratorName,elementType,fieldName,countField,returnLocal,suffix),"    "));
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<reverseIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(PooledBufferSupport.Add(elementType,fieldName,countField,currentName,suffix),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        global::System.Array.Reverse({fieldName},0,{countField});");
        builder.AppendLine(IndentBlock(PooledBufferSupport.Commit(fieldName,countField),"        "));
        builder.AppendLine($"        this.{this.StateName(reverseIndex)}=0;");
        builder.AppendLine(IndentBlock(PooledBufferSupport.Finally(elementType,fieldName,returnLocal),"    "));
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(reverseIndex)}<this.{countField}) {{");
        currentName=this.ItemName(reverseIndex+1);
        builder.AppendLine($"    var {currentName}=this.{fieldName}[this.{this.StateName(reverseIndex)}++];");
        for(var index = reverseIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildSkipLastLoop(string enumeratorName,string successStatement,string endStatement,int skipLastIndex) {
        var builder = new StringBuilder();
        var countOrd = this.CountOrdinal(skipLastIndex);
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<skipLastIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        // 初回だけ ArrayPool から借りる。容量は捨てる件数+1 で確定するので成長は起きない。
        builder.AppendLine($"    if(!this.{this.StateName(skipLastIndex)}) {{");
        builder.AppendLine($"        this.{this.QueueName(skipLastIndex)}=new global::Seamless.Linq.ValueQueue<{stages[skipLastIndex].OutputType}>(global::System.Math.Max(0,{this.CountName(countOrd)})+1);");
        builder.AppendLine($"        this.{this.StateName(skipLastIndex)}=true;");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{this.QueueName(skipLastIndex)}.Enqueue({currentName});");
        builder.AppendLine($"    if(this.{this.QueueName(skipLastIndex)}.Count > global::System.Math.Max(0,{this.CountName(countOrd)})) {{");
        var dequeueItem = this.ItemName(skipLastIndex+1);
        builder.AppendLine($"        var {dequeueItem}=this.{this.QueueName(skipLastIndex)}.Dequeue();");
        currentName=dequeueItem;
        for(var index = skipLastIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildTakeLastLoop(string enumeratorName,string successStatement,string endStatement,int takeLastIndex) {
        var stage = stages[takeLastIndex];
        var countOrd = this.CountOrdinal(takeLastIndex);
        var elementType = stage.OutputType;
        var fieldName = this.ReverseItemsName(takeLastIndex);
        var countField = this.BufferCountName(takeLastIndex);
        var suffix = $"{this.item}_{takeLastIndex}";
        var returnLocal = $"__bufReturn{suffix}";
        var takeNLocal = $"__takeN{suffix}";
        var takeStartLocal = $"__takeStart{suffix}";
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{fieldName} is null) {{");
        builder.AppendLine(IndentBlock(PooledBufferSupport.BuildStart(enumeratorName,elementType,fieldName,countField,returnLocal,suffix),"    "));
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<takeLastIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(PooledBufferSupport.Add(elementType,fieldName,countField,currentName,suffix),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        var {takeNLocal}=global::System.Math.Max(0,global::System.Math.Min(global::System.Math.Max(0,{this.CountName(countOrd)}),{countField}));");
        builder.AppendLine($"        var {takeStartLocal}={countField}-{takeNLocal};");
        builder.AppendLine(IndentBlock(PooledBufferSupport.Commit(fieldName,countField),"        "));
        builder.AppendLine($"        this.{this.StateName(takeLastIndex)}={takeStartLocal};");
        builder.AppendLine(IndentBlock(PooledBufferSupport.Finally(elementType,fieldName,returnLocal),"    "));
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(takeLastIndex)}<this.{countField}) {{");
        currentName=this.ItemName(takeLastIndex+1);
        builder.AppendLine($"    var {currentName}=this.{fieldName}[this.{this.StateName(takeLastIndex)}++];");
        for(var index = takeLastIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildTakeRangeLoop(string enumeratorName,string successStatement,string endStatement,int takeRangeIndex) {
        var stage = stages[takeRangeIndex];
        var rangeParamName = this.RangeName(this.RangeOrdinal(takeRangeIndex));
        var elementType = stage.OutputType;
        var fieldName = this.ReverseItemsName(takeRangeIndex);
        var countField = this.BufferCountName(takeRangeIndex);
        var suffix = $"{this.item}_{takeRangeIndex}";
        var returnLocal = $"__bufReturn{suffix}";
        var rangeOffLocal = $"__rangeOff{suffix}";
        var rangeLenLocal = $"__rangeLen{suffix}";
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{fieldName} is null) {{");
        builder.AppendLine(IndentBlock(PooledBufferSupport.BuildStart(enumeratorName,elementType,fieldName,countField,returnLocal,suffix),"    "));
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<takeRangeIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(PooledBufferSupport.Add(elementType,fieldName,countField,currentName,suffix),"            "));
        builder.AppendLine("        }");
        // Range.GetOffsetAndLength は範囲が件数を超えると例外だが、System.Linq の Take(Range) は [0, 件数] へ切り詰める。
        builder.AppendLine($"        var ({rangeOffLocal},{rangeLenLocal})=global::Seamless.Linq.GeneratedCodeHelpers.GetClampedOffsetAndLength({rangeParamName},{countField});");
        builder.AppendLine(IndentBlock(PooledBufferSupport.Commit(fieldName,countField),"        "));
        builder.AppendLine($"        this.{this.StateName(takeRangeIndex)}={rangeOffLocal};");
        builder.AppendLine($"        this.{this.StateEndName(takeRangeIndex)}={rangeOffLocal}+{rangeLenLocal};");
        builder.AppendLine(IndentBlock(PooledBufferSupport.Finally(elementType,fieldName,returnLocal),"    "));
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(takeRangeIndex)}<this.{this.StateEndName(takeRangeIndex)}) {{");
        currentName=this.ItemName(takeRangeIndex+1);
        builder.AppendLine($"    var {currentName}=this.{fieldName}[this.{this.StateName(takeRangeIndex)}++];");
        for(var index = takeRangeIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildDefaultIfEmptyLoop(string enumeratorName,string successStatement,string endStatement,int defaultIfEmptyIndex) {
        var stage = stages[defaultIfEmptyIndex];
        var builder = new StringBuilder();
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<defaultIfEmptyIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"    this.{this.StateName(defaultIfEmptyIndex)}=true;");
        for(var index = defaultIfEmptyIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine($"while(!this.{this.StateName(defaultIfEmptyIndex)}) {{");
        builder.AppendLine($"    this.{this.StateName(defaultIfEmptyIndex)}=true;");
        currentName=this.ItemName(defaultIfEmptyIndex+1);
        builder.AppendLine($"    {stage.OutputType} {currentName}={stage.Value};");
        for(var index = defaultIfEmptyIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildBinarySetOrderLoop(string enumeratorName,string successStatement,string endStatement,int setIndex,int orderIndex) {
        var stage = stages[setIndex];
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var lastOrderIndex = orderIndex+orderStages.Length-1;
        var builder = new StringBuilder();
        var endLabel = this.EndLabel(orderIndex);
        var needsEndLabel = this.StagesBetweenNeedEndLabel(setIndex,orderIndex);
        var breakStatement = needsEndLabel ? $"goto {endLabel};" : "break;";
        builder.AppendLine($"if(this.{this.OrderValuesName(orderIndex)} is null) {{");
        builder.AppendLine($"    var {this.OrderInitialCapacityLocalName(orderIndex)}=16;");
        this.AppendOrderBufferBuildStart(builder,orderIndex);
        if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
            this.AppendUnionOrderMaterialization(builder,enumeratorName,setIndex,orderIndex,breakStatement);
        } else {
            this.AppendExceptIntersectOrderMaterialization(builder,enumeratorName,setIndex,orderIndex,breakStatement);
        }
        if(needsEndLabel) {
            builder.AppendLine($"        {endLabel}:");
        }
        this.AppendOrderBufferBuildEnd(builder,orderIndex,orderStages);
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(orderIndex)}<this.{this.OrderCountName(orderIndex)}) {{");
        var sortedCurrentName = this.ItemName(lastOrderIndex+1);
        builder.AppendLine($"    var {sortedCurrentName}=this.{this.OrderValuesName(orderIndex)}[this.{this.OrderIndexesName(orderIndex)}[this.{this.StateName(orderIndex)}++]];");
        var currentName = sortedCurrentName;
        for(var index = lastOrderIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private void AppendUnionOrderMaterialization(StringBuilder builder,string enumeratorName,int unionIndex,int orderIndex,string breakStatement) {
        var stage = stages[unionIndex];
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<unionIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var firstKeyName = currentName;
        if(stage.Kind==PipelineStageKind.UnionBy) {
            firstKeyName=this.KeyName(unionIndex);
            builder.AppendLine($"            {ComparerKeyType(stage)} {firstKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.UnionBy),firstKeyName,currentName),"            "));
        }
        builder.AppendLine($"            if(!this.{this.SetName(unionIndex)}.Add({firstKeyName})) continue;");
        this.AppendBinarySetOrderTail(builder,unionIndex,orderIndex,currentName,breakStatement,"            ");
        builder.AppendLine("        }");
        builder.AppendLine($"        while({this.SecondEnumeratorName(this.SecondOrdinal(unionIndex))}.TryGetNext(out var {this.ItemName(unionIndex+1)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var secondCurrentName = this.ItemName(unionIndex+1);
        builder.AppendLine($"            {secondCurrentName}=({stage.OutputType})(object?){secondCurrentName}!;");
        var secondKeyName = secondCurrentName;
        if(stage.Kind==PipelineStageKind.UnionBy) {
            secondKeyName=this.KeyName(unionIndex)+"_second"; // KeyName(unionIndex+1) は次の段(DistinctBy など)の key 変数と同じ名前になり、重複定義(CS0128)になる
            builder.AppendLine($"            {ComparerKeyType(stage)} {secondKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.UnionBy),secondKeyName,secondCurrentName),"            "));
        }
        builder.AppendLine($"            if(!this.{this.SetName(unionIndex)}.Add({secondKeyName})) continue;");
        this.AppendBinarySetOrderTail(builder,unionIndex,orderIndex,secondCurrentName,breakStatement,"            ");
        builder.AppendLine("        }");
    }

    private void AppendExceptIntersectOrderMaterialization(StringBuilder builder,string enumeratorName,int setIndex,int orderIndex,string breakStatement) {
        var stage = stages[setIndex];
        builder.AppendLine($"        while({this.SecondEnumeratorName(this.SecondOrdinal(setIndex))}.TryGetNext(out var {this.ItemName(setIndex+1)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        builder.AppendLine($"            this.{this.SecondSetName(setIndex)}.Add(({ComparerKeyType(stage)})(object?){this.ItemName(setIndex+1)}!);");
        builder.AppendLine("        }");
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<setIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var setKeyName = currentName;
        if(stage.Kind is PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
            setKeyName=this.KeyName(setIndex);
            builder.AppendLine($"            {ComparerKeyType(stage)} {setKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,stage.Kind.ToString(),setKeyName,currentName),"            "));
        }
        if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
            builder.AppendLine($"            if(this.{this.SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{this.SetName(setIndex)}.Add({setKeyName})) continue;");
        } else {
            builder.AppendLine($"            if(!this.{this.SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{this.SetName(setIndex)}.Add({setKeyName})) continue;");
        }
        this.AppendBinarySetOrderTail(builder,setIndex,orderIndex,currentName,breakStatement,"            ");
        builder.AppendLine("        }");
    }

    private void AppendBinarySetOrderTail(StringBuilder builder,int setIndex,int orderIndex,string currentName,string breakStatement,string indent) {
        for(var index = setIndex+1;index<orderIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
        }
        this.AppendOrderBufferedValue(builder,orderIndex,currentName,indent);
    }

    private string BuildBinarySetLoop(string enumeratorName,string successStatement,string endStatement,int setIndex) {
        var stage = stages[setIndex];
        if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
            return this.BuildUnionLoop(enumeratorName,successStatement,endStatement,setIndex);
        }

        var builder = new StringBuilder();
        builder.AppendLine($"if(!this.{this.StateName(setIndex)}) {{");
        builder.AppendLine($"    while({this.SecondEnumeratorName(this.SecondOrdinal(setIndex))}.TryGetNext(out var {this.ItemName(setIndex+1)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        builder.AppendLine($"        this.{this.SecondSetName(setIndex)}.Add(({ComparerKeyType(stage)})(object?){this.ItemName(setIndex+1)}!);");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{this.StateName(setIndex)}=true;");
        builder.AppendLine("}");
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<setIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var setKeyName = currentName;
        if(stage.Kind is PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
            setKeyName=this.KeyName(setIndex);
            builder.AppendLine($"    {ComparerKeyType(stage)} {setKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,stage.Kind.ToString(),setKeyName,currentName),"    "));
        }
        if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy) {
            builder.AppendLine($"    if(this.{this.SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{this.SetName(setIndex)}.Add({setKeyName})) continue;");
        } else {
            builder.AppendLine($"    if(!this.{this.SecondSetName(setIndex)}.Contains({setKeyName}) || !this.{this.SetName(setIndex)}.Add({setKeyName})) continue;");
        }
        for(var index = setIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildUnionLoop(string enumeratorName,string successStatement,string endStatement,int unionIndex) {
        var builder = new StringBuilder();
        var endLabel = this.EndLabel(unionIndex);
        var needsEndLabel = this.StagesAfterNeedEndLabel(unionIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        builder.AppendLine($"    if(!this.{this.StateName(unionIndex)}) {{");
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<unionIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        var firstKeyName = currentName;
        if(stages[unionIndex].Kind==PipelineStageKind.UnionBy) {
            firstKeyName=this.KeyName(unionIndex);
            builder.AppendLine($"            {ComparerKeyType(stages[unionIndex])} {firstKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stages[unionIndex].Func!,nameof(PipelineStageKind.UnionBy),firstKeyName,currentName),"            "));
        }
        builder.AppendLine($"            if(!this.{this.SetName(unionIndex)}.Add({firstKeyName})) continue;");
        for(var index = unionIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{this.StateName(unionIndex)}=true;");
        builder.AppendLine("    }");
        builder.AppendLine($"    while({this.SecondEnumeratorName(this.SecondOrdinal(unionIndex))}.TryGetNext(out var {this.ItemName(unionIndex+1)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var secondCurrentName = this.ItemName(unionIndex+1);
        builder.AppendLine($"        {secondCurrentName}=({stages[unionIndex].OutputType})(object?){secondCurrentName}!;");
        var secondKeyName = secondCurrentName;
        if(stages[unionIndex].Kind==PipelineStageKind.UnionBy) {
            secondKeyName=this.KeyName(unionIndex)+"_second"; // KeyName(unionIndex+1) は次の段(DistinctBy など)の key 変数と同じ名前になり、重複定義(CS0128)になる
            builder.AppendLine($"        {ComparerKeyType(stages[unionIndex])} {secondKeyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stages[unionIndex].Func!,nameof(PipelineStageKind.UnionBy),secondKeyName,secondCurrentName),"        "));
        }
        builder.AppendLine($"        if(!this.{this.SetName(unionIndex)}.Add({secondKeyName})) continue;");
        currentName=secondCurrentName;
        for(var index = unionIndex+1;index<stages.Count;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
        builder.AppendLine("    }");
        if(needsEndLabel) {
            builder.AppendLine($"    {endLabel}:");
        }
        builder.AppendLine(IndentBlock(endStatement,"    "));
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string BuildSelectManyLoop(string enumeratorName,string successStatement,string endStatement,int selectManyIndex) {
        var builder = new StringBuilder();
        var selectManyStage = stages[selectManyIndex];
        var endLabel = this.EndLabel(selectManyIndex);
        var needsEndLabel = this.StagesAfterNeedEndLabel(selectManyIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        var innerItemName = this.ItemName(selectManyIndex+1);
        builder.AppendLine("while(true) {");
        if(selectManyStage.InnerIsArray) {
            // 内側シーケンスが配列の場合は IEnumerator<T> を介さず、インデックスで直接走査する。
            var arrayName = this.InnerArrayName(selectManyIndex);
            var indexName = this.InnerIndexName(selectManyIndex);
            var hasValueName = this.InnerHasValueName(selectManyIndex);
            builder.AppendLine($"    if(this.{hasValueName}) {{");
            builder.AppendLine($"        if(this.{indexName} < this.{arrayName}.Length) {{");
            builder.AppendLine($"            var {innerItemName}=this.{arrayName}[this.{indexName}];");
            builder.AppendLine($"            this.{indexName}++;");
            this.AppendSelectManyTail(builder,selectManyStage,selectManyIndex,innerItemName,breakStatement,successStatement,"            ");
            builder.AppendLine("        }");
            builder.AppendLine($"        this.{hasValueName}=false;");
            builder.AppendLine("    }");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var outerCurrentName0 = this.AppendSelectManyHead(builder,selectManyStage,selectManyIndex,this.ItemName(0));
            var collectionExpression0 = this.SelectManyCollectionExpression(selectManyStage,selectManyIndex,outerCurrentName0);
            builder.AppendLine($"        this.{arrayName}={collectionExpression0};");
            builder.AppendLine($"        this.{indexName}=0;");
            builder.AppendLine($"        this.{hasValueName}=true;");
            builder.AppendLine("        break;");
            builder.AppendLine("    }");
            builder.AppendLine($"    if(!this.{hasValueName}) {{");
            builder.AppendLine(IndentBlock(endStatement,"        "));
            builder.AppendLine("    }");
        } else {
            var innerEnumeratorName = this.InnerEnumeratorName(selectManyIndex);
            builder.AppendLine($"    if(this.{innerEnumeratorName} is not null) {{");
            builder.AppendLine($"        if(this.{innerEnumeratorName}.MoveNext()) {{");
            builder.AppendLine($"            var {innerItemName}=this.{innerEnumeratorName}.Current;");
            this.AppendSelectManyTail(builder,selectManyStage,selectManyIndex,innerItemName,breakStatement,successStatement,"            ");
            builder.AppendLine("        }");
            builder.AppendLine($"        this.{innerEnumeratorName}.Dispose();");
            builder.AppendLine($"        this.{innerEnumeratorName}=null;");
            builder.AppendLine("    }");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var outerCurrentName = this.AppendSelectManyHead(builder,selectManyStage,selectManyIndex,this.ItemName(0));
            var collectionExpression = this.SelectManyCollectionExpression(selectManyStage,selectManyIndex,outerCurrentName);
            builder.AppendLine($"        this.{innerEnumeratorName}=((global::System.Collections.Generic.IEnumerable<{selectManyStage.InnerElementType}>){collectionExpression}).GetEnumerator();");
            builder.AppendLine("        break;");
            builder.AppendLine("    }");
            builder.AppendLine($"    if(this.{innerEnumeratorName} is null) {{");
            builder.AppendLine(IndentBlock(endStatement,"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        if(needsEndLabel) {
            builder.AppendLine($"{endLabel}:");
            builder.AppendLine(endStatement);
        }
        return builder.ToString().TrimEnd();
    }

    // SelectMany の内側要素を取得した後、resultSelector の適用と後続ステージの処理・成功文の出力までを行う。
    private string AppendSelectManyTail(StringBuilder builder,PipelineStage selectManyStage,int selectManyIndex,string innerItemName,string breakStatement,string successStatement,string indent) {
        this.AppendCounterCall(builder,indent,"IncrementLogicalSelectManyInner");
        var currentName = innerItemName;
        if(selectManyStage.SecondFunc is { } resultSelector) {
            // resultSelector の出力は専用名にする。後続 Select 段は配列 index(=selectManyIndex+1)から
            // ItemName(selectManyIndex+2) を出力に使うため、ItemName(selectManyIndex+2) を流用すると衝突する。
            currentName=this.SelectManyResultName(selectManyIndex);
            builder.AppendLine(IndentBlock($"{selectManyStage.OutputType} {currentName}=default!;",indent));
            builder.AppendLine(IndentBlock(this.FuncAssign(resultSelector,nameof(PipelineStageKind.SelectMany),currentName,this.OuterName(selectManyIndex),innerItemName),indent));
        }
        for(var index = selectManyIndex+1;index<stages.Count;index++) {
            var stage = stages[index];
            currentName=this.AppendStage(builder,stage,currentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),indent));
        return currentName;
    }

    // SelectMany より前段のステージを適用し、outerValue フィールドへの代入とインデックス更新までを行う。
    private string AppendSelectManyHead(StringBuilder builder,PipelineStage selectManyStage,int selectManyIndex,string outerCurrentName) {
        var currentName = outerCurrentName;
        for(var index = 0;index<selectManyIndex;index++) {
            var stage = stages[index];
            currentName=this.AppendStage(builder,stage,currentName,index);
        }
        var outerName = this.OuterName(selectManyIndex);
        this.AppendCounterCall(builder,"        ","IncrementLogicalSelectManyOuter");
        builder.AppendLine($"        this.{outerName}={currentName};");
        if(selectManyStage.UsesIndex) {
            builder.AppendLine($"        this.{this.StateName(selectManyIndex)}++;");
        }
        return currentName;
    }

    private string SelectManyCollectionExpression(PipelineStage selectManyStage,int selectManyIndex,string currentName) {
        string[] selectorArguments;
        if(selectManyStage.UsesIndex) {
            selectorArguments=new[] { currentName,$"this.{this.StateName(selectManyIndex)}" };
        } else {
            selectorArguments=new[] { currentName };
        }
        return this.FuncInvoke(selectManyStage.Func!,nameof(PipelineStageKind.SelectMany),selectorArguments);
    }

    private string AppendStage(StringBuilder builder,PipelineStage stage,string currentName,int index,string breakStatement = "break;") {
        switch(stage.Kind) {
            case PipelineStageKind.Where:
                this.AppendWhere(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Select:
                return this.AppendSelect(builder,stage,currentName,index);
            case PipelineStageKind.Take:
                this.AppendTake(builder,index,this.CountOrdinal(index),breakStatement);
                return currentName;
            case PipelineStageKind.Skip:
                this.AppendSkip(builder,index,this.CountOrdinal(index));
                return currentName;
            case PipelineStageKind.TakeWhile:
                this.AppendTakeWhile(builder,stage,currentName,index,breakStatement);
                return currentName;
            case PipelineStageKind.SkipWhile:
                this.AppendSkipWhile(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Cast:
                return this.AppendCast(builder,stage,currentName,index);
            case PipelineStageKind.OfType:
                return this.AppendOfType(builder,stage,currentName,index);
            case PipelineStageKind.Distinct:
                this.AppendDistinct(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.DistinctBy:
                this.AppendDistinctBy(builder,stage,currentName,index);
                return currentName;
            case PipelineStageKind.Union:
            case PipelineStageKind.Except:
            case PipelineStageKind.Intersect:
            case PipelineStageKind.UnionBy:
            case PipelineStageKind.ExceptBy:
            case PipelineStageKind.IntersectBy:
                return currentName;
            case PipelineStageKind.Concat:
            case PipelineStageKind.Append:
            case PipelineStageKind.Prepend:
                return currentName;
            case PipelineStageKind.OrderBy:
            case PipelineStageKind.OrderByDescending:
            case PipelineStageKind.ThenBy:
            case PipelineStageKind.ThenByDescending:
                return currentName;
            case PipelineStageKind.GroupBy:
                return currentName;
            case PipelineStageKind.Reverse:
                return currentName;
            case PipelineStageKind.AsEnumerable:
                return currentName;
            default:
                throw new NotSupportedException($"Unsupported nested pipeline stage: {stage.Kind}.");
        }
    }

    private void AppendWhere(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{this.StateName(index)}++;");
            arguments=new[] { currentName,$"this.{this.StateName(index)}" };
        } else {
            arguments=new[] { currentName };
        }
        this.AppendCounterCall(builder,"    ","IncrementLogicalWhere");
        var (preCode,condExpr)=this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.Where),this.TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"    "));
        }
        builder.AppendLine($"    if(!{WrapConditionExpression(condExpr)}) continue;");
    }

    private string AppendSelect(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = this.ItemName(index+1);
        builder.AppendLine($"    {stage.OutputType} {nextName}=default!;");
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{this.StateName(index)}++;");
            arguments=new[] { currentName,$"this.{this.StateName(index)}" };
        } else {
            arguments=new[] { currentName };
        }
        this.AppendCounterCall(builder,"    ","IncrementLogicalSelect");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.Select),nextName,arguments),"    "));
        return nextName;
    }

    private void AppendTake(StringBuilder builder,int index,int countIndex,string breakStatement = "break;") {
        var stateName = this.StateName(index);
        var countName = this.CountName(countIndex);
        builder.AppendLine($"    if(this.{stateName}>=this.{countName}Value) {{");
        builder.AppendLine($"        {breakStatement}");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{stateName}++;");
    }

    private void AppendSkip(StringBuilder builder,int index,int countIndex) {
        var stateName = this.StateName(index);
        var countName = this.CountName(countIndex);
        builder.AppendLine($"    if(this.{stateName}<this.{countName}Value) {{");
        builder.AppendLine($"        this.{stateName}++;");
        builder.AppendLine("        continue;");
        builder.AppendLine("    }");
    }

    private void AppendTakeWhile(StringBuilder builder,PipelineStage stage,string currentName,int index,string breakStatement = "break;") {
        var stateName = this.StateName(index);
        builder.AppendLine($"    if(this.{stateName}) {breakStatement}");
        string[] arguments;
        if(stage.UsesIndex) {
            builder.AppendLine($"    this.{this.IndexName(index)}++;");
            arguments=new[] { currentName,$"this.{this.IndexName(index)}" };
        } else {
            arguments=new[] { currentName };
        }
        var (preCode,condExpr)=this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.TakeWhile),this.TempName(index),arguments);
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"    "));
        }
        builder.AppendLine($"    if(!{WrapConditionExpression(condExpr)}) {{");
        builder.AppendLine($"        this.{stateName}=true;");
        builder.AppendLine($"        {breakStatement}");
        builder.AppendLine("    }");
    }

    private void AppendSkipWhile(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var stateName = this.StateName(index);
        string[] arguments;
        if(stage.UsesIndex) {
            arguments=new[] { currentName,$"this.{this.IndexName(index)}" };
        } else {
            arguments=new[] { currentName };
        }
        var (preCode,condExpr)=this.FuncInvokeCondition(stage.Func!,nameof(PipelineStageKind.SkipWhile),this.TempName(index),arguments);
        builder.AppendLine($"    if(this.{stateName}) {{");
        if(stage.UsesIndex) {
            builder.AppendLine($"        this.{this.IndexName(index)}++;");
        }
        if(!string.IsNullOrEmpty(preCode)) {
            builder.AppendLine(IndentBlock(preCode,"        "));
        }
        builder.AppendLine($"        if({WrapConditionExpression(condExpr)}) continue;");
        builder.AppendLine($"        this.{stateName}=false;");
        builder.AppendLine("    }");
    }

    private string AppendCast(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = this.ItemName(index+1);
        builder.AppendLine($"    var {nextName}=({stage.OutputType})(object?){currentName};");
        return nextName;
    }

    private string AppendOfType(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var nextName = this.ItemName(index+1);
        // 入力の要素型と同じ型で OfType すると、値型では `x is not int y` が「絶対に一致しない」パターンとして CS8518 になる。
        // object 経由で判定すれば、値型は常に一致(JIT が箱化を消す)、参照型は null だけが落ちる。
        var subject = stage.InputType==stage.OutputType ? $"(object?){currentName}" : currentName;
        builder.AppendLine($"    if({subject} is not {stage.OutputType} {nextName}) continue;");
        return nextName;
    }

    private void AppendDistinct(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        builder.AppendLine($"    if(!this.{this.SetName(index)}.Add({currentName})) continue;");
    }

    private void AppendDistinctBy(StringBuilder builder,PipelineStage stage,string currentName,int index) {
        var keyName = this.KeyName(index);
        builder.AppendLine($"    {ComparerKeyType(stage)} {keyName}=default!;");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.DistinctBy),keyName,currentName),"    "));
        builder.AppendLine($"    if(!this.{this.SetName(index)}.Add({keyName})) continue;");
    }
}
