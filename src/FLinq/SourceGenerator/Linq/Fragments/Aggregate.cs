namespace Tsavorite.SourceGenerator.Linq.Fragments;
public class Aggregate_func(int 番号, Enumerable source, FuncFragment func) : Scalar(番号) {
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType){
        if(source is RangeRoot range
            && source.ElementType=="int"
            && RangePredicateOptimizer.IsAccumulatorAddition(func)) {
            return this.RenderRangeSumInline(range,destVar,destType);
        }
        if(TryRenderDirectLambda(func) is { } lambda
            && this.RenderDirectEnumerableCallInline(source,destVar,destType,"Aggregate",lambda) is { } direct) {
            return direct;
        }
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(System.NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = source.CanProvideSpan
            ? $$"""
                {{srcVar}}.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                {{source.ElementType}} {{this.result}}=default!;
                var __first=true;
                foreach(var __item in __span){
                    if(__first){
                        {{this.result}}=__item;
                        __first=false;
                    }else{
                        {{this.FuncAssignInline(func,nameof(func),funcVar,this.result,this.result,"__item")}}
                    }
                }
                """
            : $$"""
                if(!{{srcVar}}.TryGetNext(out var __item)) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                var {{this.result}}=__item;
                while({{srcVar}}.TryGetNext(out __item)){
                    {{this.FuncAssignInline(func,nameof(func),funcVar,this.result,this.result,"__item")}}
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
    private string RenderRangeSumInline(RangeRoot range,string destVar,string destType) {
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        var sum=$"__sum{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                if({{count}}==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                var {{sum}}={{RangeAggregateSupport.LongRangeSumExpression(start,count)}};
                {{destVar}}=({{destType}})(int){{sum}};
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
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            {{this.e}}.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            {{source.ElementType}} {{this.result}}=default!;
            var __first=true;
            foreach(var __item in __span){
                if(__first){
                    {{this.result}}=__item;
                    __first=false;
                }else{
                    {{this.FuncAssign(func,nameof(func),this.result,this.result,"__item")}}
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
            if(!{{this.e}}.TryGetNext(out var {{this.item}})) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            var {{this.result}}={{this.item}};
            while({{this.e}}.TryGetNext(out {{this.item}})){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,this.item)}}
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
        if(source is RangeRoot range
            && source.ElementType=="int"
            && RangePredicateOptimizer.IsAccumulatorAddition(func)) {
            return this.RenderRangeSeedSumInline(range,destVar,destType);
        }
        if(TryRenderDirectLambda(func) is { } lambda
            && this.RenderDirectEnumerableCallInline(source,destVar,destType,"Aggregate",seed,lambda) is { } direct) {
            return direct;
        }
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(System.NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        if(funcSetup is null) return null;
        var fold = source.CanProvideSpan
            ? $$"""
                {{srcVar}}.TryGetSpan(out var __span);
                foreach(var __item in __span){
                    {{this.FuncAssignInline(func,nameof(func),funcVar,this.result,this.result,"__item")}}
                }
                """
            : $$"""
                while({{srcVar}}.TryGetNext(out var __item)){
                    {{this.FuncAssignInline(func,nameof(func),funcVar,this.result,this.result,"__item")}}
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
    private string RenderRangeSeedSumInline(RangeRoot range,string destVar,string destType) {
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{this.result}}={{seed}};
                {{this.result}} += {{RangeAggregateSupport.RangeSumExpression(start,count,FuncParameterTypeName(func,0))}};
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
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            {{this.e}}.TryGetSpan(out var __span);
            foreach(var __item in __span){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,"__item")}}
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
                {{this.FuncAssign(func,nameof(func),this.result,this.result,this.item)}}
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
        if(source is RangeRoot range
            && source.ElementType=="int"
            && RangePredicateOptimizer.IsAccumulatorAddition(func)
            && resultSelector.CanInlineAssign
            && !resultSelector.RequiresStatementInlining
            && !resultSelector.CanInvokeAsDelegate) {
            return this.RenderRangeSeedSumResultSelectorInline(range,destVar,destType);
        }
        if(TryRenderDirectLambda(func) is { } lambda
            && TryRenderDirectLambda(resultSelector) is { } resultLambda
            && this.RenderDirectEnumerableCallInline(source,destVar,destType,"Aggregate",seed,lambda,resultLambda) is { } direct) {
            return direct;
        }
        string srcExpr;
        try { srcExpr = source.GetEnumeratorExpression; }
        catch(System.NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var funcVar = $"__aggFunc{番号}";
        var resultSelectorVar = $"__aggResultSelector{番号}";
        var funcSetup = this.DelegateLocal(func,funcVar);
        var resultSelectorSetup = this.DelegateLocal(resultSelector,resultSelectorVar);
        if(funcSetup is null || resultSelectorSetup is null) return null;
        var fold = source.CanProvideSpan
            ? $$"""
                {{srcVar}}.TryGetSpan(out var __span);
                foreach(var __item in __span){
                    {{this.FuncAssignInline(func,nameof(func),funcVar,this.result,this.result,"__item")}}
                }
                """
            : $$"""
                while({{srcVar}}.TryGetNext(out var __item)){
                    {{this.FuncAssignInline(func,nameof(func),funcVar,this.result,this.result,"__item")}}
                }
                """;
        return $$"""
            {{destType}} {{destVar}};
            {{funcSetup}}
            {{resultSelectorSetup}}
            using(var {{srcVar}}={{srcExpr}}){
                var {{this.result}} = {{seed}};
                {{fold}}
                {{this.FuncAssignInline(resultSelector,nameof(resultSelector),resultSelectorVar,destVar,this.result)}}
            }
            """;
    }
    private string RenderRangeSeedSumResultSelectorInline(RangeRoot range,string destVar,string destType) {
        var start=$"__start{番号}";
        var count=$"__count{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{start}}={{range.StartExpression}};
                var {{count}}={{range.CountExpression}};
                var {{this.result}}={{seed}};
                {{this.result}} += {{RangeAggregateSupport.RangeSumExpression(start,count,FuncParameterTypeName(func,0))}};
                {{this.FuncAssign(resultSelector,nameof(resultSelector),destVar,this.result)}}
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
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{this.yielded}}) {
                current = default!;
                return false;
            }
            this.{{this.yielded}} = true;
            var {{this.result}} = {{seed}};
            {{this.e}}.TryGetSpan(out var __span);
            foreach(var __item in __span){
                {{this.FuncAssign(func,nameof(func),this.result,this.result,"__item")}}
            }
            {{this.FuncAssign(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
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
                {{this.FuncAssign(func,nameof(func),this.result,this.result,this.item)}}
            }
            {{this.FuncAssign(resultSelector,nameof(resultSelector),$"this.{this.currentValue}",this.result)}}
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
        if(func.CanInvokeAsDelegate) {
            return $"{destination}={delegateLocalName}.Invoke({string.Join(", ", arguments)});";
        }

        return fragment.FuncAssignForAggregateInline(func,methodName,destination,arguments);
    }
}

file static class RangeAggregateSupport {
    public static string LongRangeSumExpression(string start,string count) =>
        $"((long)({count}) * (2L * (long)({start}) + (long)({count}) - 1L) / 2L)";

    public static string RangeSumExpression(string start,string count,string accumulatorType) {
        var sum=LongRangeSumExpression(start,count);
        return accumulatorType is "long" or "global::System.Int64"
            ? sum
            : $"({accumulatorType})({sum})";
    }
}
