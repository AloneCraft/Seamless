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
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = source.CanTryProvideSpan
            ? $$"""
                {{source.ElementType}} {{result}};
                if({{srcVar}}.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{result}}=default!;
                    var __first=true;
                    foreach(var __item in __span){
                        if(__first){
                            {{result}}=__item;
                            __first=false;
                        }else{
                            {{this.FuncAssignInlineIndented("            ",func,nameof(func),funcVar,result,result,"__item")}}
                        }
                    }
                }else{
                    if(!{{srcVar}}.TryGetNext(out var __item)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                    {{result}}=__item;
                    while({{srcVar}}.TryGetNext(out __item)){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,result,result,"__item")}}
                    }
                }
                """
            : $$"""
                if(!{{srcVar}}.TryGetNext(out var __item)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                var {{result}}=__item;
                while({{srcVar}}.TryGetNext(out __item)){
                    {{this.FuncAssignInlineIndented("    ",func,nameof(func),funcVar,result,result,"__item")}}
                }
                """;
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                {{fold}}
                {{destVar}}={{result}};
            }
            """;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct(source.ElementType)}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            {{source.ElementType}} {{result}};
            if({{e}}.TryGetSpan(out var __span)){
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{result}}=default!;
                var __first=true;
                foreach(var __item in __span){
                    if(__first){
                        {{result}}=__item;
                        __first=false;
                    }else{
                        {{FuncAssignIndented("            ",func,nameof(func),result,result,"__item")}}
                    }
                }
            }else{
                if(!{{e}}.TryGetNext(out var {{item}})) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{result}}={{item}};
                while({{e}}.TryGetNext(out {{item}})){
                    {{FuncAssignIndented("        ",func,nameof(func),result,result,item)}}
                }
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """
        : $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            if(!{{e}}.TryGetNext(out var {{item}})) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            var {{result}}={{item}};
            while({{e}}.TryGetNext(out {{item}})){
                {{FuncAssignIndented("    ",func,nameof(func),result,result,item)}}
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
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
public class Aggregate_seed_func(int 番号, Enumerable source,string seed, FuncFragment func) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = source.CanTryProvideSpan
            ? $$"""
                if({{srcVar}}.TryGetSpan(out var __span)){
                    foreach(var __item in __span){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,result,result,"__item")}}
                    }
                }else{
                    while({{srcVar}}.TryGetNext(out var __item)){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,result,result,"__item")}}
                    }
                }
                """
            : $$"""
                while({{srcVar}}.TryGetNext(out var __item)){
                    {{this.FuncAssignInlineIndented("    ",func,nameof(func),funcVar,result,result,"__item")}}
                }
                """;
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                var {{result}} = {{seed}};
                {{fold}}
                {{destVar}}={{result}};
            }
            """;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(func,nameof(func));
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(func)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct(FuncParameterTypeName(func,0))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(func)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            if({{e}}.TryGetSpan(out var __span)){
                foreach(var __item in __span){
                    {{FuncAssignIndented("        ",func,nameof(func),result,result,"__item")}}
                }
            }else{
                while({{e}}.TryGetNext(out var {{item}})){
                    {{FuncAssignIndented("        ",func,nameof(func),result,result,item)}}
                }
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """
        : $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            while({{e}}.TryGetNext(out var {{item}})){
                {{FuncAssignIndented("    ",func,nameof(func),result,result,item)}}
            }
            this.{{currentValue}}={{result}};
            current=this.{{currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
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
public class Aggregate_seed_func_resultSelector(int 番号, Enumerable source,string seed, FuncFragment func,FuncFragment resultSelector) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var resultSelectorVar = $"__aggResultSelector{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        var resultSelectorSetup = this.DelegateLocal(resultSelector,resultSelectorVar);
        if(funcSetup is null || resultSelectorSetup is null) return null;
        var fold = source.CanTryProvideSpan
            ? $$"""
                if({{srcVar}}.TryGetSpan(out var __span)){
                    foreach(var __item in __span){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,result,result,"__item")}}
                    }
                }else{
                    while({{srcVar}}.TryGetNext(out var __item)){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,result,result,"__item")}}
                    }
                }
                """
            : $$"""
                while({{srcVar}}.TryGetNext(out var __item)){
                    {{this.FuncAssignInlineIndented("    ",func,nameof(func),funcVar,result,result,"__item")}}
                }
                """;
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            {{resultSelectorSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                var {{result}} = {{seed}};
                {{fold}}
                {{this.FuncAssignInlineIndented("    ",resultSelector,nameof(resultSelector),resultSelectorVar,destVar,result)}}
            }
            """;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => $$"""
        {{FuncSupport(func,nameof(func))}}
        {{FuncSupport(resultSelector,nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(func,resultSelector)})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{DefineScalarEnumeratorStruct(FuncReturnTypeName(resultSelector))}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(func,resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        private bool {{yielded}} = false;
        """;
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            if({{e}}.TryGetSpan(out var __span)){
                foreach(var __item in __span){
                    {{FuncAssignIndented("        ",func,nameof(func),result,result,"__item")}}
                }
            }else{
                while({{e}}.TryGetNext(out var {{item}})){
                    {{FuncAssignIndented("        ",func,nameof(func),result,result,item)}}
                }
            }
            {{FuncAssignIndented("",resultSelector,nameof(resultSelector),$"this.{currentValue}",result)}}
            current=this.{{currentValue}};
            return true;
            """
        : $$"""
            if(this.{{yielded}}) {
                current = default!;
                return false;
            }
            this.{{yielded}} = true;
            var {{result}} = {{seed}};
            while({{e}}.TryGetNext(out var {{item}})){
                {{FuncAssignIndented("    ",func,nameof(func),result,result,item)}}
            }
            {{FuncAssignIndented("",resultSelector,nameof(resultSelector),$"this.{currentValue}",result)}}
            current=this.{{currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}

file static class AggregateInlineDelegateSupport {
    public static string? DelegateLocal(this Fragment _,FuncFragment func,string localName) {
        if(!func.CanInvokeAsDelegate) {
            return string.Empty;
        }

        if(func.DelegateTypeName is not { Length: > 0 } delegateTypeName
            || func.DelegateConstructorArg is not { Length: > 0 } delegateConstructorArg) {
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
        string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>indent+line));
}
