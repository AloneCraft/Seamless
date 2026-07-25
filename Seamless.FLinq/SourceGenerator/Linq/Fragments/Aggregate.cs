namespace Seamless.FLinq.Linq.Fragments;
/*
Aggregate_func は seed なし Aggregate(func) を、先頭要素を初期 accumulator にして畳み込む scalar へ変換する。

入力例:
  source.Aggregate((acc, item) => acc + item)

生成例:
  if(!source.TryGetNext(out var item)) throw NoElements();
  var result = item;
  while(source.TryGetNext(out item)) {
      result = func(result, item);
  }
  current = result;

バリエーション:
  - source が TryGetSpan 可能なら span foreach で先頭要素判定を行う。
  - func が delegate 保持対象なら inline direct computation 側で delegate local を用意して Invoke する。
*/
public class Aggregate_func(int 番号,Enumerable source,FuncFragment func):Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType) {
        string srcExpr;
        try { srcExpr=source.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = AggregateLoopSupport.RenderSeedlessFold(source,srcVar,source.ElementType,this.result,"__item",
            (indent,itemName) => this.FuncAssignInlineIndented(indent,func,nameof(func),funcVar,this.result,this.result,itemName));
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                {{fold}}
                {{destVar}}={{this.result}};
            }
            """;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(source.ElementType)}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            {{AggregateLoopSupport.RenderSeedlessFold(source,this.e,source.ElementType,this.result,this.item,
                (indent,itemName) => this.FuncAssignIndented(indent,func,nameof(func),this.result,this.result,itemName))}}
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

/*
Aggregate_seed_func は Aggregate(seed, func) を、seed から始める畳み込みにする。

入力例:
  source.Aggregate(0, (acc, item) => acc + item)

生成例:
  var result = seed;
  while(source.TryGetNext(out var item)) {
      result = func(result, item);
  }
  current = result;

seed なしとの差分:
  空 sequence でも seed を返すため NoElements 例外を出さない。
*/
public class Aggregate_seed_func(int 番号,Enumerable source,string seed,FuncFragment func):Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType) {
        string srcExpr;
        try { srcExpr=source.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = AggregateLoopSupport.RenderSeededFold(source,srcVar,this.result,"__item",
            (indent,itemName) => this.FuncAssignInlineIndented(indent,func,nameof(func),funcVar,this.result,this.result,itemName));
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                var {{this.result}} = {{seed}};
                {{fold}}
                {{destVar}}={{this.result}};
            }
            """;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(FuncParameterTypeName(func,0))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            {{AggregateLoopSupport.RenderSeededFold(source,this.e,this.result,this.item,
                (indent,itemName) => this.FuncAssignIndented(indent,func,nameof(func),this.result,this.result,itemName))}}
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

/*
Aggregate_seed_func_resultSelector は Aggregate(seed, func, resultSelector) を、畳み込み後に final selector を通す。

入力例:
  source.Aggregate(seed, (acc, item) => Step(acc, item), acc => Finish(acc))

生成例:
  var result = seed;
  foreach(var item in source) result = func(result, item);
  current = resultSelector(result);

設計意図:
  func と resultSelector の両方に closure/delegate/statement inline の可能性があるため、
  ClosureArgsSuffix と FuncAssign を2つ分組み合わせて生成する。
*/
public class Aggregate_seed_func_resultSelector(int 番号,Enumerable source,string seed,FuncFragment func,FuncFragment resultSelector):Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType) {
        string srcExpr;
        try { srcExpr=source.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var resultSelectorVar = $"__aggResultSelector{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        var resultSelectorSetup = this.DelegateLocal(resultSelector,resultSelectorVar);
        if(funcSetup is null||resultSelectorSetup is null) return null;
        var fold = AggregateLoopSupport.RenderSeededFold(source,srcVar,this.result,"__item",
            (indent,itemName) => this.FuncAssignInlineIndented(indent,func,nameof(func),funcVar,this.result,this.result,itemName));
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            {{resultSelectorSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                var {{this.result}} = {{seed}};
                {{fold}}
                {{this.FuncAssignInlineIndented("    ",resultSelector,nameof(resultSelector),resultSelectorVar,destVar,this.result)}}
            }
            """;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => $$"""
        {{this.FuncSupport(func,nameof(func))}}
        {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(func,resultSelector)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct(FuncReturnTypeName(resultSelector))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(func,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            {{AggregateLoopSupport.RenderSeededFold(source,this.e,this.result,this.item,
                (indent,itemName) => this.FuncAssignIndented(indent,func,nameof(func),this.result,this.result,itemName))}}
            {{this.FuncAssignIndented("",resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
            current=this.{{this.currentValue}};
            return true;
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

file static class AggregateLoopSupport {
    public static string RenderSeedlessFold(
        Enumerable source,
        string sourceName,
        string resultType,
        string resultName,
        string itemName,
        Func<string,string,string> renderAssign) {
        var spanName = $"__span_{resultName}";
        var indexName = $"__i_{resultName}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is not null) {
            return $$"""
                {{resultType}} {{resultName}};
                var {{spanName}}={{directSpanExpression}};
                if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{resultName}}={{spanName}}[0];
                for(var {{indexName}}=1;{{indexName}}<{{spanName}}.Length;{{indexName}}++){
                    var {{itemName}}={{spanName}}[{{indexName}}];
                {{IndentLines(renderAssign("        ",itemName),"    ")}}
                }
                """;
        }
        if(source.CanProvideSpan) {
            return $$"""
                {{resultType}} {{resultName}};
                {{sourceName}}.TryGetSpan(out var {{spanName}});
                if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{resultName}}={{spanName}}[0];
                for(var {{indexName}}=1;{{indexName}}<{{spanName}}.Length;{{indexName}}++){
                    var {{itemName}}={{spanName}}[{{indexName}}];
                {{IndentLines(renderAssign("        ",itemName),"    ")}}
                }
                """;
        }
        if(source.CanTryProvideSpan) {
            return $$"""
                {{resultType}} {{resultName}};
                if({{sourceName}}.TryGetSpan(out var {{spanName}})){
                    if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{resultName}}={{spanName}}[0];
                    for(var {{indexName}}=1;{{indexName}}<{{spanName}}.Length;{{indexName}}++){
                        var {{itemName}}={{spanName}}[{{indexName}}];
                {{IndentLines(renderAssign("            ",itemName),"        ")}}
                    }
                }else{
                    if(!{{sourceName}}.TryGetNext(out var {{itemName}})) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{resultName}}={{itemName}};
                    while({{sourceName}}.TryGetNext(out {{itemName}})){
                {{IndentLines(renderAssign("        ",itemName),"        ")}}
                    }
                }
                """;
        }
        return $$"""
            if(!{{sourceName}}.TryGetNext(out var {{itemName}})) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            var {{resultName}}={{itemName}};
            while({{sourceName}}.TryGetNext(out {{itemName}})){
            {{IndentLines(renderAssign("    ",itemName),"    ")}}
            }
            """;
    }

    public static string RenderSeededFold(
        Enumerable source,
        string sourceName,
        string resultName,
        string itemName,
        Func<string,string,string> renderAssign) {
        var spanName = $"__span_{resultName}";
        var indexName = $"__i_{resultName}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is not null) {
            return $$"""
                var {{spanName}}={{directSpanExpression}};
                for(var {{indexName}}=0;{{indexName}}<{{spanName}}.Length;{{indexName}}++){
                    var {{itemName}}={{spanName}}[{{indexName}}];
                {{IndentLines(renderAssign("        ",itemName),"    ")}}
                }
                """;
        }
        if(source.CanProvideSpan) {
            return $$"""
                {{sourceName}}.TryGetSpan(out var {{spanName}});
                for(var {{indexName}}=0;{{indexName}}<{{spanName}}.Length;{{indexName}}++){
                    var {{itemName}}={{spanName}}[{{indexName}}];
                {{IndentLines(renderAssign("        ",itemName),"    ")}}
                }
                """;
        }
        if(source.CanTryProvideSpan) {
            return $$"""
                if({{sourceName}}.TryGetSpan(out var {{spanName}})){
                    for(var {{indexName}}=0;{{indexName}}<{{spanName}}.Length;{{indexName}}++){
                        var {{itemName}}={{spanName}}[{{indexName}}];
                {{IndentLines(renderAssign("            ",itemName),"        ")}}
                    }
                }else{
                    while({{sourceName}}.TryGetNext(out var {{itemName}})){
                {{IndentLines(renderAssign("        ",itemName),"        ")}}
                    }
                }
                """;
        }
        return $$"""
            while({{sourceName}}.TryGetNext(out var {{itemName}})){
            {{IndentLines(renderAssign("    ",itemName),"    ")}}
            }
            """;
    }

    private static string IndentLines(string text,string indent) =>
        string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line => line.Length==0 ? line : indent+line));
}

file static class AggregateInlineDelegateSupport {
    public static string? DelegateLocal(this Fragment _,FuncFragment func,string localName) {
        if(!func.CanInvokeAsDelegate) {
            return string.Empty;
        }

        if(func.DelegateTypeName is not { Length:>0 } delegateTypeName
            ||func.DelegateConstructorArg is not { Length:>0 } delegateConstructorArg) {
            return null;
        }

        return $"{delegateTypeName} {localName} = {delegateConstructorArg};";
    }

    public static string FuncAssignInline(this Fragment fragment,FuncFragment func,string methodName,string delegateLocalName,string destination,params string[] arguments) {
        return fragment.FuncAssignForAggregateInline(func,methodName,destination,delegateLocalName,arguments);
    }

    public static string FuncAssignInlineIndented(this Fragment fragment,string indent,FuncFragment func,string methodName,string delegateLocalName,string destination,params string[] arguments) {
        return IndentLines(fragment.FuncAssignInline(func,methodName,delegateLocalName,destination,arguments).Trim(),indent);
    }

    private static string IndentLines(string text,string indent) =>
        string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line => indent+line));
}
