namespace Seamless.SourceGenerator.Linq.Fragments;

public class GroupBy_keySelector(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(FuncSupport(keySelector,nameof(keySelector)),$$"""
        private sealed class Grouping : global::System.Linq.IGrouping<{{FuncReturnTypeName(keySelector)}}, {{source.ElementType}}> {
            private readonly global::System.Collections.Generic.List<{{source.ElementType}}> values = new();
            public Grouping({{FuncReturnTypeName(keySelector)}} key) {
                this.Key = key;
            }
            public {{FuncReturnTypeName(keySelector)}} Key { get; }
            public void Add({{source.ElementType}} value) => values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> GetEnumerator() => values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        """);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>? {{lookup}};
        private global::System.Collections.Generic.List<Grouping>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => source.CanProvideSpan
        ? $$"""
            if(this.{{lookup}} is null) {
                {{e}}.TryGetSpan(out var __span);
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>(__span.Length);
                this.{{group}}=new global::System.Collections.Generic.List<Grouping>(__span.Length);
                foreach(var {{item}} in __span){
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(!this.{{lookup}}.TryGetValue({{key}},out var __group)){
                        __group=new Grouping({{key}});
                        this.{{lookup}}.Add({{key}},__group);
                        this.{{group}}.Add(__group);
                    }
                    __group.Add({{item}});
                }
            }
            this.{{a}}++;
            if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
                current=this.{{currentValue}}=this.{{group}}[this.{{a}}];
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{lookup}} is null) {
                if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>(__count);
                    this.{{group}}=new global::System.Collections.Generic.List<Grouping>(__count);
                }else{
                    this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, Grouping>();
                    this.{{group}}=new global::System.Collections.Generic.List<Grouping>();
                }
                while({{e}}.MoveNext()) {
                    var {{item}}={{e}}.Current;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(!this.{{lookup}}.TryGetValue({{key}},out var __group)){
                        __group=new Grouping({{key}});
                        this.{{lookup}}.Add({{key}},__group);
                        this.{{group}}.Add(__group);
                    }
                    __group.Add({{item}});
                }
            }
            this.{{a}}++;
            if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
                current=this.{{currentValue}}=this.{{group}}[this.{{a}}];
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>";
}
// GroupBy(keySelector, resultSelector) のフラグメント。
// resultSelector が匿名型を返す場合は TResult ジェネリック型パラメータを使う。
public class GroupBy_keySelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {

    // resultSelector は Func<TKey, IEnumerable<TSource>, TResult>（3引数）で識別する。
    // elementSelector Func<TSource, TElement>（2引数）は除外。
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 3 };

    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;

    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Seamless.Linq.Carrier.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;

    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;

    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>, global::Seamless.Linq.Carrier.IValueEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, resultSelector)}}));
        """;

    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(resultSelector, nameof(resultSelector))), FactorySource);

    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;

    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new global::System.NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(keySelector,resultSelector)}}
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>? {{lookup}};
        private global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>(__count);
                this.{{group}}=new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>(__count);
            }else{
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>();
                this.{{group}}=new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            }
            while({{e}}.MoveNext()) {
                var {{item}}={{e}}.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!this.{{lookup}}.TryGetValue({{key}},out var __group)) {
                    __group=new global::System.Collections.Generic.List<{{source.ElementType}}>();
                    this.{{lookup}}.Add({{key}},__group);
                    this.{{group}}.Add({{key}});
                }
                __group.Add({{item}});
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            var __key=this.{{group}}[this.{{a}}];
            {{FuncAssignCurrent(resultSelector,nameof(resultSelector),currentValue,"__key",$"(global::System.Collections.Generic.IEnumerable<{source.ElementType}>)this.{lookup}[__key]")}}
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;

    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})"
            : $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {resultSelector.DelegateConstructorArg ?? resultSelector.RenderAsLambda()})";
}
public class GroupBy_keySelector_elementSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector)
    : Enumerable(番号) {

    // elementSelector は Func<TSource, TElement>（型引数2個）で識別する。
    // resultSelector Func<TKey, IEnumerable<TSource>, TResult>（型引数3個）は除外。
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 };

    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg ?? elementSelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))),$$"""
        private sealed class Grouping : global::System.Linq.IGrouping<{{KeyType}}, {{ElemType}}> {
            private readonly global::System.Collections.Generic.List<{{ElemType}}> values = new();
            public Grouping({{KeyType}} key) {
                this.Key = key;
            }
            public {{KeyType}} Key { get; }
            public void Add({{ElemType}} value) => values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{ElemType}}> GetEnumerator() => values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
        """);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, Grouping>? {{lookup}};
        private global::System.Collections.Generic.List<Grouping>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, Grouping>(__count);
                this.{{group}}=new global::System.Collections.Generic.List<Grouping>(__count);
            }else{
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, Grouping>();
                this.{{group}}=new global::System.Collections.Generic.List<Grouping>();
            }
            while({{e}}.MoveNext()) {
                var {{item}}={{e}}.Current;
                {{KeyType}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!this.{{lookup}}.TryGetValue({{key}},out var __group)) {
                    __group=new Grouping({{key}});
                    this.{{lookup}}.Add({{key}},__group);
                    this.{{group}}.Add(__group);
                }
                {{ElemType}} {{a}}=default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),a,item)}}
                __group.Add({{a}});
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            current=this.{{currentValue}}=this.{{group}}[this.{{a}}];
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {FuncReturnTypeName(elementSelector)}>";
}
// GroupBy(keySelector, elementSelector, resultSelector) のフラグメント。
// resultSelector が匿名型を返す場合は TResult ジェネリック型パラメータを使う。
public class GroupBy_keySelector_elementSelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {

    // 3引数: keySelector, elementSelector(Func<TSource,TElement> 型引数2個), resultSelector(Func<TKey,IEnumerable<TElement>,TResult> 型引数3個)
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[2].Type is INamedTypeSymbol { TypeArguments.Length: 3 };

    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);

    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;

    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>, global::Seamless.Linq.Carrier.IValueEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;

    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;

    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>, global::Seamless.Linq.Carrier.IValueEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, elementSelector, resultSelector)}}));
        """;

    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))), FuncSupport(resultSelector, nameof(resultSelector))), FactorySource);

    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;

    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new global::System.NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(keySelector,elementSelector,resultSelector)}}
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, global::System.Collections.Generic.List<{{ElemType}}>>? {{lookup}};
        private global::System.Collections.Generic.List<{{KeyType}}>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            if({{e}}.TryGetNonEnumeratedCount(out var __count)) {
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, global::System.Collections.Generic.List<{{ElemType}}>>(__count);
                this.{{group}}=new global::System.Collections.Generic.List<{{KeyType}}>(__count);
            }else{
                this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, global::System.Collections.Generic.List<{{ElemType}}>>();
                this.{{group}}=new global::System.Collections.Generic.List<{{KeyType}}>();
            }
            while({{e}}.MoveNext()) {
                var {{item}}={{e}}.Current;
                {{KeyType}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!this.{{lookup}}.TryGetValue({{key}},out var __group)) {
                    __group=new global::System.Collections.Generic.List<{{ElemType}}>();
                    this.{{lookup}}.Add({{key}},__group);
                    this.{{group}}.Add({{key}});
                }
                {{ElemType}} {{a}}=default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),a,item)}}
                __group.Add({{a}});
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            var __key=this.{{group}}[this.{{a}}];
            {{FuncAssignCurrent(resultSelector,nameof(resultSelector),currentValue,"__key",$"(global::System.Collections.Generic.IEnumerable<{ElemType}>)this.{lookup}[__key]")}}
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;

    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})"
            : $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg ?? elementSelector.RenderAsLambda()}, {resultSelector.DelegateConstructorArg ?? resultSelector.RenderAsLambda()})";
}
