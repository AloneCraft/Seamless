namespace Seamless.SourceGenerator.Linq.Fragments;

internal static class GroupBySupport{
    public static string GroupingClass(string keyType,string elementType)=>$$"""
        private sealed class Grouping:global::System.Linq.IGrouping<{{keyType}},{{elementType}}>{
            private readonly global::System.Collections.Generic.List<{{elementType}}> values=new();
            public Grouping({{keyType}} key){
                this.Key=key;
            }
            public {{keyType}} Key{get;}
            public void Add({{elementType}} value)=>values.Add(value);
            public global::System.Collections.Generic.IEnumerator<{{elementType}}> GetEnumerator()=>values.GetEnumerator();
            global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
        }
        """;

    public static string LookupFields(string keyType,string lookupValueType,string groupListType,string lookupName,string groupName,string indexName)=>$$"""
        private global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>? {{lookupName}};
        private global::System.Collections.Generic.List<{{groupListType}}>? {{groupName}};
        private int {{indexName}}=-1;
        """;

    public static string BuildGroupingLookupBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string groupingType,
        string keyAssignCode,
        string groupAddCode,
        string lookupName,
        string groupName,
        string indexName,
        string itemName,
        string keyName)=>BuildLookupBody(
            enumeratorName,
            canProvideSpan,
            keyType,
            groupingType,
            groupingType,
            $"new {groupingType}({keyName})",
            keyAssignCode,
            groupAddCode,
            lookupName,
            groupName,
            indexName,
            itemName,
            keyName);

    public static string BuildListLookupBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string lookupValueType,
        string groupListType,
        string lookupValueCreateExpression,
        string keyAssignCode,
        string groupAddCode,
        string lookupName,
        string groupName,
        string indexName,
        string itemName,
        string keyName)=>BuildLookupBody(
            enumeratorName,
            canProvideSpan,
            keyType,
            lookupValueType,
            groupListType,
            lookupValueCreateExpression,
            keyAssignCode,
            groupAddCode,
            lookupName,
            groupName,
            indexName,
            itemName,
            keyName);

    public static string BuildLookupInitialization(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string lookupValueType,
        string groupListType,
        string lookupValueCreateExpression,
        string keyAssignCode,
        string groupAddCode,
        string groupListAddCode,
        string lookupName,
        string groupName,
        string itemName,
        string keyName)=>canProvideSpan ? $$"""
        if(this.{{lookupName}} is null){
            {{enumeratorName}}.TryGetSpan(out var __span);
            this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__span.Length);
            this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__span.Length);
            foreach(var {{itemName}} in __span){
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                    __group={{lookupValueCreateExpression}};
                    this.{{lookupName}}.Add({{keyName}},__group);
                    {{groupListAddCode}}
                }
                {{IndentBlock(groupAddCode,"                ")}}
            }
        }
        """ : $$"""
        if(this.{{lookupName}} is null){
            if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count)){
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__count);
                this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__count);
            }else{
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>();
                this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>();
            }
            while({{enumeratorName}}.MoveNext()){
                var {{itemName}}={{enumeratorName}}.Current;
                {{keyType}} {{keyName}}=default!;
                {{IndentBlock(keyAssignCode,"                ")}}
                if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                    __group={{lookupValueCreateExpression}};
                    this.{{lookupName}}.Add({{keyName}},__group);
                    {{groupListAddCode}}
                }
                {{IndentBlock(groupAddCode,"                ")}}
            }
        }
        """;

    private static string BuildLookupBody(
        string enumeratorName,
        bool canProvideSpan,
        string keyType,
        string lookupValueType,
        string groupListType,
        string lookupValueCreateExpression,
        string keyAssignCode,
        string groupAddCode,
        string lookupName,
        string groupName,
        string indexName,
        string itemName,
        string keyName){
        return canProvideSpan ? $$"""
            if(this.{{lookupName}} is null){
                {{enumeratorName}}.TryGetSpan(out var __span);
                this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__span.Length);
                this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__span.Length);
                foreach(var {{itemName}} in __span){
                    {{keyType}} {{keyName}}=default!;
                    {{IndentBlock(keyAssignCode,"                    ")}}
                    if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                        __group={{lookupValueCreateExpression}};
                        this.{{lookupName}}.Add({{keyName}},__group);
                        this.{{groupName}}.Add(__group);
                    }
                    {{IndentBlock(groupAddCode,"                    ")}}
                }
            }
            this.{{indexName}}++;
            if(this.{{groupName}} is not null&&(uint)this.{{indexName}}<(uint)this.{{groupName}}.Count){
                current=this.Current=this.{{groupName}}[this.{{indexName}}];
                return true;
            }
            current=default!;
            return false;
            """ : $$"""
            if(this.{{lookupName}} is null){
                if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count)){
                    this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>(__count);
                    this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>(__count);
                }else{
                    this.{{lookupName}}=new global::System.Collections.Generic.Dictionary<{{keyType}},{{lookupValueType}}>();
                    this.{{groupName}}=new global::System.Collections.Generic.List<{{groupListType}}>();
                }
                while({{enumeratorName}}.MoveNext()){
                    var {{itemName}}={{enumeratorName}}.Current;
                    {{keyType}} {{keyName}}=default!;
                    {{IndentBlock(keyAssignCode,"                    ")}}
                    if(!this.{{lookupName}}.TryGetValue({{keyName}},out var __group)){
                        __group={{lookupValueCreateExpression}};
                        this.{{lookupName}}.Add({{keyName}},__group);
                        this.{{groupName}}.Add(__group);
                    }
                    {{IndentBlock(groupAddCode,"                    ")}}
                }
            }
            this.{{indexName}}++;
            if(this.{{groupName}} is not null&&(uint)this.{{indexName}}<(uint)this.{{groupName}}.Count){
                current=this.Current=this.{{groupName}}[this.{{indexName}}];
                return true;
            }
            current=default!;
            return false;
            """;
    }

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0 ? line : indent+line));
    }
}

internal class GroupBy_keySelector(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.GroupBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()})";
    public override string StructMethod => JoinStaticMethods(FuncSupport(keySelector,nameof(keySelector)),GroupBySupport.GroupingClass(FuncReturnTypeName(keySelector),source.ElementType));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        {{GroupBySupport.LookupFields(FuncReturnTypeName(keySelector),"Grouping","Grouping",lookup,group,a)}}
        """;
    public override string GetTryGetNextBody => GroupBySupport.BuildGroupingLookupBody(e,source.CanProvideSpan,FuncReturnTypeName(keySelector),"Grouping",FuncAssign(keySelector,nameof(keySelector),key,item),"__group.Add("+item+");",lookup,group,a,item,key);
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
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(keySelector,resultSelector)}}
        {{GroupBySupport.LookupFields(FuncReturnTypeName(keySelector),$"global::System.Collections.Generic.List<{source.ElementType}>",FuncReturnTypeName(keySelector),lookup,group,a)}}
        """;
    public override string GetTryGetNextBody => GroupBySupport.BuildLookupInitialization(e,source.CanProvideSpan,FuncReturnTypeName(keySelector),$"global::System.Collections.Generic.List<{source.ElementType}>",FuncReturnTypeName(keySelector),$"new global::System.Collections.Generic.List<{source.ElementType}>()",FuncAssign(keySelector,nameof(keySelector),key,item),"__group.Add("+item+");","this."+group+".Add("+key+");",lookup,group,item,key)+$$"""
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count){
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
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))),GroupBySupport.GroupingClass(KeyType,ElemType));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{GroupBySupport.LookupFields(KeyType,"Grouping","Grouping",lookup,group,a)}}
        """;
    public override string GetTryGetNextBody => GroupBySupport.BuildGroupingLookupBody(e,source.CanProvideSpan,KeyType,"Grouping",FuncAssign(keySelector,nameof(keySelector),key,item),"__group.Add("+a+");",lookup,group,a,item,key);
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
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(keySelector,elementSelector,resultSelector)}}
        {{GroupBySupport.LookupFields(KeyType,$"global::System.Collections.Generic.List<{ElemType}>",KeyType,lookup,group,a)}}
        """;
    public override string GetTryGetNextBody => GroupBySupport.BuildLookupInitialization(e,source.CanProvideSpan,KeyType,$"global::System.Collections.Generic.List<{ElemType}>",KeyType,$"new global::System.Collections.Generic.List<{ElemType}>()",FuncAssign(keySelector,nameof(keySelector),key,item),"__group.Add("+a+");","this."+group+".Add("+key+");",lookup,group,item,key)+$$"""
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count){
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
