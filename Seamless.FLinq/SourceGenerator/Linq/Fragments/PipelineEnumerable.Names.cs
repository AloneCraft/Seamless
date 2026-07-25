namespace Seamless.FLinq.Linq.Fragments;

// 生成コード内で使う名前と型の決定。
internal sealed partial class PipelineEnumerable {
    private IEnumerable<string> ClosureArgsWithoutPrefix() {
        var suffix = this.ClosureArgsSuffix(this.Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in SplitTopLevelComma(suffix[2..])) {
            yield return part.Trim();
        }
    }

    private IEnumerable<string> ClosureParametersWithoutPrefix() {
        var suffix = this.ClosurePrimaryCtorSuffix(this.Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in SplitTopLevelComma(suffix[2..])) {
            yield return part.Trim();
        }
    }

    private static IEnumerable<string> SplitTopLevelComma(string text) {
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var angleDepth = 0;
        var start = 0;
        var inString = false;
        var inChar = false;
        var inVerbatimString = false;
        for(var index = 0;index<=text.Length;index++) {
            if(index==text.Length) {
                yield return text[start..index];
                yield break;
            }

            var current = text[index];
            var next = index+1<text.Length ? text[index+1] : '\0';
            if(inString) {
                if(inVerbatimString&&current=='"'&&next=='"') {
                    index++;
                    continue;
                }
                if(!inVerbatimString&&current=='\\') {
                    index++;
                    continue;
                }
                if(current=='"') {
                    inString=false;
                    inVerbatimString=false;
                }
                continue;
            }
            if(inChar) {
                if(current=='\\') {
                    index++;
                    continue;
                }
                if(current=='\'') {
                    inChar=false;
                }
                continue;
            }

            if(current=='@'&&next=='"') {
                inString=true;
                inVerbatimString=true;
                index++;
                continue;
            }
            if(current=='"') {
                inString=true;
                continue;
            }
            if(current=='\'') {
                inChar=true;
                continue;
            }

            switch(current) {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case '<':
                    angleDepth++;
                    break;
                case '>':
                    angleDepth--;
                    break;
                case ',' when parenDepth==0&&bracketDepth==0&&braceDepth==0&&angleDepth==0:
                    yield return text[start..index];
                    start=index+1;
                    break;
            }
        }
    }

    private string QueueName(int index) => $"__queue{this.item}_{index}";
    private string StateEndName(int index) => $"__stateEnd{this.item}_{index}";
    private string RangeName(int index) => $"__range{this.item}_{index}";
    private int RangeOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind==PipelineStageKind.TakeRange);
    private IEnumerable<PipelineStage> RangeStages() =>
        stages.Where(static stage => stage.Kind==PipelineStageKind.TakeRange);
    private string ItemName(int index) => $"{this.item}_{index}";
    private string SelectManyResultName(int index) => $"__smr{this.item}_{index}";
    private string TempName(int index) => $"{this.temp}_{index}";
    private string StateName(int index) => $"{this.state}_{index}";
    private string IndexName(int index) => $"__idx{this.item}_{index}";
    private string CountName(int index) => $"__cnt{this.item}_{index}";
    private string ComparerName(int index) => $"__cmp{this.item}_{index}";
    private string OrderComparerName(int index) => $"__ordcmp{this.item}_{index}";
    private string OrderComparerLocalName(int index) => $"__ordcmpLocal{this.item}_{index}";
    private static string SecondTypeParameterName(int index) => $"TSecond{index}";
    private string SecondEnumeratorName(int index) => $"__second{this.item}_{index}";
    private string InnerEnumeratorName(int index) => $"__inner{this.item}_{index}";
    private string InnerArrayName(int index) => $"__innerArr{this.item}_{index}";
    private string InnerIndexName(int index) => $"__innerIdx{this.item}_{index}";
    private string InnerHasValueName(int index) => $"__innerHas{this.item}_{index}";
    private string OuterName(int index) => $"{this.outerValue}_{index}";
    private string SetName(int index) => $"__set{this.item}_{index}";
    private string SecondSetName(int index) => $"__set2{this.item}_{index}";
    private string SortItemName(int index) => $"__sortitem{this.item}_{index}";
    private string OrderIndexComparerName(int index) => $"__orderIndexComparer{this.item}_{index}";
    private string SortedItemsName(int index) => $"__sorted{this.item}_{index}";
    private string OrderValuesName(int index) => $"__orderValues{this.item}_{index}";
    private string OrderIndexesName(int index) => $"__orderIndexes{this.item}_{index}";
    private string OrderCountName(int index) => $"__orderCount{this.item}_{index}";
    private string OrderValuesLocalName(int index) => $"__orderValuesLocal{this.item}_{index}";
    private string OrderIndexesLocalName(int index) => $"__orderIndexesLocal{this.item}_{index}";
    private string OrderCountLocalName(int index) => $"__orderCountLocal{this.item}_{index}";
    private string OrderInitialCapacityLocalName(int index) => $"__orderInitialCapacity{this.item}_{index}";
    private string OrderReturnValuesLocalName(int index) => $"__orderReturnValues{this.item}_{index}";
    private string OrderReturnIndexesLocalName(int index) => $"__orderReturnIndexes{this.item}_{index}";
    private string OrderNextCapacityLocalName(int index) => $"__orderNextCapacity{this.item}_{index}";
    private string OrderNextValuesLocalName(int index) => $"__orderNextValues{this.item}_{index}";
    private string OrderNextIndexesLocalName(int index) => $"__orderNextIndexes{this.item}_{index}";
    private string OrderValueForKeysLocalName(int index) => $"__orderValueForKeys{this.item}_{index}";
    private string OrderKeysLocalName(int orderIndex,PipelineStage orderStage) => $"__orderKeys{this.item}_{orderIndex}_{this.OrderStageAbsoluteIndex(orderIndex,orderStage)}";
    private string OrderKeyLocalName(int orderIndex,PipelineStage orderStage) => $"__orderKey{this.item}_{orderIndex}_{this.OrderStageAbsoluteIndex(orderIndex,orderStage)}";
    private string OrderIndexComparerKeysFieldName(int orderIndex,PipelineStage orderStage) => $"keys{this.OrderStageAbsoluteIndex(orderIndex,orderStage)}";
    private string OrderIndexComparerComparerFieldName(int orderIndex,PipelineStage orderStage) => $"comparer{this.OrderStageAbsoluteIndex(orderIndex,orderStage)}";
    private string GroupingName(int index) => $"__grouping{this.item}_{index}";
    private string GroupItemsName(int index) => $"__groups{this.item}_{index}";
    private string GroupName(int index) => $"__group{this.item}_{index}";
    private string ReverseItemsName(int index) => $"__reversed{this.item}_{index}";
    private string EndLabel(int index) => $"__end{this.item}_{index}";
    private string KeyName(int index) => $"{this.key}_{index}";
    private bool StagesAfterNeedEndLabel(int index) =>
        stages.Skip(index+1).Any(static stage => stage.Kind is PipelineStageKind.Take or PipelineStageKind.TakeWhile);
    private bool StagesBetweenNeedEndLabel(int startIndex,int endIndex) =>
        stages.Skip(startIndex+1).Take(endIndex-startIndex-1).Any(static stage => stage.Kind is PipelineStageKind.Take or PipelineStageKind.TakeWhile);
    private int CountOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip);
    private int ComparerOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy or PipelineStageKind.Union or PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.UnionBy or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy or PipelineStageKind.GroupBy);
    private int OrderComparerOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending or PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending);
    private int SecondOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.SecondSource is not null);
    private static string ComparerKeyType(PipelineStage stage) => stage.KeyType??stage.OutputType;
    private IEnumerable<PipelineStage> OrderStages(int orderIndex) {
        yield return stages[orderIndex];
        for(var index = orderIndex+1;index<stages.Count;index++) {
            if(stages[index].Kind is not (PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending)) {
                yield break;
            }
            yield return stages[index];
        }
    }
    private string OrderCompareExpression(PipelineStage stage,string compareName) {
        if(stage.Descending) {
            return $"-{compareName}";
        }
        return compareName;
    }
    private string OrderCompareCall(PipelineStage stage,string left,string right,string comparerName) =>
        CanUseDirectDefaultOrderCompare(stage)
            ? $"{left}.CompareTo({right})"
            : $"{comparerName}.Compare({left},{right})";
    private static bool CanUseDirectDefaultOrderCompare(PipelineStage stage) =>
        stage.Value is { } comparer
        &&ComparerExpressionSupport.IsDefaultOrderComparer(comparer,ComparerKeyType(stage))
        &&(ComparerKeyType(stage) is "byte" or "sbyte"
            or "short" or "ushort"
            or "int" or "uint"
            or "long" or "ulong"
            or "float" or "double" or "decimal"
            or "char" or "string"
            or "global::System.Byte" or "global::System.SByte"
            or "global::System.Int16" or "global::System.UInt16"
            or "global::System.Int32" or "global::System.UInt32"
            or "global::System.Int64" or "global::System.UInt64"
            or "global::System.Single" or "global::System.Double" or "global::System.Decimal"
            or "global::System.Char" or "global::System.String");

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        ||string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        ||string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);

    private string OrderSortItemType(int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        if(orderStages.Length==2) {
            var secondaryOrder = orderStages[1];
            return $$"""
            private readonly struct {{this.SortItemName(orderIndex)}} {
                public {{this.SortItemName(orderIndex)}}({{primaryOrder.InputType}} value,{{ComparerKeyType(primaryOrder)}} key,{{ComparerKeyType(secondaryOrder)}} secondKey,int index){
                    this.Value=value;
                    this.Key=key;
                    this.SecondKey=secondKey;
                    this.Index=index;
                }
                public {{primaryOrder.InputType}} Value { get; }
                public {{ComparerKeyType(primaryOrder)}} Key { get; }
                public {{ComparerKeyType(secondaryOrder)}} SecondKey { get; }
                public int Index { get; }
            }
            """;
        }
        return $$"""
        private readonly struct {{this.SortItemName(orderIndex)}} {
            public {{this.SortItemName(orderIndex)}}({{primaryOrder.InputType}} value,{{ComparerKeyType(primaryOrder)}} key,int index){
                this.Value=value;
                this.Key=key;
                this.Index=index;
            }
            public {{primaryOrder.InputType}} Value { get; }
            public {{ComparerKeyType(primaryOrder)}} Key { get; }
            public int Index { get; }
        }
        """;
    }

    private string OrderIndexComparerType(int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var comparerName = this.OrderIndexComparerName(orderIndex);
        var builder = new StringBuilder();
        builder.AppendLine($"private readonly struct {comparerName} : global::System.Collections.Generic.IComparer<int> {{");
        foreach(var orderStage in orderStages) {
            builder.AppendLine($"    private readonly {ComparerKeyType(orderStage)}[] {this.OrderIndexComparerKeysFieldName(orderIndex,orderStage)};");
            if(!CanUseDirectDefaultOrderCompare(orderStage)) {
                builder.AppendLine($"    private readonly global::System.Collections.Generic.IComparer<{ComparerKeyType(orderStage)}> {this.OrderIndexComparerComparerFieldName(orderIndex,orderStage)};");
            }
        }

        var parameters = orderStages
            .SelectMany(orderStage => {
                var args = new List<string> { $"{ComparerKeyType(orderStage)}[] {this.OrderIndexComparerKeysFieldName(orderIndex,orderStage)}" };
                if(!CanUseDirectDefaultOrderCompare(orderStage)) {
                    args.Add($"global::System.Collections.Generic.IComparer<{ComparerKeyType(orderStage)}>? {this.OrderIndexComparerComparerFieldName(orderIndex,orderStage)}");
                }
                return args;
            });
        builder.AppendLine($"    public {comparerName}({string.Join(",",parameters)}) {{");
        foreach(var orderStage in orderStages) {
            var keysField = this.OrderIndexComparerKeysFieldName(orderIndex,orderStage);
            var comparerField = this.OrderIndexComparerComparerFieldName(orderIndex,orderStage);
            builder.AppendLine($"        this.{keysField}={keysField};");
            if(!CanUseDirectDefaultOrderCompare(orderStage)) {
                builder.AppendLine($"        this.{comparerField}={comparerField} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(orderStage)}>.Default;");
            }
        }
        builder.AppendLine("    }");
        builder.AppendLine("    public int Compare(int left,int right) {");
        for(var index = 0;index<orderStages.Length;index++) {
            var orderStage = orderStages[index];
            var compareName = index==0 ? "__orderCompare" : $"__orderCompare{index}";
            var keysField = this.OrderIndexComparerKeysFieldName(orderIndex,orderStage);
            var comparerField = this.OrderIndexComparerComparerFieldName(orderIndex,orderStage);
            var comparerExpression = CanUseDirectDefaultOrderCompare(orderStage) ? string.Empty : $"this.{comparerField}";
            builder.AppendLine($"        var {compareName}={this.OrderCompareCall(orderStage,$"this.{keysField}[left]",$"this.{keysField}[right]",comparerExpression)};");
            builder.AppendLine($"        if({compareName}!=0) return {this.OrderCompareExpression(orderStage,compareName)};");
        }
        builder.AppendLine("        return left.CompareTo(right);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private string GroupValueType(int index,PipelineStage stage) {
        if(stage.ThirdFunc is not null) {
            return stage.OutputType;
        }
        return this.GroupingName(index);
    }

    private string GroupingType(int index,PipelineStage stage) => $$"""
        private sealed class {{this.GroupingName(index)}}:global::System.Linq.IGrouping<{{ComparerKeyType(stage)}},{{stage.InnerElementType}}> {
            private readonly global::System.Collections.Generic.List<{{stage.InnerElementType}}> values = new();
            public {{this.GroupingName(index)}}({{ComparerKeyType(stage)}} key){
                this.Key=key;
            }
            public {{ComparerKeyType(stage)}} Key { get; }
            public void Add({{stage.InnerElementType}} value) => this.values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{stage.InnerElementType}}> GetEnumerator() => this.values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        """;

    private static string IndentBlock(string text,string indent) {
        if(string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }
        var lines = text.Replace("\r\n","\n").Split('\n');
        var builder = new StringBuilder();
        for(var index = 0;index<lines.Length;index++) {
            if(!string.IsNullOrWhiteSpace(lines[index])) {
                builder.Append(indent).Append(lines[index]);
            }
            if(index<lines.Length-1) {
                builder.AppendLine();
            }
        }
        return builder.ToString();
    }
}
