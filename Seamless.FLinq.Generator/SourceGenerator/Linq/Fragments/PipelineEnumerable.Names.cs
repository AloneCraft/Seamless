using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq.Generator;

// 生成コード内で使う名前と型の決定。
internal sealed partial class PipelineEnumerable {
    private IEnumerable<string> ClosureParametersWithoutPrefix() {
        var suffix = this.ClosurePrimaryCtorSuffix(this.Funcs.ToArray());
        if(string.IsNullOrEmpty(suffix)) {
            yield break;
        }
        foreach(var part in SplitTopLevelComma(suffix.Substring(2))) {
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
                yield return text.Substring(start,index-start);
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
                    yield return text.Substring(start,index-start);
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
    private string ItemName(int index)=>GeneratedNames.PreferLocal($"{this.item}_{index}",index<stages.Count ? stages[index].Func : null);
    private string SelectManyResultName(int index) => $"__smr{this.item}_{index}";
    private string TempName(int index) => $"{this.temp}_{index}";
    private string StateName(int index) => $"{this.state}_{index}";
    private string IndexName(int index) => $"__idx{this.item}_{index}";
    private string CountName(int index) => $"__cnt{this.item}_{index}";
    private string ElementArgumentName(int index) => $"__element{this.item}_{index}";
    private IEnumerable<(PipelineStage Stage,int Index)> ElementStages() =>
        stages.Select(static (stage,index)=>(stage,index)).Where(static pair=>pair.stage.Kind is PipelineStageKind.Append or PipelineStageKind.Prepend);
    // 融合した集約の上限を、列挙子の中で描く。Take / Skip の値は、構築のときに 0 以上へ直したフィールド(__cnt…Value)に入っている。
    // 段は参照で探し、見つからなければ同じ値の式の Take / Skip を探す(正規化で段が作り直された場合)。
    private string? GroupLimitExpression(GroupByAggregatePlan plan) =>
        plan.GroupLimit?.Render((stage,valueText) => {
            var index = -1;
            for(var i = 0;i<stages.Count&&index<0;i++) {
                if(ReferenceEquals(stages[i],stage)) {
                    index=i;
                }
            }
            for(var i = 0;i<stages.Count&&index<0;i++) {
                if(stages[i].Kind is PipelineStageKind.Take or PipelineStageKind.Skip&&stages[i].Value==valueText) {
                    index=i;
                }
            }
            if(index<0) {
                throw new InvalidOperationException($"The Take/Skip stage for the GroupBy aggregate limit '{valueText}' was not found.");
            }
            return $"this.{this.CountName(this.CountOrdinal(index))}Value";
        });
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
    private string GroupDirectKeysName(int index) => $"__groupKeys{this.item}_{index}";
    private string GroupDirectAccsName(int index) => $"__groupAccs{this.item}_{index}";
    private string GroupDirectCountName(int index) => $"__groupCount{this.item}_{index}";
    private string GroupDirectCursorName(int index) => $"__groupCursor{this.item}_{index}";
    private string SortItemName(int index) => $"__sortitem{this.item}_{index}";
    private string OrderIndexComparerName(int index) => $"__orderIndexComparer{this.item}_{index}";
    private string OrderTopKComparerName(int index) => $"__orderTopKComparer{this.item}_{index}";
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
    private string GroupBuiltName(int index) => $"__groupBuilt{this.item}_{index}";
    private string GroupCursorName(int index) => $"__groupCursor{this.item}_{index}";
    private string GroupFirstName(int index) => $"__groupFirst{this.item}_{index}";
    private string GroupAggMapName(int index) => $"__groupAggMap{this.item}_{index}";
    private string GroupAggEnumName(int index) => $"__groupAggEnum{this.item}_{index}";
    private string ReverseItemsName(int index) => $"__reversed{this.item}_{index}";
    private string BufferCountName(int index) => $"__bufCount{this.item}_{index}";
    private string EndLabel(int index) => $"__end{this.item}_{index}";
    private string KeyName(int index) => $"{this.key}_{index}";
    private bool StagesAfterNeedEndLabel(int index) =>
        stages.Skip(index+1).Any(static stage => stage.Kind is PipelineStageKind.Take or PipelineStageKind.TakeWhile);
    private bool StagesBetweenNeedEndLabel(int startIndex,int endIndex) =>
        stages.Skip(startIndex+1).Take(endIndex-startIndex-1).Any(static stage => stage.Kind is PipelineStageKind.Take or PipelineStageKind.TakeWhile);
    // 件数の引数(コンストラクタの int __cntN)は CountStages() の順に並ぶので、序数も同じ種類(Take/Skip/SkipLast/TakeLast)を数える。
    // Take/Skip だけを数えると、TakeLast/SkipLast の後ろの Take/Skip が前の段の件数を読んでしまう(TakeLast(6).Take(2) が 6 件返る)。
    private int CountOrdinal(int stageIndex) =>
        stages.Take(stageIndex).Count(static stage => stage.Kind is PipelineStageKind.Take or PipelineStageKind.Skip or PipelineStageKind.SkipLast or PipelineStageKind.TakeLast);
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

    private bool TryGetSmallKeyRange(PipelineStage stage, out int keyCount) {
        keyCount = 0;
        if(stage.Func is not { InlineBody: { } inlineBody } selector) {
            return false;
        }
        if(stage.Value != "null") {
            return false;
        }
        var expr = Unwrap(SyntaxFactory.ParseExpression(inlineBody));
        // パターン 1: x & MASK (例: x & 15)
        if(expr is BinaryExpressionSyntax andExpr && andExpr.IsKind(SyntaxKind.BitwiseAndExpression)) {
            if(Unwrap(andExpr.Right) is LiteralExpressionSyntax lit && lit.Token.Value is int maskVal && maskVal >= 1 && maskVal < 64) {
                if((maskVal & (maskVal + 1)) == 0) {
                    keyCount = maskVal + 1;
                    return true;
                }
            }
        }
        // パターン 2: Math.Abs(x) % M または (x % M + M) % M
        if(expr is BinaryExpressionSyntax modExpr && modExpr.IsKind(SyntaxKind.ModuloExpression)) {
            if(Unwrap(modExpr.Right) is LiteralExpressionSyntax lit && lit.Token.Value is int modVal && modVal >= 2 && modVal <= 64) {
                var left = Unwrap(modExpr.Left);
                if(left is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax mem && mem.Name.Identifier.ValueText == "Abs") {
                    keyCount = modVal;
                    return true;
                }
                if(left is BinaryExpressionSyntax addExpr && addExpr.IsKind(SyntaxKind.AddExpression)) {
                    if(Unwrap(addExpr.Right) is LiteralExpressionSyntax addLit && addLit.Token.Value is int addVal && addVal == modVal) {
                        if(Unwrap(addExpr.Left) is BinaryExpressionSyntax innerMod && innerMod.IsKind(SyntaxKind.ModuloExpression)) {
                            if(Unwrap(innerMod.Right) is LiteralExpressionSyntax innerModLit && innerModLit.Token.Value is int innerModVal && innerModVal == modVal) {
                                keyCount = modVal;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;

        static ExpressionSyntax Unwrap(ExpressionSyntax expression) {
            while(expression is ParenthesizedExpressionSyntax parentheses) { expression = parentheses.Expression; }
            return expression;
        }
    }

    private string OrderTopKComparerType(int orderIndex) {
        var orderStages = this.OrderStages(orderIndex).ToArray();
        var primaryOrder = orderStages[0];
        var comparerName = this.OrderTopKComparerName(orderIndex);
        var builder = new StringBuilder();
        if(orderStages.Length==1) {
            var keyType = ComparerKeyType(primaryOrder);
            var canDirect = CanUseDirectDefaultOrderCompare(primaryOrder);
            builder.AppendLine($"private readonly struct {comparerName} : global::System.Collections.Generic.IComparer<{keyType}> {{");
            if(!canDirect) {
                builder.AppendLine($"    private readonly global::System.Collections.Generic.IComparer<{keyType}> __comparer;");
                builder.AppendLine($"    public {comparerName}(global::System.Collections.Generic.IComparer<{keyType}>? comparer) {{");
                builder.AppendLine($"        this.__comparer = comparer ?? global::System.Collections.Generic.Comparer<{keyType}>.Default;");
                builder.AppendLine("    }");
            } else {
                builder.AppendLine($"    public {comparerName}() {{ }}");
            }
            builder.AppendLine($"    public int Compare({keyType} left,{keyType} right) {{");
            var comparerExpr = canDirect ? string.Empty : "this.__comparer";
            var compareCall = this.OrderCompareCall(primaryOrder,"left","right",comparerExpr);
            var compareExpr = this.OrderCompareExpression(primaryOrder,compareCall);
            builder.AppendLine($"        return {compareExpr};");
            builder.AppendLine("    }");
            builder.AppendLine("}");
        } else if(orderStages.Length==2) {
            var secondaryOrder = orderStages[1];
            var primaryKeyType = ComparerKeyType(primaryOrder);
            var secondaryKeyType = ComparerKeyType(secondaryOrder);
            var primaryCanDirect = CanUseDirectDefaultOrderCompare(primaryOrder);
            var secondaryCanDirect = CanUseDirectDefaultOrderCompare(secondaryOrder);
            builder.AppendLine($"private readonly struct {comparerName} : global::Seamless.Linq.Internal.ITopKCombinedComparer<{primaryKeyType},{secondaryKeyType}> {{");
            if(!primaryCanDirect) {
                builder.AppendLine($"    private readonly global::System.Collections.Generic.IComparer<{primaryKeyType}> __primaryComparer;");
            }
            if(!secondaryCanDirect) {
                builder.AppendLine($"    private readonly global::System.Collections.Generic.IComparer<{secondaryKeyType}> __secondaryComparer;");
            }
            var ctorParams = new List<string>();
            if(!primaryCanDirect) ctorParams.Add($"global::System.Collections.Generic.IComparer<{primaryKeyType}>? primaryComparer");
            if(!secondaryCanDirect) ctorParams.Add($"global::System.Collections.Generic.IComparer<{secondaryKeyType}>? secondaryComparer");
            builder.AppendLine($"    public {comparerName}({string.Join(",",ctorParams)}) {{");
            if(!primaryCanDirect) builder.AppendLine($"        this.__primaryComparer = primaryComparer ?? global::System.Collections.Generic.Comparer<{primaryKeyType}>.Default;");
            if(!secondaryCanDirect) builder.AppendLine($"        this.__secondaryComparer = secondaryComparer ?? global::System.Collections.Generic.Comparer<{secondaryKeyType}>.Default;");
            builder.AppendLine("    }");
            builder.AppendLine($"    public int Compare({primaryKeyType} leftPrimary,{secondaryKeyType} leftSecondary,{primaryKeyType} rightPrimary,{secondaryKeyType} rightSecondary) {{");
            var primaryComparerExpr = primaryCanDirect ? string.Empty : "this.__primaryComparer";
            var primaryCall = this.OrderCompareCall(primaryOrder,"leftPrimary","rightPrimary",primaryComparerExpr);
            var primaryExpr = this.OrderCompareExpression(primaryOrder,primaryCall);
            builder.AppendLine($"        var __p = {primaryExpr};");
            builder.AppendLine("        if(__p != 0) return __p;");
            var secondaryComparerExpr = secondaryCanDirect ? string.Empty : "this.__secondaryComparer";
            var secondaryCall = this.OrderCompareCall(secondaryOrder,"leftSecondary","rightSecondary",secondaryComparerExpr);
            var secondaryExpr = this.OrderCompareExpression(secondaryOrder,secondaryCall);
            builder.AppendLine($"        return {secondaryExpr};");
            builder.AppendLine("    }");
            builder.AppendLine("}");
        }
        return builder.ToString().TrimEnd();
    }

    internal static string IndentBlock(string text,string indent) {
        if(string.IsNullOrWhiteSpace(text)) {
            return string.Empty;
        }
        if(GeneratedTextFormatting.TryIndentPreservingTokens(text,indent,out var preserved)) return preserved;
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
