namespace Seamless.FLinq.Linq.Fragments;
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(System.NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = source.CanTryProvideSpan
            ? $$"""
                {{source.ElementType}} {{this.result}};
                if({{srcVar}}.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    {{this.result}}=default!;
                    var __first=true;
                    foreach(var __item in __span){
                        if(__first){
                            {{this.result}}=__item;
                            __first=false;
                        }else{
                            {{this.FuncAssignInlineIndented("            ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                        }
                    }
                }else{
                    if(!{{srcVar}}.TryGetNext(out var __item)) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                    {{this.result}}=__item;
                    while({{srcVar}}.TryGetNext(out __item)){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                    }
                }
                """
            : $$"""
                if(!{{srcVar}}.TryGetNext(out var __item)) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                var {{this.result}}=__item;
                while({{srcVar}}.TryGetNext(out __item)){
                    {{this.FuncAssignInlineIndented("    ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                }
                """;
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
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            {{source.ElementType}} {{this.result}};
            if({{this.e}}.TryGetSpan(out var __span)){
                if(__span.Length==0) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                {{this.result}}=default!;
                var __first=true;
                foreach(var __item in __span){
                    if(__first){
                        {{this.result}}=__item;
                        __first=false;
                    }else{
                        {{this.FuncAssignIndented("            ",func,nameof(func),this.result,this.result,"__item")}}
                    }
                }
            }else{
                if(!{{this.e}}.TryGetNext(out var {{this.item}})) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
                {{this.result}}={{this.item}};
                while({{this.e}}.TryGetNext(out {{this.item}})){
                    {{this.FuncAssignIndented("        ",func,nameof(func),this.result,this.result,this.item)}}
                }
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """
        : $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            if(!{{this.e}}.TryGetNext(out var {{this.item}})) throw global::Seamless.Collections.Tsavorite.GeneratedCodeHelpers.NoElements();
            var {{this.result}}={{this.item}};
            while({{this.e}}.TryGetNext(out {{this.item}})){
                {{this.FuncAssignIndented("    ",func,nameof(func),this.result,this.result,this.item)}}
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Aggregate_seed_func(int 番号, Enumerable source,string seed, FuncFragment func) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(System.NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = source.CanTryProvideSpan
            ? $$"""
                if({{srcVar}}.TryGetSpan(out var __span)){
                    foreach(var __item in __span){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                    }
                }else{
                    while({{srcVar}}.TryGetNext(out var __item)){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                    }
                }
                """
            : $$"""
                while({{srcVar}}.TryGetNext(out var __item)){
                    {{this.FuncAssignInlineIndented("    ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                }
                """;
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
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            if({{this.e}}.TryGetSpan(out var __span)){
                foreach(var __item in __span){
                    {{this.FuncAssignIndented("        ",func,nameof(func),this.result,this.result,"__item")}}
                }
            }else{
                while({{this.e}}.TryGetNext(out var {{this.item}})){
                    {{this.FuncAssignIndented("        ",func,nameof(func),this.result,this.result,this.item)}}
                }
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """
        : $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            while({{this.e}}.TryGetNext(out var {{this.item}})){
                {{this.FuncAssignIndented("    ",func,nameof(func),this.result,this.result,this.item)}}
            }
            this.{{this.currentValue}}={{this.result}};
            current=this.{{this.currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

public class Aggregate_seed_func_resultSelector(int 番号, Enumerable source,string seed, FuncFragment func,FuncFragment resultSelector) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(System.NotSupportedException) { return null; }
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
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                    }
                }else{
                    while({{srcVar}}.TryGetNext(out var __item)){
                        {{this.FuncAssignInlineIndented("        ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                    }
                }
                """
            : $$"""
                while({{srcVar}}.TryGetNext(out var __item)){
                    {{this.FuncAssignInlineIndented("    ",func,nameof(func),funcVar,this.result,this.result,"__item")}}
                }
                """;
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
    public override string GetTryGetNextBody => source.CanTryProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            if({{this.e}}.TryGetSpan(out var __span)){
                foreach(var __item in __span){
                    {{this.FuncAssignIndented("        ",func,nameof(func),this.result,this.result,"__item")}}
                }
            }else{
                while({{this.e}}.TryGetNext(out var {{this.item}})){
                    {{this.FuncAssignIndented("        ",func,nameof(func),this.result,this.result,this.item)}}
                }
            }
            {{this.FuncAssignIndented("",resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
            current=this.{{this.currentValue}};
            return true;
            """
        : $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            while({{this.e}}.TryGetNext(out var {{this.item}})){
                {{this.FuncAssignIndented("    ",func,nameof(func),this.result,this.result,this.item)}}
            }
            {{this.FuncAssignIndented("",resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
            current=this.{{this.currentValue}};
            return true;
            """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
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
