namespace Seamless.FLinq.Linq.Fragments;

/*
PipelineEnumerable は Where/Select/Take/OrderBy など複数 stage を1つの enumerator 状態機械へ畳む。

入力例:
  source.Where(p).Select(s).Take(n)

生成例:
  while(source.TryGetNext(out var current0)) {
      if(!p(current0)) continue;
      var current1 = s(current0);
      if(takeCountReached) return false;
      current = current1;
      return true;
  }
  return false;

主な stage 分岐:
  - Where/Select/AsEnumerable/Cast は直列 loop 内の軽い変換として展開する。
  - SelectMany/GroupBy/Reverse/OrderBy/TakeLast などは stage 専用の buffer/state field を追加する。
  - Union/Except/Intersect 系 stage は二入力集合演算として BuildBinarySetLoop に切り替える。

設計意図:
  個別 Fragment を深くネストすると enumerator struct が段数分重なる。Pipeline は stage list を見て
  1つの Struct に state field と loop 分岐を集め、CountPreserving な場合は TryCopyTo もまとめて生成する。
*/
internal sealed class PipelineEnumerable(int 番号,Enumerable source,IReadOnlyList<PipelineStage> stages,string elementType):Enumerable(番号) {
    private bool StagesCountPreserving => stages.All(static stage => stage.Kind is PipelineStageKind.Select or PipelineStageKind.AsEnumerable or PipelineStageKind.Cast);
    private bool SingleDefaultIfEmpty => stages.Count==1&&stages[0].Kind==PipelineStageKind.DefaultIfEmpty;
    protected override Enumerable PrimarySource => source;
    public Enumerable Source => source;
    public IReadOnlyList<PipelineStage> Stages => stages;
    public override string ElementType => elementType;
    public override string? NonEnumeratedCountExpression => this.StagesCountPreserving ? source.NonEnumeratedCountExpression : null;
    public override bool CountPreserving => source.CountPreserving&&this.StagesCountPreserving;
    public override bool CanTryProvideSpan => this.SingleDefaultIfEmpty&&source.CanTryProvideSpan;
    private void AppendCounterCall(StringBuilder builder,string indent,string methodName) {
        var call = this.CounterCall(methodName);
        if(!string.IsNullOrEmpty(call)) {
            builder.AppendLine($"{indent}{call}");
        }
    }
    public override string StructMethod {
        get {
            var result = string.Empty;
            foreach(var source in this.Funcs.Select(static f => f.SupportingMemberSources)) {
                result=JoinStaticMethods(result,source);
            }
            foreach(var source in this.SecondSources.Select(static source => source.StructMethod)) {
                result=JoinStaticMethods(result,source);
            }
            return result;
        }
    }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.SecondSourcesStructs}}
        {{base.GetEnumeratorStruct}}
        """;
    protected override string StructTypeParameters {
        get {
            var secondSources = this.SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.StructTypeParameters;
            }
            var parameters = new List<string> { "TEnum" };
            for(var index = 0;index<secondSources.Length;index++) {
                parameters.Add(SecondTypeParameterName(index));
            }
            return $"<{string.Join(", ",parameters)}>";
        }
    }
    protected override string StructTypeConstraints {
        get {
            var constraints = new List<string> { $"where TEnum : struct, global::Seamless.Linq.IValueEnumerator<{source.ElementType}>" };
            foreach(var (second,index) in this.SecondSources.Select(static (second,index) => (second,index))) {
                constraints.Add($"where {SecondTypeParameterName(index)} : struct, global::Seamless.Linq.IValueEnumerator<{second.ElementType}>");
            }
            return string.Join("\n    ",constraints);
        }
    }
    public override string EnumeratorTypeName {
        get {
            var secondSources = this.SecondSources.ToArray();
            if(secondSources.Length==0) {
                return base.EnumeratorTypeName;
            }
            var arguments = new List<string> { source.EnumeratorTypeName };
            arguments.AddRange(secondSources.Select(static second => second.EnumeratorTypeName));
            return $"{this.Struct}<{string.Join(", ",arguments)}>";
        }
    }
    public override string GetEnumeratorExpression {
        get {
            var args = new List<string> { source.GetEnumeratorExpression };
            foreach(var second in this.SecondSources) {
                args.Add(second.GetEnumeratorExpression);
            }
            foreach(var stage in this.CountStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.RangeStages()) {
                args.Add(stage.Value!);
            }
            foreach(var stage in this.ComparerStages()) {
                args.Add(this.EqualityComparerExpression(stage));
            }
            foreach(var stage in this.OrderComparerStages()) {
                args.Add(this.OrderComparerExpression(stage));
            }
            args.AddRange(this.ClosureArgsWithoutPrefix());
            return $"new {this.EnumeratorTypeName}({string.Join(", ",args)})";
        }
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
            parameters.AddRange(this.ClosureParametersWithoutPrefix());
            return string.Join(", ",parameters);
        }
    }
    public override string GetEnumeratorFields {
        get {
            var builder = new StringBuilder();
            var countIndex = 0;
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind is PipelineStageKind.Where or PipelineStageKind.Select&&stage.UsesIndex) {
                    builder.AppendLine($"private int {this.StateName(index)} = -1;");
                } else if(stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(countIndex++);
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
                    builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SecondSetName(index)};");
                    builder.AppendLine($"private global::Seamless.Linq.HashSetSlim<{ComparerKeyType(stage)}> {this.SetName(index)};");
                    builder.AppendLine($"private bool {this.StateName(index)} = false;");
                } else if(stage.Kind is PipelineStageKind.Concat or PipelineStageKind.Append or PipelineStageKind.Prepend) {
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine(this.OrderIndexComparerType(index));
                    builder.AppendLine($"private {stage.InputType}[]? {this.OrderValuesName(index)} = null;");
                    builder.AppendLine($"private int[]? {this.OrderIndexesName(index)} = null;");
                    builder.AppendLine($"private int {this.OrderCountName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.GroupBy) {
                    builder.AppendLine(this.GroupingType(index,stage));
                    builder.AppendLine($"private global::System.Collections.Generic.List<{this.GroupValueType(index,stage)}>? {this.GroupItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.Reverse) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {this.ReverseItemsName(index)} = null;");
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
                    builder.AppendLine($"private global::System.Collections.Generic.Queue<{stage.OutputType}> {this.QueueName(index)} = new();");
                } else if(stage.Kind==PipelineStageKind.TakeLast) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                } else if(stage.Kind==PipelineStageKind.TakeRange) {
                    builder.AppendLine($"private global::System.Collections.Generic.List<{stage.OutputType}>? {this.ReverseItemsName(index)} = null;");
                    builder.AppendLine($"private int {this.StateName(index)} = 0;");
                    builder.AppendLine($"private int {this.StateEndName(index)} = 0;");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetEnumeratorConstructor {
        get {
            var builder = new StringBuilder();
            var countIndex = 0;
            for(var index = 0;index<stages.Count;index++) {
                var stage = stages[index];
                if(stage.Kind==PipelineStageKind.Take||stage.Kind==PipelineStageKind.Skip) {
                    var countName = this.CountName(countIndex++);
                    builder.AppendLine($"this.{countName}Value = global::System.Math.Max(0, {countName});");
                } else if(stage.Kind is PipelineStageKind.Distinct or PipelineStageKind.DistinctBy) {
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Union or PipelineStageKind.UnionBy) {
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                } else if(stage.Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"this.{this.SecondSetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                    builder.AppendLine($"this.{this.SetName(index)} = new({this.ComparerName(this.ComparerOrdinal(index))});");
                }
            }
            return builder.ToString().TrimEnd();
        }
    }
    public override string GetTryGetNextBody => this.BuildLoop(this.e,"current=this.Current={0};\nreturn true;","current=default!;\nreturn false;");
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
            return this.StagesCountPreserving
                ? $"return this.{this.e}.TryGetNonEnumeratedCount(out count);"
                : base.GetTryGetNonEnumeratedCountBody;
        }
    }
    protected override string GetTryGetSpanBody {
        get {
            if(this.SingleDefaultIfEmpty) {
                return $$"""
                    if(this.{{this.StateName(0)}}){
                        return this.{{this.e}}.TryGetSpan(out span);
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
                        return this.{{this.e}}.TryCopyTo(destination,__offset);
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
                    builder.AppendLine($"this.{this.SetName(index)}.Dispose();");
                } else if(stages[index].Kind is PipelineStageKind.Except or PipelineStageKind.Intersect or PipelineStageKind.ExceptBy or PipelineStageKind.IntersectBy) {
                    builder.AppendLine($"this.{this.SecondSetName(index)}.Dispose();");
                    builder.AppendLine($"this.{this.SetName(index)}.Dispose();");
                } else if(stages[index].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                    builder.AppendLine($"if(this.{this.OrderValuesName(index)} is not null && this.{this.OrderValuesName(index)}.Length!=0) {{");
                    builder.AppendLine($"    global::System.Buffers.ArrayPool<{stages[index].InputType}>.Shared.Return(this.{this.OrderValuesName(index)},clearArray:global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{stages[index].InputType}>());");
                    builder.AppendLine($"    this.{this.OrderValuesName(index)}=null;");
                    builder.AppendLine("}");
                    builder.AppendLine($"if(this.{this.OrderIndexesName(index)} is not null && this.{this.OrderIndexesName(index)}.Length!=0) {{");
                    builder.AppendLine($"    global::System.Buffers.ArrayPool<int>.Shared.Return(this.{this.OrderIndexesName(index)},clearArray:false);");
                    builder.AppendLine($"    this.{this.OrderIndexesName(index)}=null;");
                    builder.AppendLine("}");
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

    private IEnumerable<FuncFragment> Funcs => stages.SelectMany(static stage => new[] { stage.Func,stage.SecondFunc,stage.ThirdFunc }).Where(static func => func is not null).Cast<FuncFragment>();
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
                return this.BuildBinarySetOrderLoop(enumeratorName,successStatement,endStatement,binarySetIndex[0],orderIndex[^1]);
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
            // 連続した OrderBy チェーン: 最後の OrderBy が前の順序を完全に置き換える（LINQ の仕様通り）
            return this.BuildOrderLoop(enumeratorName,successStatement,endStatement,orderIndex[^1]);
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
        builder.AppendLine($"{fallbackIndent}global::System.Array.Sort({indexesLocal},0,{countLocal},new {this.OrderIndexComparerName(orderIndex)}({string.Join(",",comparerArgs)}));");
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
        // GroupBy バッファ収集後、後続に OrderBy/ThenBy があればソートして融合する
        var orderAfterGroup = -1;
        for(var checkIdx = groupByIndex+1;checkIdx<stages.Count;checkIdx++) {
            if(stages[checkIdx].Kind is PipelineStageKind.OrderBy or PipelineStageKind.OrderByDescending) {
                orderAfterGroup=checkIdx;
                break;
            }
        }
        if(orderAfterGroup>=0) {
            var primaryOrder = stages[orderAfterGroup];
            var thenByIdx = orderAfterGroup+1<stages.Count
                &&stages[orderAfterGroup+1].Kind is PipelineStageKind.ThenBy or PipelineStageKind.ThenByDescending
                ? (int?)(orderAfterGroup+1) : null;
            var primaryCmpLocal = this.OrderComparerLocalName(orderAfterGroup);
            builder.AppendLine($"    var {primaryCmpLocal}={this.OrderComparerName(this.OrderComparerOrdinal(orderAfterGroup))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(primaryOrder)}>.Default;");
            if(thenByIdx.HasValue) {
                var thenStage = stages[thenByIdx.Value];
                var secondaryCmpLocal = this.OrderComparerLocalName(thenByIdx.Value);
                builder.AppendLine($"    var {secondaryCmpLocal}={this.OrderComparerName(this.OrderComparerOrdinal(thenByIdx.Value))} ?? global::System.Collections.Generic.Comparer<{ComparerKeyType(thenStage)}>.Default;");
            }
            builder.AppendLine($"    {this.GroupItemsName(groupByIndex)}.Sort((left,right) => {{");
            var leftPrimKey = $"__lpk{orderAfterGroup}";
            var rightPrimKey = $"__rpk{orderAfterGroup}";
            builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {leftPrimKey}=default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"        {leftPrimKey}=left;");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),leftPrimKey,"left"),"        "));
            }
            builder.AppendLine($"        {ComparerKeyType(primaryOrder)} {rightPrimKey}=default!;");
            if(primaryOrder.Func is null) {
                builder.AppendLine($"        {rightPrimKey}=right;");
            } else {
                builder.AppendLine(IndentBlock(this.FuncAssign(primaryOrder.Func,primaryOrder.Kind.ToString(),rightPrimKey,"right"),"        "));
            }
            builder.AppendLine($"        var __gcmp={primaryCmpLocal}.Compare({leftPrimKey},{rightPrimKey});");
            builder.AppendLine($"        if(__gcmp!=0) return {this.OrderCompareExpression(primaryOrder,"__gcmp")};");
            if(thenByIdx.HasValue) {
                var thenStage = stages[thenByIdx.Value];
                var secondaryCmpLocal = this.OrderComparerLocalName(thenByIdx.Value);
                var leftSecKey = $"__lsk{thenByIdx.Value}";
                var rightSecKey = $"__rsk{thenByIdx.Value}";
                builder.AppendLine($"        {ComparerKeyType(thenStage)} {leftSecKey}=default!;");
                builder.AppendLine(IndentBlock(this.FuncAssign(thenStage.Func!,thenStage.Kind.ToString(),leftSecKey,"left"),"        "));
                builder.AppendLine($"        {ComparerKeyType(thenStage)} {rightSecKey}=default!;");
                builder.AppendLine(IndentBlock(this.FuncAssign(thenStage.Func!,thenStage.Kind.ToString(),rightSecKey,"right"),"        "));
                builder.AppendLine($"        var __gcmp2={secondaryCmpLocal}.Compare({leftSecKey},{rightSecKey});");
                builder.AppendLine($"        if(__gcmp2!=0) return {this.OrderCompareExpression(thenStage,"__gcmp2")};");
            }
            builder.AppendLine("        return 0;");
            builder.AppendLine("    });");
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
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.ReverseItemsName(reverseIndex)} is null) {{");
        builder.AppendLine($"    var {this.ReverseItemsName(reverseIndex)}=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<reverseIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        {this.ReverseItemsName(reverseIndex)}.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    {this.ReverseItemsName(reverseIndex)}.Reverse();");
        builder.AppendLine($"    this.{this.ReverseItemsName(reverseIndex)}={this.ReverseItemsName(reverseIndex)};");
        builder.AppendLine($"    this.{this.StateName(reverseIndex)}=0;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(reverseIndex)}<this.{this.ReverseItemsName(reverseIndex)}.Count) {{");
        currentName=this.ItemName(reverseIndex+1);
        builder.AppendLine($"    var {currentName}=this.{this.ReverseItemsName(reverseIndex)}[this.{this.StateName(reverseIndex)}++];");
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
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.ReverseItemsName(takeLastIndex)} is null) {{");
        builder.AppendLine($"    var __tmpList=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<takeLastIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        __tmpList.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    var __takeN=global::System.Math.Max(0,global::System.Math.Min(global::System.Math.Max(0,{this.CountName(countOrd)}),__tmpList.Count));");
        builder.AppendLine($"    var __takeStart=__tmpList.Count-__takeN;");
        builder.AppendLine($"    this.{this.ReverseItemsName(takeLastIndex)}=__tmpList;");
        builder.AppendLine($"    this.{this.StateName(takeLastIndex)}=__takeStart;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(takeLastIndex)}<this.{this.ReverseItemsName(takeLastIndex)}.Count) {{");
        currentName=this.ItemName(takeLastIndex+1);
        builder.AppendLine($"    var {currentName}=this.{this.ReverseItemsName(takeLastIndex)}[this.{this.StateName(takeLastIndex)}++];");
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
        var builder = new StringBuilder();
        builder.AppendLine($"if(this.{this.ReverseItemsName(takeRangeIndex)} is null) {{");
        builder.AppendLine($"    var __tmpRange=new global::System.Collections.Generic.List<{stage.OutputType}>();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out var {this.ItemName(0)})) {{");
        this.AppendCounterCall(builder,"        ","IncrementPhysicalMoveNext");
        var currentName = this.ItemName(0);
        for(var index = 0;index<takeRangeIndex;index++) {
            currentName=this.AppendStage(builder,stages[index],currentName,index);
        }
        builder.AppendLine($"        __tmpRange.Add({currentName});");
        builder.AppendLine("    }");
        builder.AppendLine($"    var (__rangeOff,__rangeLen)={rangeParamName}.GetOffsetAndLength(__tmpRange.Count);");
        builder.AppendLine($"    this.{this.ReverseItemsName(takeRangeIndex)}=__tmpRange;");
        builder.AppendLine($"    this.{this.StateName(takeRangeIndex)}=__rangeOff;");
        builder.AppendLine($"    this.{this.StateEndName(takeRangeIndex)}=__rangeOff+__rangeLen;");
        builder.AppendLine("}");
        builder.AppendLine($"while(this.{this.StateName(takeRangeIndex)}<this.{this.StateEndName(takeRangeIndex)}) {{");
        currentName=this.ItemName(takeRangeIndex+1);
        builder.AppendLine($"    var {currentName}=this.{this.ReverseItemsName(takeRangeIndex)}[this.{this.StateName(takeRangeIndex)}++];");
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
            secondKeyName=this.KeyName(unionIndex+1);
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
            secondKeyName=this.KeyName(unionIndex+1);
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
            selectorArguments= [currentName,$"this.{this.StateName(selectManyIndex)}"];
        } else {
            selectorArguments= [currentName];
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
            arguments= [currentName,$"this.{this.StateName(index)}"];
        } else {
            arguments= [currentName];
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
            arguments= [currentName,$"this.{this.StateName(index)}"];
        } else {
            arguments= [currentName];
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
            arguments= [currentName,$"this.{this.IndexName(index)}"];
        } else {
            arguments= [currentName];
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
            arguments= [currentName,$"this.{this.IndexName(index)}"];
        } else {
            arguments= [currentName];
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
        builder.AppendLine($"    if({currentName} is not {stage.OutputType} {nextName}) continue;");
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
