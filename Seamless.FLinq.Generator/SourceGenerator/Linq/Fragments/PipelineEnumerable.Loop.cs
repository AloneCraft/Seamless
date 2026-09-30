namespace Seamless.FLinq.Generator;

// 段を畳んだループ本体の構築。
internal sealed partial class PipelineEnumerable {
    private string BuildLoop(string enumeratorName,string successStatement,string endStatement) {
        var binarySetIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => IsBinarySetStage(pair.stage.Kind))
            .Select(static pair => pair.index)
            .ToArray();
        var orderIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => IsPrimaryOrderStage(pair.stage.Kind))
            .Select(static pair => pair.index)
            .ToArray();
        if(binarySetIndex.Length==1) {
            if(orderIndex.Length>0) {
                return this.BuildBinarySetOrderLoop(enumeratorName,successStatement,endStatement,binarySetIndex[0],orderIndex[orderIndex.Length-1]);
            }
            return this.BuildBinarySetLoop(enumeratorName,successStatement,endStatement,binarySetIndex[0]);
        }
        if(binarySetIndex.Length>1) {
            throw new NotSupportedException("Multiple binary set stages are not supported in PipelineEnumerable.");
        }
        var selectManyIndex = stages.Select(static (stage,index) => (stage,index)).Where(static pair => pair.stage.Kind==PipelineStageKind.SelectMany).Select(static pair => pair.index).ToArray();
        if(selectManyIndex.Length==1) {
            return this.BuildSelectManyLoop(enumeratorName,successStatement,endStatement,selectManyIndex[0]);
        }
        var groupByIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.GroupBy)
            .Select(static pair => pair.index)
            .ToArray();
        if(groupByIndex.Length==1) {
            return this.BuildGroupByLoop(enumeratorName,successStatement,endStatement,groupByIndex[0]);
        }
        if(groupByIndex.Length>1) {
            throw new NotSupportedException("Multiple GroupBy stages are not supported in PipelineEnumerable.");
        }
        var reverseIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.Reverse)
            .Select(static pair => pair.index)
            .ToArray();
        if(reverseIndex.Length==1) {
            return this.BuildReverseLoop(enumeratorName,successStatement,endStatement,reverseIndex[0]);
        }
        if(reverseIndex.Length>1) {
            throw new NotSupportedException("Multiple Reverse stages are not supported in PipelineEnumerable.");
        }
        var defaultIfEmptyIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.DefaultIfEmpty)
            .Select(static pair => pair.index)
            .ToArray();
        if(defaultIfEmptyIndex.Length==1) {
            return this.BuildDefaultIfEmptyLoop(enumeratorName,successStatement,endStatement,defaultIfEmptyIndex[0]);
        }
        if(defaultIfEmptyIndex.Length>1) {
            throw new NotSupportedException("Multiple DefaultIfEmpty stages are not supported in PipelineEnumerable.");
        }
        if(orderIndex.Length==1) {
            return this.BuildOrderLoop(enumeratorName,successStatement,endStatement,orderIndex[0]);
        }
        if(orderIndex.Length>1) {
            // 複数の OrderBy(後の OrderBy が主キー、前の順序はキーが等しいときの並び)は PipelineBuilder の検証で拒否済みで、通常ここには来ない。
            return this.BuildOrderLoop(enumeratorName,successStatement,endStatement,orderIndex[orderIndex.Length-1]);
        }
        var skipLastIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.SkipLast)
            .Select(static pair => pair.index).ToArray();
        if(skipLastIndex.Length==1) {
            return this.BuildSkipLastLoop(enumeratorName,successStatement,endStatement,skipLastIndex[0]);
        }
        var takeLastIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.TakeLast)
            .Select(static pair => pair.index).ToArray();
        if(takeLastIndex.Length==1) {
            return this.BuildTakeLastLoop(enumeratorName,successStatement,endStatement,takeLastIndex[0]);
        }
        var takeRangeIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind==PipelineStageKind.TakeRange)
            .Select(static pair => pair.index).ToArray();
        if(takeRangeIndex.Length==1) {
            return this.BuildTakeRangeLoop(enumeratorName,successStatement,endStatement,takeRangeIndex[0]);
        }
        var concatLikeIndex = stages.Select(static (stage,index) => (stage,index))
            .Where(static pair => pair.stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend)
            .Select(static pair => pair.index)
            .ToArray();
        if(concatLikeIndex.Length==1) {
            return this.BuildConcatLikeLoop(enumeratorName,successStatement,endStatement,concatLikeIndex[0]);
        }
        if(concatLikeIndex.Length>1) {
            throw new NotSupportedException("Multiple concat-like stages are not supported in PipelineEnumerable.");
        }

        var builder = new StringBuilder();
        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<stages.Count;index++) {
            var stage = stages[index];
            switch(stage.Kind) {
                case PipelineStageKind.Where:
                    this.AppendWhere(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Select:
                    currentName=this.AppendSelect(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.SelectMany:
                    throw new NotSupportedException("Multiple SelectMany stages are not supported in PipelineEnumerable.");
                case PipelineStageKind.Distinct:
                    this.AppendDistinct(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.DistinctBy:
                    this.AppendDistinctBy(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Union:
                case PipelineStageKind.Except:
                case PipelineStageKind.Intersect:
                case PipelineStageKind.UnionBy:
                case PipelineStageKind.ExceptBy:
                case PipelineStageKind.IntersectBy:
                    throw new NotSupportedException("Binary set stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.Concat:
                case PipelineStageKind.Append:
                case PipelineStageKind.Prepend:
                    throw new NotSupportedException("Concat-like stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.OrderBy:
                case PipelineStageKind.OrderByDescending:
                case PipelineStageKind.ThenBy:
                case PipelineStageKind.ThenByDescending:
                    throw new NotSupportedException("Ordering stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.GroupBy:
                    throw new NotSupportedException("GroupBy stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.Reverse:
                    throw new NotSupportedException("Reverse stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.DefaultIfEmpty:
                    throw new NotSupportedException("DefaultIfEmpty stage requires a dedicated PipelineEnumerable loop.");
                case PipelineStageKind.AsEnumerable:
                    break;
                case PipelineStageKind.Take:
                    this.AppendTake(builder,index,this.CountOrdinal(index));
                    break;
                case PipelineStageKind.Skip:
                    this.AppendSkip(builder,index,this.CountOrdinal(index));
                    break;
                case PipelineStageKind.TakeWhile:
                    this.AppendTakeWhile(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.SkipWhile:
                    this.AppendSkipWhile(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.Cast:
                    currentName=this.AppendCast(builder,stage,currentName,index);
                    break;
                case PipelineStageKind.OfType:
                    currentName=this.AppendOfType(builder,stage,currentName,index);
                    break;
            }
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"    "));
        builder.AppendLine("}");
        builder.AppendLine(endStatement);
        return builder.ToString().TrimEnd();
    }

    private string BuildConcatLikeLoop(string enumeratorName,string successStatement,string endStatement,int concatIndex) {
        var stage = stages[concatIndex];
        var builder = new StringBuilder();
        var endLabel = this.EndLabel(concatIndex);
        var needsEndLabel = this.StagesAfterNeedEndLabel(concatIndex);
        var breakStatement = "break;";
        if(needsEndLabel) {
            breakStatement=$"goto {endLabel};";
        }
        builder.AppendLine("while(true) {");
        if(stage.Kind==PipelineStageKind.Prepend) {
            builder.AppendLine($"    if(this.{this.StateName(concatIndex)}==0) {{");
            builder.AppendLine($"        this.{this.StateName(concatIndex)}=1;");
            var prependName = this.ItemName(concatIndex+1);
            builder.AppendLine($"        var {prependName}=({stage.OutputType})({stage.Value});");
            var currentName = prependName;
            for(var index = concatIndex+1;index<stages.Count;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine($"    if(this.{this.StateName(concatIndex)}<=1) {{");
        builder.AppendLine($"        while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"            ","IncrementPhysicalMoveNext");
        var sourceCurrentName = this.ItemName(0);
        for(var index = 0;index<concatIndex;index++) {
            sourceCurrentName=this.AppendStage(builder,stages[index],sourceCurrentName,index);
        }
        for(var index = concatIndex+1;index<stages.Count;index++) {
            sourceCurrentName=this.AppendStage(builder,stages[index],sourceCurrentName,index,breakStatement);
        }
        builder.AppendLine(IndentBlock(string.Format(successStatement,sourceCurrentName),"            "));
        builder.AppendLine("        }");
        builder.AppendLine($"        this.{this.StateName(concatIndex)}=2;");
        builder.AppendLine("    }");
        if(stage.Kind==PipelineStageKind.Concat) {
            builder.AppendLine($"    while({this.SecondEnumeratorName(this.SecondOrdinal(concatIndex))}.TryGetNext(out var {this.ItemName(concatIndex+1)})) {{");
            this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
            var secondName = this.ItemName(concatIndex+1);
            builder.AppendLine($"        {secondName}=({stage.OutputType})(object?){secondName}!;");
            var currentName = secondName;
            for(var index = concatIndex+1;index<stages.Count;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
            builder.AppendLine($"    this.{this.StateName(concatIndex)}=3;");
        } else if(stage.Kind==PipelineStageKind.Append) {
            builder.AppendLine($"    if(this.{this.StateName(concatIndex)}==2) {{");
            builder.AppendLine($"        this.{this.StateName(concatIndex)}=3;");
            var appendName = this.ItemName(concatIndex+1);
            builder.AppendLine($"        var {appendName}=({stage.OutputType})({stage.Value});");
            var currentName = appendName;
            for(var index = concatIndex+1;index<stages.Count;index++) {
                currentName=this.AppendStage(builder,stages[index],currentName,index,breakStatement);
            }
            builder.AppendLine(IndentBlock(string.Format(successStatement,currentName),"        "));
            builder.AppendLine("    }");
        }
        builder.AppendLine(IndentBlock(endStatement,"    "));
        builder.AppendLine("}");
        if(needsEndLabel) {
            builder.AppendLine($"{endLabel}:");
            builder.AppendLine(endStatement);
        }
        return builder.ToString().TrimEnd();
    }
}
