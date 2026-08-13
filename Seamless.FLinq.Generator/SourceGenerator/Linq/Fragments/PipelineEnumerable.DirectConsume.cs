namespace Seamless.FLinq;

// 単段パイプラインを Consume へ直接展開する経路。
internal sealed partial class PipelineEnumerable {
    public override string GetConsumePreparationStatement() {
        if(source is not RangeRoot range||stages.Count!=1) {
            return base.GetConsumePreparationStatement();
        }

        var stage = stages[0];
        if(stage.Kind is not (PipelineStageKind.Select or PipelineStageKind.Where or PipelineStageKind.SkipWhile)
            ||stage.Func is not { } function
            ||function.RequiresStatementInlining
            ||!function.CanInlineAssign) {
            return base.GetConsumePreparationStatement();
        }

        var functionDeclarations = string.Join("\n",new[] {
            this.HoistedLocalDeclarations(function),
            this.StaticDelegateLocalDeclarations(function),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(functionDeclarations)
            ? range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)
            : $$"""
                {{range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)}}
                {{functionDeclarations}}
                """;
    }

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

        if(source is RangeRoot range) {
            var rangeStartName = $"__appendRangeStart{this.result}";
            var rangeCountName = $"__appendRangeCount{this.result}";
            var rangeItemName = $"__appendRangeItem{this.result}";
            var rangeAppendValue = $"{consumerName}.Consume({value});";
            var rangeBefore = stage.Kind==PipelineStageKind.Prepend ? rangeAppendValue+"\n" : string.Empty;
            var rangeAfter = stage.Kind==PipelineStageKind.Append ? "\n"+rangeAppendValue : string.Empty;
            return $$"""
                var {{rangeStartName}}={{range.StartExpression}};
                var {{rangeCountName}}={{range.CountExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeBefore}}for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                    {{consumerName}}.Consume({{rangeItemName}});
                }{{rangeAfter}}
                """;
        }

        var sourceName = $"__appendSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__appendSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__appendItem{this.result}";
        var appendValue = $"{consumerName}.Consume({value});";
        var before = stage.Kind==PipelineStageKind.Prepend ? appendValue+"\n" : string.Empty;
        var after = stage.Kind==PipelineStageKind.Append ? "\n"+appendValue : string.Empty;
        return $$"""
            {{directSpanSetup}}
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

        if(source is RangeRoot range) {
            var rangeStartName = $"__defaultRangeStart{this.result}";
            var rangeCountName = $"__defaultRangeCount{this.result}";
            var rangeItemName = $"__defaultRangeItem{this.result}";
            return $$"""
                var {{rangeStartName}}={{range.StartExpression}};
                var {{rangeCountName}}={{range.CountExpression}};
                if({{rangeCountName}}==0){
                    {{consumerName}}.Consume({{defaultValue}});
                }else{
                    global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                    for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                        var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                        {{consumerName}}.Consume({{rangeItemName}});
                    }
                }
                """;
        }

        if(source is Root { DirectEnumerableExpression: { } directEnumerable }) {
            return $$"""
                {
                    var __defaultIfEmptySource={{directEnumerable}};
                    var __defaultIfEmptyValue={{defaultValue}};
                    global::System.ArgumentNullException.ThrowIfNull(__defaultIfEmptySource);
                    using(var __defaultIfEmptyEnumerator=__defaultIfEmptySource.GetEnumerator()){
                        if(__defaultIfEmptyEnumerator.MoveNext()){
                            do{
                                {{consumerName}}.Consume(__defaultIfEmptyEnumerator.Current);
                            }while(__defaultIfEmptyEnumerator.MoveNext());
                        }else{
                            {{consumerName}}.Consume(__defaultIfEmptyValue);
                        }
                    }
                }
                """;
        }
        var sourceName = $"__defaultSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__defaultSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__defaultItem{this.result}";
        return $$"""
            {{directSpanSetup}}
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
        this.AppendConcatEnumeratorDeclaration(builder,source,sourceName);
        this.AppendConcatEnumeratorDeclaration(builder,secondSource,secondName);
        this.AppendConcatConsumeSource(builder,source,sourceName,consumerName,$"__concatLeftSpan{this.result}",$"__concatLeftItem{this.result}",source.ElementType);
        this.AppendConcatConsumeSource(builder,secondSource,secondName,consumerName,$"__concatRightSpan{this.result}",$"__concatRightItem{this.result}",stage.OutputType);
        return builder.ToString().TrimEnd();
    }

    // RangeRoot は start+i で値を計算できるので enumerator の構築自体が要らない。
    private void AppendConcatEnumeratorDeclaration(StringBuilder builder,Enumerable sourceFragment,string enumeratorName) {
        if(sourceFragment is RangeRoot) {
            return;
        }
        builder.AppendLine($"using var {enumeratorName}={sourceFragment.GetEnumeratorExpression};");
    }

    private void AppendConcatConsumeSource(StringBuilder builder,Enumerable sourceFragment,string enumeratorName,string consumerName,string spanName,string itemName,string outputType) {
        if(sourceFragment is RangeRoot range) {
            var rangeStartName = $"{itemName}Start";
            var rangeCountName = $"{itemName}Count";
            builder.AppendLine($"var {rangeStartName}={range.StartExpression};");
            builder.AppendLine($"var {rangeCountName}={range.CountExpression};");
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();");
            builder.AppendLine($"for(var __i{this.result}=0;__i{this.result}<{rangeCountName};__i{this.result}++) {{");
            builder.AppendLine($"    var {itemName}={rangeStartName}+__i{this.result};");
            builder.AppendLine($"    {consumerName}.Consume({this.CastConcatItem(sourceFragment.ElementType,outputType,itemName)});");
            builder.AppendLine("}");
            return;
        }

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

        if(source is RangeRoot range) {
            var rangeItemName = $"__whereRangeItem{this.result}";
            var rangeIndexName = $"__whereRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : new[] { rangeItemName };
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;
            var (rangePreCode,rangeConditionExpr)=this.FuncInvokeCondition(predicate,nameof(PipelineStageKind.Where),this.temp,rangeArguments);
            var rangePredicateCheck = string.IsNullOrEmpty(rangePreCode)
                ? $"if({WrapConditionExpression(rangeConditionExpr)})"
                : $"{rangePreCode}\n    if({WrapConditionExpression(rangeConditionExpr)})";

            return $$"""
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{range.PreparedCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{range.PreparedStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}{{rangePredicateCheck}} {
                        {{consumerName}}.Consume({{rangeItemName}});
                    }
                }
                """;
        }

        var sourceName = $"__whereSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__whereSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__whereItem{this.result}";
        var indexName = $"__whereIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
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
            {{directSpanSetup}}
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
            var rangeItemName = $"__selectRangeItem{this.result}";
            var rangeSelectedName = $"__selectRangeSelected{this.result}";
            var rangeIndexName = $"__selectRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : new[] { rangeItemName };
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;

            return $$"""
                {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{range.PreparedCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{range.PreparedStartName}}+__i{{this.result}};
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
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__selectItem{this.result}";
        var selectedName = $"__selectSelected{this.result}";
        var indexName = $"__selectIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
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
            {{directSpanSetup}}
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

        if(source is RangeRoot range) {
            var rangeStartName = $"__takeWhileRangeStart{this.result}";
            var rangeCountName = $"__takeWhileRangeCount{this.result}";
            var rangeItemName = $"__takeWhileRangeItem{this.result}";
            var rangeIndexName = $"__takeWhileRangeIndex{this.result}";
            var rangeArguments = stage.UsesIndex
                ? new[] { rangeItemName,rangeIndexName }
                : new[] { rangeItemName };
            var rangeIndexDeclaration = stage.UsesIndex
                ? $"var {rangeIndexName}=-1;\n"
                : string.Empty;
            var rangeIndexIncrement = stage.UsesIndex
                ? $"{rangeIndexName}++;\n    "
                : string.Empty;
            var rangeLocalDeclarations = string.Join("\n",new[]{
                this.HoistedLocalDeclarations(predicate),
                this.StaticDelegateLocalDeclarations(predicate),
            }.Where(static text => !string.IsNullOrEmpty(text)));
            var rangeLocalDeclarationBlock = string.IsNullOrEmpty(rangeLocalDeclarations)
                ? string.Empty
                : rangeLocalDeclarations+"\n";
            var (rangePreCode,rangeConditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.TakeWhile),this.temp,rangeArguments);
            var rangePredicateCheck = string.IsNullOrEmpty(rangePreCode)
                ? $"if(!{WrapConditionExpression(rangeConditionExpr)})"
                : $"{rangePreCode}\n    if(!{WrapConditionExpression(rangeConditionExpr)})";

            return $$"""
                {{rangeLocalDeclarationBlock}}
                var {{rangeStartName}}={{range.StartExpression}};
                var {{rangeCountName}}={{range.CountExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{rangeCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{rangeStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}{{rangePredicateCheck}} {
                        break;
                    }
                    {{consumerName}}.Consume({{rangeItemName}});
                }
                """;
        }

        var sourceName = $"__takeWhileSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__takeWhileSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__takeWhileItem{this.result}";
        var indexName = $"__takeWhileIndex{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
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
            {{directSpanSetup}}
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

        if(source is RangeRoot range) {
            return this.RenderSkipWhileRangeDirectConsume(consumerName,range,stage,predicate);
        }

        var sourceName = $"__skipWhileSource{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }

        var spanName = $"__skipWhileSpan{this.result}";
    var directSpanSetup = new DirectConsumeSourceRenderModel(sourceName,source.GetEnumeratorExpression,spanName,directSpanExpression).RenderSetup();
        var itemName = $"__skipWhileItem{this.result}";
        var indexName = $"__skipWhileIndex{this.result}";
        var offsetName = $"__skipWhileOffset{this.result}";
        var arguments = stage.UsesIndex
            ? new[] { itemName,indexName }
            : new[] { itemName };
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
            {{directSpanSetup}}
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

    // RangeRoot は start+i で値を計算できるので、SkipWhile も状態機械を経由せず直列 for ループにできる。
    private string RenderSkipWhileRangeDirectConsume(string consumerName,RangeRoot range,PipelineStage stage,FuncFragment predicate) {
        var rangeItemName = $"__skipWhileRangeItem{this.result}";
        var rangeIndexName = $"__skipWhileRangeIndex{this.result}";
        var rangeOffsetName = $"__skipWhileRangeOffset{this.result}";
        var rangeArguments = stage.UsesIndex
            ? new[] { rangeItemName,rangeIndexName }
            : new[] { rangeItemName };
        var rangeIndexDeclaration = stage.UsesIndex
            ? $"var {rangeIndexName}=-1;\n"
            : string.Empty;
        var rangeIndexIncrement = stage.UsesIndex
            ? $"{rangeIndexName}++;\n        "
            : string.Empty;
        var (rangePreCode,rangeConditionExpr)=this.FuncInvokeConditionStatic(predicate,nameof(PipelineStageKind.SkipWhile),this.temp,rangeArguments);
        var rangePredicateCheck = string.IsNullOrEmpty(rangePreCode)
            ? $"if(!{WrapConditionExpression(rangeConditionExpr)})"
            : $"{rangePreCode}\n            if(!{WrapConditionExpression(rangeConditionExpr)})";

        if(stage.UsesIndex) {
            var rangeSkippingName = $"__skipWhileRangeSkipping{this.result}";
            return $$"""
                {{this.CounterCall("IncrementEnumerationLoopUsed")}}
                var {{rangeSkippingName}}=true;
                {{rangeIndexDeclaration}}for(var __i{{this.result}}=0;__i{{this.result}}<{{range.PreparedCountName}};__i{{this.result}}++){
                    var {{rangeItemName}}={{range.PreparedStartName}}+__i{{this.result}};
                    {{rangeIndexIncrement}}if({{rangeSkippingName}}){
                        {{rangePredicateCheck}} {
                            {{rangeSkippingName}}=false;
                        }
                    }
                    if(!{{rangeSkippingName}}){
                        {{consumerName}}.Consume({{rangeItemName}});
                    }
                }
                """;
        }

        return $$"""
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            var {{rangeOffsetName}}=0;
            {{rangeIndexDeclaration}}for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{rangeIndexIncrement}}{{rangePredicateCheck}} {
                    {{consumerName}}.Consume({{rangeItemName}});
                    {{rangeOffsetName}}++;
                    break;
                }
            }
            for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{consumerName}}.Consume({{rangeItemName}});
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

        // 全段が Order 系のときに System.Linq.Enumerable.OrderBy(...) へ丸ごと委譲していた経路は撤去した。
        // 委譲は「展開が遅い」ことの隠蔽であって解決ではない。FLinq は同じアルゴリズムをインライン展開できる以上、
        // 理論下限は System.Linq と同速であり、負けているなら展開側の欠陥として原因を潰すべきである。
        // 実際、同じ OrderBy(keySelector) を Array/List タブは BuildOrderLoop の ArrayPool 経路で
        // 160B・対 ZLinq 0.87 で通しており、委譲していた Range だけが 49,469B・1.04 で負けていた。
        // 以降は base.GetConsumeExecutionStatement 経由で BuildOrderLoop の ArrayPool 経路へ落とす。
        return null;
    }
}
