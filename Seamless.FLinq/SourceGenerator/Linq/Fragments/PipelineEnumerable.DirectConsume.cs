namespace Seamless.FLinq.Linq.Fragments;

// 単段パイプラインを Consume へ直接展開する経路。
internal sealed partial class PipelineEnumerable {
    public override string GetConsumeExecutionStatement(string consumerName) =>
        this.RenderSingleTakeWhileDirectConsume(consumerName)
        ??this.RenderSingleSkipWhileDirectConsume(consumerName)
        ??this.RenderSingleConcatDirectConsume(consumerName)
        ??this.RenderSingleAppendOrPrependDirectConsume(consumerName)
        ??this.RenderSingleDefaultIfEmptyDirectConsume(consumerName)
        ??this.RenderSingleSelectDirectConsume(consumerName)
        ??this.RenderSingleWhereDirectConsume(consumerName)
        ??this.RenderPureOrderingDirectConsume(consumerName)
        ??base.GetConsumeExecutionStatement(consumerName);

    private string? RenderSingleAppendOrPrependDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind is not (PipelineStageKind.Append or PipelineStageKind.Prepend)
            ||stage.Value is not { } value) {
            return null;
        }

        var sourceName = $"__appendSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__appendSpan{this.result}";
        var itemName = $"__appendItem{this.result}";
        var appendValue = $"{consumerName}.Consume({value});";
        var before = stage.Kind==PipelineStageKind.Prepend ? appendValue+"\n" : string.Empty;
        var after = stage.Kind==PipelineStageKind.Append ? "\n"+appendValue : string.Empty;
        return $$"""
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            {{before}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{consumerName}}.Consume({{itemName}});
            }{{after}}
            """;
    }

    private string? RenderSingleDefaultIfEmptyDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.DefaultIfEmpty||stage.Value is not { } defaultValue) {
            return null;
        }

        var sourceName = $"__defaultSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__defaultSpan{this.result}";
        var itemName = $"__defaultItem{this.result}";
        return $$"""
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            if({{spanName}}.Length==0){
                {{consumerName}}.Consume({{defaultValue}});
            }else{
                for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                    var {{itemName}}={{spanName}}[__i{{this.result}}];
                    {{consumerName}}.Consume({{itemName}});
                }
            }
            """;
    }

    private string? RenderSingleConcatDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.Concat||stage.SecondSource is not { } secondSource) {
            return null;
        }

        var sourceName = $"__concatSource{this.result}";
        var secondName = $"__concatSecond{this.result}";
        var builder = new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={secondSource.GetEnumeratorExpression};");
        this.AppendConcatConsumeSource(builder,source,sourceName,consumerName,$"__concatLeftSpan{this.result}",$"__concatLeftItem{this.result}",source.ElementType);
        this.AppendConcatConsumeSource(builder,secondSource,secondName,consumerName,$"__concatRightSpan{this.result}",$"__concatRightItem{this.result}",stage.OutputType);
        return builder.ToString().TrimEnd();
    }

    private void AppendConcatConsumeSource(StringBuilder builder,Enumerable sourceFragment,string enumeratorName,string consumerName,string spanName,string itemName,string outputType) {
        var directSpanExpression = sourceFragment.GetDirectSpanExpression(enumeratorName);
        if(directSpanExpression is not null) {
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();");
            builder.AppendLine($"var {spanName}={directSpanExpression};");
            builder.AppendLine($"for(var __i{this.result}=0;__i{this.result}<{spanName}.Length;__i{this.result}++) {{");
            builder.AppendLine($"    var {itemName}={spanName}[__i{this.result}];");
            builder.AppendLine($"    {consumerName}.Consume({this.CastConcatItem(sourceFragment.ElementType,outputType,itemName)});");
            builder.AppendLine("}");
            return;
        }

        builder.AppendLine($"while({enumeratorName}.TryGetNext(out var {itemName})) {{");
        this.AppendCounterCall(builder,"    ","IncrementPhysicalMoveNext");
        builder.AppendLine($"    {consumerName}.Consume({this.CastConcatItem(sourceFragment.ElementType,outputType,itemName)});");
        builder.AppendLine("}");
    }

    private string CastConcatItem(string inputType,string outputType,string itemName) =>
        inputType==outputType
            ? itemName
            : $"({outputType})(object?){itemName}!";

    private string? RenderSingleWhereDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.Where
            ||stage.Func is not { } predicate
            ||predicate.RequiresStatementInlining
            ||!predicate.CanInlineAssign) {
            return null;
        }

        var sourceName = $"__whereSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__whereSpan{this.result}";
        var itemName = $"__whereItem{this.result}";
        var indexName = $"__whereIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : [itemName];
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var (preCode,conditionExpr)=this.FuncInvokeCondition(predicate,nameof(PipelineStageKind.Where),this.temp,arguments);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if({WrapConditionExpression(conditionExpr)})"
            : $"{preCode}\n        if({WrapConditionExpression(conditionExpr)})";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return $$"""
            {{localDeclarations}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{indexIncrement}}{{predicateCheck}} {
                    {{consumerName}}.Consume({{itemName}});
                }
            }
            """;
    }

    private string? RenderSingleSelectDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.Select
            ||stage.Func is not { } selector
            ||selector.RequiresStatementInlining
            ||!selector.CanInlineAssign) {
            return null;
        }

        if(source is RangeRoot range) {
            var rangeStartName = $"__selectRangeStart{this.result}";
            var rangeCountName = $"__selectRangeCount{this.result}";
            var rangeItemName = $"__selectRangeItem{this.result}";
            var rangeSelectedName = $"__selectRangeSelected{this.result}";
            var rangeIndexName = $"__selectRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : [rangeItemName];
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;
            var rangeLocalDeclarations = string.Join("\n",new[]{
                this.HoistedLocalDeclarations(selector),
                this.StaticDelegateLocalDeclarations(selector),
            }.Where(static value => !string.IsNullOrWhiteSpace(value)));

            return $$"""
                {{rangeLocalDeclarations}}
                var {{rangeStartName}}={{range.StartExpression}};
                var {{rangeCountName}}={{range.CountExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}{{stage.OutputType}} {{rangeSelectedName}}=default!;
                {{IndentLines(this.FuncAssign(selector,nameof(PipelineStageKind.Select),rangeSelectedName,rangeArguments),"    ")}}
                    {{consumerName}}.Consume({{rangeSelectedName}});
                }
                """;
        }

        var sourceName = $"__selectSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__selectSpan{this.result}";
        var itemName = $"__selectItem{this.result}";
        var selectedName = $"__selectSelected{this.result}";
        var indexName = $"__selectIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : [itemName];
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(selector),
            this.StaticDelegateLocalDeclarations(selector),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));

        return $$"""
            {{localDeclarations}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{indexIncrement}}{{stage.OutputType}} {{selectedName}}=default!;
            {{IndentLines(this.FuncAssign(selector,nameof(PipelineStageKind.Select),selectedName,arguments),"    ")}}
                {{consumerName}}.Consume({{selectedName}});
            }
            """;
    }

    private string? RenderSingleTakeWhileDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.TakeWhile
            ||stage.Func is not { } predicate
            ||predicate.RequiresStatementInlining
            ||!predicate.CanInlineAssign) {
            return null;
        }

        var sourceName = $"__takeWhileSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__takeWhileSpan{this.result}";
        var itemName = $"__takeWhileItem{this.result}";
        var indexName = $"__takeWhileIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : [itemName];
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (preCode,conditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.TakeWhile),this.temp,arguments);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if(!{WrapConditionExpression(conditionExpr)})"
            : $"{preCode}\n        if(!{WrapConditionExpression(conditionExpr)})";

        return $$"""
            {{localDeclarationBlock}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                var {{itemName}}={{spanName}}[__i{{this.result}}];
                {{indexIncrement}}{{predicateCheck}} {
                    break;
                }
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }

    private string? RenderSingleSkipWhileDirectConsume(string consumerName) {
        if(stages.Count!=1) {
            return null;
        }

        var stage = stages[0];
        if(stage.Kind!=PipelineStageKind.SkipWhile
            ||stage.Func is not { } predicate
            ||predicate.RequiresStatementInlining
            ||!predicate.CanInlineAssign) {
            return null;
        }

        var sourceName = $"__skipWhileSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__skipWhileSpan{this.result}";
        var itemName = $"__skipWhileItem{this.result}";
        var indexName = $"__skipWhileIndex{this.result}";
        var offsetName = $"__skipWhileOffset{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : [itemName];
        var indexDeclaration = stage.UsesIndex
            ? $"var {indexName}=-1;\n"
            : string.Empty;
        var indexIncrement = stage.UsesIndex
            ? $"{indexName}++;\n        "
            : string.Empty;
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (preCode,conditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.SkipWhile),this.temp,arguments);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if(!{WrapConditionExpression(conditionExpr)})"
            : $"{preCode}\n            if(!{WrapConditionExpression(conditionExpr)})";

        if(stage.UsesIndex) {
            var skippingName = $"__skipWhileSkipping{this.result}";
            return $$"""
                {{localDeclarationBlock}}
                using var {{sourceName}}={{source.GetEnumeratorExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var {{spanName}}={{directSpanExpression}};
                var {{skippingName}}=true;
                {{indexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{spanName}}.Length;__i{{this.result}}++){
                    var {{itemName}}={{spanName}}[__i{{this.result}}];
                    {{indexIncrement}}if({{skippingName}}){
                        {{predicateCheck}} {
                            {{skippingName}}=false;
                        }
                    }
                    if(!{{skippingName}}){
                        {{consumerName}}.Consume({{itemName}});
                    }
                }
                """;
        }

        return $$"""
            {{localDeclarationBlock}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            var {{offsetName}}=0;
            {{indexDeclaration}}for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{indexIncrement}}{{predicateCheck}} {
                    {{consumerName}}.Consume({{itemName}});
                    {{offsetName}}++;
                    break;
                }
            }
            for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }

    private string? RenderPureOrderingDirectConsume(string consumerName) {
        var directOrderStages = this.OrderStages(0).ToArray();
        if(directOrderStages.Length>0) {
            var primaryOrder = directOrderStages[0];
            if(this.CanUseSortedDefaultOrderDirectConsumeFastPath(0,primaryOrder,directOrderStages)) {
                var sourceName = $"__orderedSource{番号}";
                var fallbackName = $"__orderedFallback{番号}";
                var spanName = $"__orderedSpan{番号}";
                var sortedName = $"__orderedSorted{番号}";
                var handledName = $"__orderedHandled{番号}";
                var directItemName = $"__orderedItem{番号}";
                var directConsumeLoop = primaryOrder.Descending
                    ? $$"""
                            var __groupEnd{{番号}}={{spanName}}.Length;
                            var __orderComparer{{番号}}=global::System.Collections.Generic.Comparer<{{primaryOrder.InputType}}>.Default;
                            while(__groupEnd{{番号}}>0){
                                var __groupStart{{番号}}=__groupEnd{{番号}}-1;
                                while(__groupStart{{番号}}>0&&__orderComparer{{番号}}.Compare({{spanName}}[__groupStart{{番号}}-1],{{spanName}}[__groupEnd{{番号}}-1])==0){
                                    __groupStart{{番号}}--;
                                }
                                for(var __i=__groupStart{{番号}};__i<__groupEnd{{番号}};__i++){
                                    var {{directItemName}}={{spanName}}[__i];
                                    {{consumerName}}.Consume({{directItemName}});
                                }
                                __groupEnd{{番号}}=__groupStart{{番号}};
                            }
                        """
                    : $$"""
                            for(var __i=0;__i<{{spanName}}.Length;__i++){
                                var {{directItemName}}={{spanName}}[__i];
                                {{consumerName}}.Consume({{directItemName}});
                            }
                        """;
                return $$"""
                    var {{handledName}}=false;
                    using var {{sourceName}}={{source.GetEnumeratorExpression}};
                    if({{sourceName}}.TryGetSpan(out var {{spanName}})){
                        var {{sortedName}}=true;
                        for(var __i=1;__i<{{spanName}}.Length;__i++){
                            if({{this.SortedOrderOutOfOrderExpression(primaryOrder,spanName,"__i")}}){
                                {{sortedName}}=false;
                                break;
                            }
                        }
                        if({{sortedName}}){
                    {{directConsumeLoop}}
                            {{handledName}}=true;
                        }
                    }
                    if(!{{handledName}}){
                        using var {{fallbackName}}={{this.GetEnumeratorExpression}};
                        while({{fallbackName}}.TryGetNext(out var {{directItemName}})){
                            {{consumerName}}.Consume({{directItemName}});
                        }
                    }
                    """;
            }
        }

        if(stages.Count==0
            ||!stages.All(static stage => stage.Kind is PipelineStageKind.OrderBy
                or PipelineStageKind.OrderByDescending
                or PipelineStageKind.ThenBy
                or PipelineStageKind.ThenByDescending)
            ||stages[0].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
            ||source.DirectEnumerableExpression is not { } sourceExpression) {
            return null;
        }

        var expression = sourceExpression;
        foreach(var stage in stages) {
            var methodName = stage.Kind.ToString();
            string argumentList;
            if(stage.Func is { } func) {
                if(TryRenderDirectLambda(func) is not { } lambda) {
                    return null;
                }
                var comparerExpression = this.OrderComparerExpression(stage);
                argumentList=stage.Value=="null"
                    ? lambda
                    : $"{lambda}, {comparerExpression}";
            } else {
                methodName=stage.Kind switch {
                    PipelineStageKind.OrderBy => "Order",
                    PipelineStageKind.OrderByDescending => "OrderDescending",
                    _ => methodName
                };
                var comparerExpression = this.OrderComparerExpression(stage);
                argumentList=stage.Value=="null"
                    ? string.Empty
                    : comparerExpression;
            }
            expression=string.IsNullOrEmpty(argumentList)
                ? $"global::System.Linq.Enumerable.{methodName}({expression})"
                : $"global::System.Linq.Enumerable.{methodName}({expression}, {argumentList})";
        }

        var itemName = $"__orderedItem{番号}";
        return $$"""
            foreach(var {{itemName}} in {{expression}}){
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }

    private static string? TryRenderDirectLambda(FuncFragment func) {
        if(func.CapturedLocals.Length!=0||func.HoistedLocals.Length!=0||func.RequiresStatementInlining) {
            return null;
        }
        try {
            return func.RenderAsLambda();
        } catch(InvalidOperationException) {
            return null;
        }
    }
}
