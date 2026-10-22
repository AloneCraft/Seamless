namespace Seamless.FLinq.Generator;

// 段ごとのループ本体（GroupBy/Reverse/SelectMany/集合演算など）。
internal sealed partial class PipelineEnumerable {
    private string BuildGroupByLoop(string enumeratorName,string successStatement,string endStatement,int groupByIndex) {
        var stage = stages[groupByIndex];
        var builder = new StringBuilder();
        if(stage.AggregatePlan is { } aggPlan) {
            builder.AppendLine($"if(!this.{this.GroupBuiltName(groupByIndex)}) {{");
            builder.AppendLine($"    this.{this.GroupBuiltName(groupByIndex)}=true;");
            builder.AppendLine($"    var __hasCnt={enumeratorName}.TryGetNonEnumeratedCount(out var __cnt);");
            builder.AppendLine($"    this.{this.GroupAggMapName(groupByIndex)}=new global::Seamless.Linq.DictionarySlim<{ComparerKeyType(stage)},{aggPlan.AccumulatorType}>(__hasCnt ? global::System.Math.Min(__cnt,1024) : 0,{this.ComparerName(this.ComparerOrdinal(groupByIndex))});");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var currentName = this.ItemName(0);
            for(var index = 0;index<groupByIndex;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index);
            }
            var keyName = this.KeyName(groupByIndex);
            builder.AppendLine($"        {ComparerKeyType(stage)} {keyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),"        "));
            var groupElementName = $"__groupElem_{groupByIndex}";
            builder.AppendLine($"        {stage.InnerElementType} {groupElementName}=default!;");
            if(stage.SecondFunc is { } elementSelector) {
                builder.AppendLine(IndentBlock(this.FuncAssign(elementSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),"        "));
            } else {
                builder.AppendLine($"        {groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
            }
            builder.AppendLine($"        ref var __acc=ref this.{this.GroupAggMapName(groupByIndex)}!.GetValueRefOrAddDefault({keyName},out var __exists);");
            builder.AppendLine(aggPlan.RenderAccumulatorUpdate("__acc",groupElementName,"        "));
            builder.AppendLine("    }");
            builder.AppendLine($"    this.{this.GroupAggEnumName(groupByIndex)}=this.{this.GroupAggMapName(groupByIndex)}!.GetEnumerator();");
            builder.AppendLine("}");
            builder.AppendLine($"while(this.{this.GroupAggEnumName(groupByIndex)}.TryGetNext(out var __kv)) {{");
            builder.AppendLine("    var __key=__kv.Key;");
            builder.AppendLine("    var __acc=__kv.Value;");
            var resultName = this.ItemName(groupByIndex+1);
            builder.AppendLine($"    {stage.OutputType} {resultName}={aggPlan.ProjectedResultExpression};");
            var afterName = resultName;
            for(var index = groupByIndex+1;index<stages.Count;index++) {
                afterName=this.AppendStage(builder,stages[index],afterName,index);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,afterName),"    "));
            builder.AppendLine("}");
            builder.AppendLine(endStatement);
            return builder.ToString().TrimEnd();
        }
        builder.AppendLine($"if(!this.{this.GroupBuiltName(groupByIndex)}) {{");
        builder.AppendLine($"    this.{this.GroupBuiltName(groupByIndex)}=true;");
        builder.AppendLine($"    var __hasCnt={enumeratorName}.TryGetNonEnumeratedCount(out var __cnt);");
        builder.AppendLine($"    var __builder=new global::Seamless.Linq.ContiguousLookupBuilder<{ComparerKeyType(stage)},{stage.InnerElementType}>(__hasCnt ? __cnt : 0,__hasCnt ? global::System.Math.Min(__cnt,1024) : 0,{this.ComparerName(this.ComparerOrdinal(groupByIndex))});");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName2 = this.ItemName(0);
        for(var index = 0;index<groupByIndex;index++) {
            currentName2=this.AppendStage(builder,stages[index],currentName2,index);
        }
        var keyName2 = this.KeyName(groupByIndex);
        builder.AppendLine($"        {ComparerKeyType(stage)} {keyName2}=default!;");
        builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName2,currentName2),"        "));
        var groupElementName2 = $"__groupElem_{groupByIndex}";
        builder.AppendLine($"        {stage.InnerElementType} {groupElementName2}=default!;");
        if(stage.SecondFunc is { } elementSelector2) {
            builder.AppendLine(IndentBlock(this.FuncAssign(elementSelector2,nameof(PipelineStageKind.GroupBy),groupElementName2,currentName2),"        "));
        } else {
            builder.AppendLine($"        {groupElementName2}=({stage.InnerElementType})(object?){currentName2}!;");
        }
        builder.AppendLine($"        __builder.Add({keyName2},{groupElementName2});");
        builder.AppendLine("    }");
        builder.AppendLine($"    this.{this.GroupCursorName(groupByIndex)}=this.{this.GroupFirstName(groupByIndex)}=__builder.GetRootGroupAndClear();");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.GroupCursorName(groupByIndex)} is not null) {{");
        builder.AppendLine($"    var __group=this.{this.GroupCursorName(groupByIndex)}!;");
        builder.AppendLine("    var __next=__group.NextGroupInAddOrder;");
        builder.AppendLine($"    this.{this.GroupCursorName(groupByIndex)}=__next==this.{this.GroupFirstName(groupByIndex)} ? null : __next;");
        var groupOutName = this.ItemName(groupByIndex+1);
        if(stage.ThirdFunc is { } resultSelector2) {
            builder.AppendLine($"    {stage.OutputType} {groupOutName}=default!;");
            builder.AppendLine(IndentBlock(this.GroupResultAssign(resultSelector2,stage.InnerElementType,groupOutName),"    "));
        } else {
            builder.AppendLine($"    var {groupOutName}=__group;");
        }
        var afterName2 = groupOutName;
        for(var index = groupByIndex+1;index<stages.Count;index++) {
            afterName2=this.AppendStage(builder,stages[index],afterName2,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,afterName2),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    // GroupBy の結果選択子を呼ぶ。GroupBy(key,elem,(k,g)=>...) 由来なら引数は (キー, 要素の列) の2つ、
    // GroupBy(key).Select(g=>...) を畳んだ形なら引数は IGrouping 1つ(g)。
    // 後者に「キー」を渡すと g.Key が key.Key になってコンパイルできない(2026-10-22 に Zip / Join の前で見つかった)。
    private string GroupResultAssign(FuncFragment resultSelector,string? innerElementType,string destination) {
        if(resultSelector.ParameterNames.Length==1) {
            return this.FuncAssign(resultSelector,nameof(PipelineStageKind.GroupBy),destination,"__group");
        }
        return this.FuncAssign(resultSelector,nameof(PipelineStageKind.GroupBy),destination,"__group.Key","(global::System.Collections.Generic.IEnumerable<"+innerElementType+">)__group");
    }

    private string BuildGroupByOrderLoop(string enumeratorName,string successStatement,string endStatement,int groupByIndex,int orderIndex) {
        var stage = stages[groupByIndex];
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var lastOrderIndex = orderIndex+orderStages.Length-1;
        var builder = new StringBuilder();
        var endLabel = this.EndLabel(orderIndex);
        var needsEndLabel = this.StagesBetweenNeedEndLabel(groupByIndex,orderIndex);
        var breakStatement = needsEndLabel ? $"goto {endLabel};" : "break;";
        builder.AppendLine($"if(this.{this.OrderValuesName(orderIndex)} is null) {{");
        if(stage.AggregatePlan is { } aggPlan) {
            builder.AppendLine($"    var __hasSrcCount={enumeratorName}.TryGetNonEnumeratedCount(out var __srcCount);");
            builder.AppendLine($"    var __aggMap=new global::Seamless.Linq.DictionarySlim<{ComparerKeyType(stage)},{aggPlan.AccumulatorType}>(__hasSrcCount ? global::System.Math.Min(__srcCount,1024) : 0,{this.ComparerName(this.ComparerOrdinal(groupByIndex))});");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var currentName = this.ItemName(0);
            for(var index = 0;index<groupByIndex;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index);
            }
            var keyName = this.KeyName(groupByIndex);
            builder.AppendLine($"        {ComparerKeyType(stage)} {keyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),"        "));
            var groupElementName = $"__groupElem_{groupByIndex}";
            builder.AppendLine($"        {stage.InnerElementType} {groupElementName}=default!;");
            if(stage.SecondFunc is { } elementSelector) {
                builder.AppendLine(IndentBlock(this.FuncAssign(elementSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),"        "));
            } else {
                builder.AppendLine($"        {groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
            }
            builder.AppendLine($"        ref var __acc=ref __aggMap.GetValueRefOrAddDefault({keyName},out var __exists);");
            builder.AppendLine(aggPlan.RenderAccumulatorUpdate("__acc",groupElementName,"        "));
            builder.AppendLine("    }");
            builder.AppendLine($"    var {this.OrderInitialCapacityLocalName(orderIndex)}=global::System.Math.Max(16,__aggMap.Count);");
            this.AppendOrderBufferBuildStart(builder,orderIndex);
            builder.AppendLine("        var __aggEnum=__aggMap.GetEnumerator();");
            builder.AppendLine("        while(__aggEnum.TryGetNext(out var __kv)) {");
            builder.AppendLine("            var __key=__kv.Key;");
            builder.AppendLine("            var __acc=__kv.Value;");
            var resultName = this.ItemName(groupByIndex+1);
            builder.AppendLine($"            {stage.OutputType} {resultName}={aggPlan.ProjectedResultExpression};");
            var intermediateName = resultName;
            for(var index = groupByIndex+1;index<orderIndex;index++) {
                intermediateName=this.AppendStage(builder,stages[index],intermediateName,index,breakStatement);
            }
            this.AppendOrderBufferedValue(builder,orderIndex,intermediateName,"            ");
            builder.AppendLine("        }");
        } else {
            builder.AppendLine($"    var __hasSrcCount={enumeratorName}.TryGetNonEnumeratedCount(out var __srcCount);");
            builder.AppendLine($"    var __builder=new global::Seamless.Linq.ContiguousLookupBuilder<{ComparerKeyType(stage)},{stage.InnerElementType}>(__hasSrcCount ? __srcCount : 0,__hasSrcCount ? global::System.Math.Min(__srcCount,1024) : 0,{this.ComparerName(this.ComparerOrdinal(groupByIndex))});");
            builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var currentName = this.ItemName(0);
            for(var index = 0;index<groupByIndex;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index);
            }
            var keyName = this.KeyName(groupByIndex);
            builder.AppendLine($"        {ComparerKeyType(stage)} {keyName}=default!;");
            builder.AppendLine(IndentBlock(this.FuncAssign(stage.Func!,nameof(PipelineStageKind.GroupBy),keyName,currentName),"        "));
            var groupElementName = $"__groupElem_{groupByIndex}";
            builder.AppendLine($"        {stage.InnerElementType} {groupElementName}=default!;");
            if(stage.SecondFunc is { } elementSelector) {
                builder.AppendLine(IndentBlock(this.FuncAssign(elementSelector,nameof(PipelineStageKind.GroupBy),groupElementName,currentName),"        "));
            } else {
                builder.AppendLine($"        {groupElementName}=({stage.InnerElementType})(object?){currentName}!;");
            }
            builder.AppendLine($"        __builder.Add({keyName},{groupElementName});");
            builder.AppendLine("    }");
            builder.AppendLine($"    var {this.OrderInitialCapacityLocalName(orderIndex)}=global::System.Math.Max(16,__builder.GroupCount);");
            builder.AppendLine("    var __firstGroup=__builder.GetRootGroupAndClear();");
            this.AppendOrderBufferBuildStart(builder,orderIndex);
            builder.AppendLine("        var __groupCursor=__firstGroup;");
            builder.AppendLine("        while(__groupCursor is not null) {");
            builder.AppendLine("            var __group=__groupCursor;");
            builder.AppendLine("            var __nextGroup=__group.NextGroupInAddOrder;");
            builder.AppendLine("            __groupCursor=__nextGroup==__firstGroup ? null : __nextGroup;");
            var groupOutName = this.ItemName(groupByIndex+1);
            if(stage.ThirdFunc is { } resultSelector) {
                builder.AppendLine($"            {stage.OutputType} {groupOutName}=default!;");
                builder.AppendLine(IndentBlock(this.GroupResultAssign(resultSelector,stage.InnerElementType,groupOutName),"            "));
            } else {
                builder.AppendLine($"            var {groupOutName}=__group;");
            }
            var intermediateName = groupOutName;
            for(var index = groupByIndex+1;index<orderIndex;index++) {
                intermediateName=this.AppendStage(builder,stages[index],intermediateName,index,breakStatement);
            }
            this.AppendOrderBufferedValue(builder,orderIndex,intermediateName,"            ");
            builder.AppendLine("        }");
        }
        if(needsEndLabel) {
            builder.AppendLine($"        {endLabel}:");
        }
        this.AppendOrderBufferBuildEnd(builder,orderIndex,orderStages);
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(orderIndex)}<this.{this.OrderCountName(orderIndex)}) {{");
        var sortedCurrentName = this.ItemName(lastOrderIndex+1);
        builder.AppendLine($"    var {sortedCurrentName}=this.{this.OrderValuesName(orderIndex)}[this.{this.OrderIndexesName(orderIndex)}[this.{this.StateName(orderIndex)}++]];");
        var currentName3 = sortedCurrentName;
        for(var index = lastOrderIndex+1;index<stages.Count;index++) {
            currentName3=this.AppendStage(builder,stages[index],currentName3,index);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName3),"    "));
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
        builder.AppendLine($"            {(stage.SecondSource?.ElementType==stage.OutputType ? string.Empty : $"{secondCurrentName}=({stage.OutputType})(object?){secondCurrentName}!;")}");
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
        builder.AppendLine($"            this.{this.SecondSetName(setIndex)}.Add({this.SecondValueAs(stage,ComparerKeyType(stage),this.ItemName(setIndex+1))});");
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
        builder.AppendLine($"            {this.SingleSetCheck(stage,setIndex,setKeyName)}");
        this.AppendBinarySetOrderTail(builder,setIndex,orderIndex,currentName,breakStatement,"            ");
        builder.AppendLine("        }");
    }

    // 集合演算の段より前の値が一意(.Anchor の無い Range の後に添字の無い Where だけ)なら、重複除きは要らないので第2ソースの集合を
    // 引くだけにする。そうでなければ、Intersect は第2ソースの集合から取り除けたもの(第2ソースにあり、まだ出していない)だけを流す
    // (集合1つ。System.Linq と同じ)。Except は第2ソースの集合に無く、出したものの集合へ足せたものだけを流す
    // (第2ソースの集合へ足して重複除きを兼ねる形は、大きい集合へ足すことになり遅かった)。
    private string SingleSetCheck(PipelineStage stage,int setIndex,string keyName) {
        var secondSet = $"this.{this.SecondSetName(setIndex)}";
        if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect&&this.ValuesUniqueBefore(setIndex)) {
            return stage.Kind==PipelineStageKind.Except
                ? $"if({secondSet}.Contains({keyName})) continue;"
                : $"if(!{secondSet}.Contains({keyName})) continue;";
        }
        return stage.Kind is PipelineStageKind.Except or PipelineStageKind.ExceptBy
            ? $"if({secondSet}.Contains({keyName}) || !this.{this.SetName(setIndex)}.Add({keyName})) continue;"
            : $"if(!{secondSet}.Remove({keyName})) continue;";
    }

    private bool ValuesUniqueBefore(int stageIndex) =>
        source is RangeRoot or DescendingRangeRoot
        &&!source.TreeContainsAnchor()
        &&stages.Take(stageIndex).All(static stage => stage.Kind==PipelineStageKind.Where&&!stage.UsesIndex);

    // 第2ソースの値を集合のキーの型として渡す。型が同じなら変換しない(参照型の共有ジェネリックコードでは object 経由の変換が型検査の呼び出しになる)。
    private string SecondValueAs(PipelineStage stage,string targetType,string valueName) =>
        stage.SecondSource?.ElementType==targetType
            ? valueName
            : $"({targetType})(object?){valueName}!";

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
        builder.AppendLine($"        this.{this.SecondSetName(setIndex)}.Add({this.SecondValueAs(stage,ComparerKeyType(stage),this.ItemName(setIndex+1))});");
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
        builder.AppendLine($"    {this.SingleSetCheck(stage,setIndex,setKeyName)}");
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
        builder.AppendLine($"        {(stages[unionIndex].SecondSource?.ElementType==stages[unionIndex].OutputType ? string.Empty : $"{secondCurrentName}=({stages[unionIndex].OutputType})(object?){secondCurrentName}!;")}");
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
