namespace Seamless.SourceGenerator.Linq.Fragments;

internal static class GroupBySupport{
    public static string ArrayBackedGroupingClass(string keyType,string elementType)=>$$"""
        private sealed class Grouping:global::System.Linq.IGrouping<{{keyType}},{{elementType}}>{
            private readonly {{keyType}} key;
            private {{elementType}}[] elements;
            private int count;
            public Grouping({{keyType}} key,{{elementType}} value){
                this.key=key;
                this.elements=[value];
                this.count=1;
            }
            public {{keyType}} Key=>key;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Add({{elementType}} value){
                if(this.elements.Length==this.count){
                    global::System.Array.Resize(ref this.elements,this.count*2);
                }
                this.elements[this.count]=value;
                this.count++;
            }
            public global::System.Collections.Generic.IEnumerator<{{elementType}}> GetEnumerator(){
                for(var __i=0;__i<this.count;__i++){
                    yield return this.elements[__i];
                }
            }
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
        }
        """;

    public static string GroupingDictionaryFields(string keyType,string lookupName,string innerEnumeratorName)=>$$"""
        private global::System.Collections.Generic.Dictionary<{{keyType}},Grouping>? {{lookupName}};
        private global::System.Collections.Generic.Dictionary<{{keyType}},Grouping>.Enumerator {{innerEnumeratorName}};
        """;

    public static string ListDictionaryFields(string keyType,string elementType,string lookupName,string innerEnumeratorName)=>$$"""
        private global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>>? {{lookupName}};
        private global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>>.Enumerator {{innerEnumeratorName}};
        """;

    public static string BuildGroupingDictionaryBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string elementType,
        string keyAssignCode,
        string elementAssignCode,
        string elementName,
        string lookupName,
        string innerEnumeratorName,
        string itemName,
        string keyName)=>canProvideSpan ? $$"""
        if(this.{{lookupName}} is null){
            {{enumeratorName}}.TryGetSpan(out var __span);
            this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},Grouping>(__span.Length);
            for(var __i=0;__i<__span.Length;__i++){
                var {{itemName}}=__span[__i];
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                ref var __group=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookupName}},{{keyName}},out var __exists);
                if(!__exists){
                    __group=new Grouping({{keyName}},{{elementName}});
                }else{
                    __group!.Add({{elementName}});
                }
            }
            this.{{innerEnumeratorName}}=this.{{lookupName}}.GetEnumerator();
        }
        """ : $$"""
        if(this.{{lookupName}} is null){
            if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count)){
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},Grouping>(__count);
            }else{
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},Grouping>();
            }
            while({{enumeratorName}}.MoveNext()){
                var {{itemName}}={{enumeratorName}}.Current;
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                ref var __group=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookupName}},{{keyName}},out var __exists);
                if(!__exists){
                    __group=new Grouping({{keyName}},{{elementName}});
                }else{
                    __group!.Add({{elementName}});
                }
            }
            this.{{innerEnumeratorName}}=this.{{lookupName}}.GetEnumerator();
        }
        """;

    public static string BuildListDictionaryBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string elementType,
        string keyAssignCode,
        string elementAssignCode,
        string elementName,
        string lookupName,
        string innerEnumeratorName,
        string itemName,
        string keyName)=>canProvideSpan ? $$"""
        if(this.{{lookupName}} is null){
            {{enumeratorName}}.TryGetSpan(out var __span);
            this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>>(__span.Length);
            for(var __i=0;__i<__span.Length;__i++){
                var {{itemName}}=__span[__i];
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                ref var __list=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookupName}},{{keyName}},out var __exists);
                if(!__exists){
                    __list=new global::System.Collections.Generic.List<{{elementType}}>();
                }
                __list.Add({{elementName}});
            }
            this.{{innerEnumeratorName}}=this.{{lookupName}}.GetEnumerator();
        }
        """ : $$"""
        if(this.{{lookupName}} is null){
            if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count)){
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>>(__count);
            }else{
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},global::System.Collections.Generic.List<{{elementType}}>>();
            }
            while({{enumeratorName}}.MoveNext()){
                var {{itemName}}={{enumeratorName}}.Current;
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                {{IndentBlock(elementAssignCode,"                ")}}
                ref var __list=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{lookupName}},{{keyName}},out var __exists);
                if(!__exists){
                    __list=new global::System.Collections.Generic.List<{{elementType}}>();
                }
                __list.Add({{elementName}});
            }
            this.{{innerEnumeratorName}}=this.{{lookupName}}.GetEnumerator();
        }
        """;

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0 ? line : indent+line));
    }
}

internal class GroupBy_keySelector(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(FuncSupport(keySelector,nameof(keySelector)),GroupBySupport.ArrayBackedGroupingClass(FuncReturnTypeName(keySelector),source.ElementType));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.GroupingDictionaryFields(FuncReturnTypeName(keySelector),lookup,innerEnumerator);
    public override string GetTryGetNextBody => GroupBySupport.BuildGroupingDictionaryBody(e,source.CanProvideSpan,FuncReturnTypeName(keySelector),source.ElementType,FuncAssign(keySelector,nameof(keySelector),key,item),"",item,lookup,innerEnumerator,item,key)+$$"""
        if(this.{{innerEnumerator}}.MoveNext()){
            current=this.Current=this.{{innerEnumerator}}.Current.Value;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>";
}

internal class GroupBy_keySelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {
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
    public override string GetEnumeratorFields =>
        GroupBySupport.ListDictionaryFields(FuncReturnTypeName(keySelector),source.ElementType,lookup,innerEnumerator);
    public override string GetTryGetNextBody => GroupBySupport.BuildListDictionaryBody(e,source.CanProvideSpan,FuncReturnTypeName(keySelector),source.ElementType,FuncAssign(keySelector,nameof(keySelector),key,item),"",item,lookup,innerEnumerator,item,key)+$$"""
        if(this.{{innerEnumerator}}.MoveNext()){
            var __kv=this.{{innerEnumerator}}.Current;
            {{FuncAssignCurrent(resultSelector,nameof(resultSelector),currentValue,"__kv.Key",$"(global::System.Collections.Generic.IEnumerable<{source.ElementType}>)__kv.Value")}}
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

internal class GroupBy_keySelector_elementSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector)
    : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 2 };
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {elementSelector.DelegateConstructorArg ?? elementSelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))),GroupBySupport.ArrayBackedGroupingClass(KeyType,ElemType));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    public override string GetEnumeratorFields =>
        GroupBySupport.GroupingDictionaryFields(KeyType,lookup,innerEnumerator);
    public override string GetTryGetNextBody => GroupBySupport.BuildGroupingDictionaryBody(e,source.CanProvideSpan,KeyType,ElemType,FuncAssign(keySelector,nameof(keySelector),key,item),$"{ElemType} {a}=default!;\n{FuncAssign(elementSelector,nameof(elementSelector),a,item)}",a,lookup,innerEnumerator,item,key)+$$"""
        if(this.{{innerEnumerator}}.MoveNext()){
            current=this.Current=this.{{innerEnumerator}}.Current.Value;
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {FuncReturnTypeName(elementSelector)}>";
}

internal class GroupBy_keySelector_elementSelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {
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
    public override string GetEnumeratorFields =>
        GroupBySupport.ListDictionaryFields(KeyType,ElemType,lookup,innerEnumerator);
    public override string GetTryGetNextBody => GroupBySupport.BuildListDictionaryBody(e,source.CanProvideSpan,KeyType,ElemType,FuncAssign(keySelector,nameof(keySelector),key,item),$"{ElemType} {a}=default!;\n{FuncAssign(elementSelector,nameof(elementSelector),a,item)}",a,lookup,innerEnumerator,item,key)+$$"""
        if(this.{{innerEnumerator}}.MoveNext()){
            var __kv=this.{{innerEnumerator}}.Current;
            {{FuncAssignCurrent(resultSelector,nameof(resultSelector),currentValue,"__kv.Key",$"(global::System.Collections.Generic.IEnumerable<{ElemType}>)__kv.Value")}}
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
