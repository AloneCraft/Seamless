namespace Seamless.FLinq.Linq.Fragments;

/*
SetBase は Union/Except/Intersect など、要素そのものを key にする二入力集合演算の共通基底。

入力例:
  first.Union(second)
  first.Except(second, comparer)

生成例:
  if(operation.PreloadSecondFirst) {
      foreach(var right in second) set.Add(right);
      foreach(var left in first) if(set.SourceAccept(left)) return left;
  } else {
      foreach(var left in first) if(set.Add(left)) return left;
      foreach(var right in second) if(set.Add(right)) return right;
  }

バリエーション:
  - Union は左を返しながら set に追加し、その後右の未出要素を返す。
  - Except/Intersect は先に右側 set を作り、左側の Remove/Add など operation 固有の判定で返す。
  - direct consume では HashSet 初期化と TraversalSupport ループを出し、Enumerator.Current を経由しない。
*/
public abstract class SetBase(int 番号,Enumerable source,Enumerable second,string comparer,ISetOperationStrategy operation):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    private string Comparer=>ComparerExpressionSupport.RenderEqualityComparer(comparer,source.ElementType);
    public override string StructMethod=>string.Empty;
    protected override string StructTypeParameters=>"<TEnum1,TEnum2>";
    protected override string StructTypeConstraints=>
        $"where TEnum1:struct,global::System.Collections.Generic.IEnumerator<{source.ElementType}>,global::Seamless.System.Carrier.IValueEnumerator<{source.ElementType}>\n    where TEnum2:struct,global::System.Collections.Generic.IEnumerator<{second.ElementType}>,global::Seamless.System.Carrier.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName=>$"{Struct}<{source.EnumeratorTypeName},{second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression},{second.GetEnumeratorExpression},{Comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorFields=>operation.PreloadSecondFirst ? $$"""
            private readonly global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}> {{lookup}};
            private bool {{yielded}};
            private static global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                if(source.TryGetNonEnumeratedCount(out var {{count}})){
                    return new global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}>({{count}},comparer);
                }else{
                    return new global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}>(comparer);
                }
            }
            """ : $$"""
            private readonly global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}> {{lookup}};
            private bool {{yielded}};
            """;
    public override string GetEnumeratorConstructor=>operation.PreloadSecondFirst
        ? $"this.{lookup}=CreateLookup(this.second,this.comparer);"
        : $"this.{lookup}=new(this.comparer);";
    public override string GetEnumerableExpression=>operation.GetEnumerableExpression(source,second,Comparer);
    public override string GetTryGetNextBody=>operation.PreloadSecondFirst ? $$"""
            if(!this.{{yielded}}){
                while(second.TryGetNext(out var {{item}})){
                    this.{{lookup}}.Add({{SecondValueExpression(item)}});
                }
                this.{{yielded}}=true;
            }
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.{{operation.SourceAcceptMethod}}({{item}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """ : $$"""
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.{{operation.SourceAcceptMethod}}({{item}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            if(!this.{{yielded}}){
                this.{{yielded}}=true;
                while(second.TryGetNext(out var {{item}})){
                    var value=({{source.ElementType}})(object?){{item}}!;
                    if(this.{{lookup}}.Add(value)){
                        current=this.{{currentValue}}=value;
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression=> currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>$"{lookup}.Dispose();\nsource.Dispose();\nsecond.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var secondName=$"__sec{番号}";
        var setName=$"__set{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        if(CanUseSortedIntSetFastPath()) {
            AppendSortedIntSetFastPath(builder,sourceName,secondName,consumerName);
        }
        TraversalSupport.AppendHashSetInitialization(builder,setName,source.ElementType,operation.PreloadSecondFirst?secondName:sourceName,Comparer);
        if(!operation.PreloadSecondFirst){
            TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
                if({{setName}}.Add(value)){
                    {{consumerName}}.Consume(value);
                }
                """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
            TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
                if({{setName}}.Add({{SecondValueExpression("value")}})){
                    {{consumerName}}.Consume({{SecondValueExpression("value")}});
                }
                """,second.CanTryProvideSpan,second.CanProvideSpan,second.GetDirectSpanExpression(secondName));
            return builder.ToString().TrimEnd();
        }
        TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
            {{setName}}.Add({{SecondValueExpression("value")}});
            """,second.CanTryProvideSpan,second.CanProvideSpan,second.GetDirectSpanExpression(secondName));
        TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
            if({{setName}}.{{operation.SourceAcceptMethod}}(value)){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
        return builder.ToString().TrimEnd();
    }
    private string SecondValueExpression(string valueName){
        if(source.ElementType==second.ElementType){
            return valueName;
        }
        return $"({source.ElementType})(object?){valueName}!";
    }

    private bool CanUseSortedIntSetFastPath() =>
        operation.PreloadSecondFirst
        && ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,source.ElementType)
        && IsInt32Type(source.ElementType)
        && IsInt32Type(second.ElementType)
        && source.CanTryProvideSpan
        && second.CanTryProvideSpan;

    private void AppendSortedIntSetFastPath(StringBuilder builder,string sourceName,string secondName,string consumerName) {
        var sourceSpan=$"__setSrcSpan{番号}";
        var secondSpan=$"__setSecSpan{番号}";
        var sorted=$"__setSorted{番号}";
        var j=$"__setJ{番号}";
        var value=$"__setValue{番号}";
        var sourceDirectSpanExpression=source.GetDirectSpanExpression(sourceName);
        var secondDirectSpanExpression=second.GetDirectSpanExpression(secondName);
        if(sourceDirectSpanExpression is not null && secondDirectSpanExpression is not null) {
            builder.AppendLine($"var {sourceSpan}={sourceDirectSpanExpression};");
            builder.AppendLine($"var {secondSpan}={secondDirectSpanExpression};");
            AppendSortedIntSetFastPathBody(builder,sourceSpan,secondSpan,sorted,j,value,consumerName,string.Empty);
            return;
        }
        if(source.CanProvideSpan && second.CanProvideSpan) {
            builder.AppendLine($"{sourceName}.TryGetSpan(out var {sourceSpan});");
            builder.AppendLine($"{secondName}.TryGetSpan(out var {secondSpan});");
            AppendSortedIntSetFastPathBody(builder,sourceSpan,secondSpan,sorted,j,value,consumerName,string.Empty);
            return;
        }
        builder.AppendLine($"if({sourceName}.TryGetSpan(out var {sourceSpan})"
            + $"&&{secondName}.TryGetSpan(out var {secondSpan})){{");
        builder.AppendLine($"    var {sorted}=true;");
        builder.AppendLine($"    for(var __i=1;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"        if({sourceSpan}[__i]<={sourceSpan}[__i-1]){{");
        builder.AppendLine($"            {sorted}=false;");
        builder.AppendLine("            break;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine($"    if({sorted}){{");
        builder.AppendLine($"        for(var __i=1;__i<{secondSpan}.Length;__i++){{");
        builder.AppendLine($"            if({secondSpan}[__i]<={secondSpan}[__i-1]){{");
        builder.AppendLine($"                {sorted}=false;");
        builder.AppendLine("                break;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine($"    if({sorted}){{");
        builder.AppendLine($"        var {j}=0;");
        builder.AppendLine($"        for(var __i=0;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"            var {value}={sourceSpan}[__i];");
        builder.AppendLine($"            while({j}<{secondSpan}.Length&&{secondSpan}[{j}]<{value}){{");
        builder.AppendLine($"                {j}++;");
        builder.AppendLine("            }");
        if(operation.SourceAcceptMethod=="Add") {
            builder.AppendLine($"            if({j}>={secondSpan}.Length||{secondSpan}[{j}]!={value}){{");
            builder.AppendLine($"                {consumerName}.Consume({value});");
            builder.AppendLine("            }");
        } else {
            builder.AppendLine($"            if({j}<{secondSpan}.Length&&{secondSpan}[{j}]=={value}){{");
            builder.AppendLine($"                {consumerName}.Consume({value});");
            builder.AppendLine("            }");
        }
        builder.AppendLine("        }");
        builder.AppendLine("        return;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private void AppendSortedIntSetFastPathBody(
        StringBuilder builder,
        string sourceSpan,
        string secondSpan,
        string sorted,
        string j,
        string value,
        string consumerName,
        string indent) {
        builder.AppendLine($"{indent}var {sorted}=true;");
        builder.AppendLine($"{indent}for(var __i=1;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"{indent}    if({sourceSpan}[__i]<={sourceSpan}[__i-1]){{");
        builder.AppendLine($"{indent}        {sorted}=false;");
        builder.AppendLine($"{indent}        break;");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}if({sorted}){{");
        builder.AppendLine($"{indent}    for(var __i=1;__i<{secondSpan}.Length;__i++){{");
        builder.AppendLine($"{indent}        if({secondSpan}[__i]<={secondSpan}[__i-1]){{");
        builder.AppendLine($"{indent}            {sorted}=false;");
        builder.AppendLine($"{indent}            break;");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}if({sorted}){{");
        builder.AppendLine($"{indent}    var {j}=0;");
        builder.AppendLine($"{indent}    for(var __i=0;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"{indent}        var {value}={sourceSpan}[__i];");
        builder.AppendLine($"{indent}        while({j}<{secondSpan}.Length&&{secondSpan}[{j}]<{value}){{");
        builder.AppendLine($"{indent}            {j}++;");
        builder.AppendLine($"{indent}        }}");
        if(operation.SourceAcceptMethod=="Add") {
            builder.AppendLine($"{indent}        if({j}>={secondSpan}.Length||{secondSpan}[{j}]!={value}){{");
            builder.AppendLine($"{indent}            {consumerName}.Consume({value});");
            builder.AppendLine($"{indent}        }}");
        } else {
            builder.AppendLine($"{indent}        if({j}<{secondSpan}.Length&&{secondSpan}[{j}]=={value}){{");
            builder.AppendLine($"{indent}            {consumerName}.Consume({value});");
            builder.AppendLine($"{indent}        }}");
        }
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    return;");
        builder.AppendLine($"{indent}}}");
    }

    private static bool IsInt32Type(string typeName)
        =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}

/*
SetByBase は UnionBy/ExceptBy/IntersectBy など、keySelector(item) で集合判定する二入力演算の共通基底。

入力例:
  first.ExceptBy(second, x => x.Id, comparer)

生成例:
  foreach(var right in second) secondKeys.Add(keySelector(right));
  foreach(var left in first) {
      var key = keySelector(left);
      if(secondKeys.SourceAccept(key)) return left;
  }

バリエーション:
  - keySelector が identity の場合は key 計算用の FuncAssign を省いた専用 body を使う。
  - UnionBy は右側も「同じ key が未出なら右要素を返す」状態を持つ。
  - non-identity では closure/delegate/hoisted local を constructor と direct consume の両方へ伝播する。
*/
public abstract class SetByBase(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector,string comparer,ISetByOperationStrategy operation):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    private string Comparer=>ComparerExpressionSupport.RenderEqualityComparer(comparer,FuncReturnTypeName(keySelector));
    public override string StructMethod=> FuncSupport(keySelector,nameof(keySelector));
    protected override string StructTypeParameters=>"<TEnum1,TEnum2>";
    protected override string StructTypeConstraints=>
        $"where TEnum1:struct,global::System.Collections.Generic.IEnumerator<{source.ElementType}>,global::Seamless.System.Carrier.IValueEnumerator<{source.ElementType}>\n    where TEnum2:struct,global::System.Collections.Generic.IEnumerator<{second.ElementType}>,global::Seamless.System.Carrier.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName=>$"{Struct}<{source.EnumeratorTypeName},{second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression},{second.GetEnumeratorExpression},{Comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields=>$$"""
        private readonly global::Seamless.System.Carrier.HashSetSlim<{{FuncReturnTypeName(keySelector)}}> {{lookup}};
        private byte {{state}};
        private static global::Seamless.System.Carrier.HashSetSlim<{{FuncReturnTypeName(keySelector)}}> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer){
            if(source.TryGetNonEnumeratedCount(out var {{count}})){
                return new global::Seamless.System.Carrier.HashSetSlim<{{FuncReturnTypeName(keySelector)}}>({{count}},comparer);
            }else{
                return new global::Seamless.System.Carrier.HashSetSlim<{{FuncReturnTypeName(keySelector)}}>(comparer);
            }
        }
        """;
    public override string GetEnumeratorConstructor=>$"this.{lookup}=CreateLookup(this.second,this.comparer);";
    public override string GetEnumerableExpression=>operation.GetEnumerableExpression(source,second,keySelector,Comparer);
    public override string GetTryGetNextBody=> UseIdentityKeySelector ? GetTryGetNextIdentityBody : operation.PreloadSecondFirst ? $$"""
            if(this.{{state}}==0){
                while(second.TryGetNext(out var {{temp}})) this.{{lookup}}.Add({{SecondKeyExpression(temp)}});
                this.{{state}}=1;
            }
            while(source.TryGetNext(out var {{item}})){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssignIndented("    ",keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.{{operation.SourceAcceptMethod}}({{key}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """ : $$"""
            while(source.TryGetNext(out var {{item}})){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssignIndented("    ",keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.{{operation.SourceAcceptMethod}}({{key}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            if(this.{{state}}==0){
                this.{{state}}=1;
                while(second.TryGetNext(out var {{temp}})){
                    var {{item}}=({{source.ElementType}})(object?){{temp}}!;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssignIndented("        ",keySelector,nameof(keySelector),key,item)}}
                    if(this.{{lookup}}.Add({{key}})){
                        current=this.{{currentValue}}={{item}};
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """;
    private string GetTryGetNextIdentityBody=>operation.PreloadSecondFirst ? $$"""
            if(this.{{state}}==0){
                while(second.TryGetNext(out var {{temp}})) this.{{lookup}}.Add({{temp}});
                this.{{state}}=1;
            }
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.{{operation.SourceAcceptMethod}}({{item}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """ : $$"""
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.{{operation.SourceAcceptMethod}}({{item}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            if(this.{{state}}==0){
                this.{{state}}=1;
                while(second.TryGetNext(out var {{temp}})){
                    if(this.{{lookup}}.Add({{temp}})){
                        current=this.{{currentValue}}={{temp}};
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression=> currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>$"{lookup}.Dispose();\nsource.Dispose();\nsecond.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var secondName=$"__sec{番号}";
        var setName=$"__set{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        var hoistedDeclarations=HoistedLocalDeclarations(keySelector);
        if(!string.IsNullOrEmpty(hoistedDeclarations)) {
            builder.AppendLine(hoistedDeclarations);
        }
        if(UseIdentityKeySelector){
            if(CanUseSortedIntSetFastPath()) {
                AppendSortedIntSetFastPath(builder,sourceName,secondName,consumerName);
            }
            TraversalSupport.AppendHashSetInitialization(builder,setName,FuncReturnTypeName(keySelector),secondName,Comparer);
            if(!operation.PreloadSecondFirst){
                TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
                    if({{setName}}.Add(value)){
                        {{consumerName}}.Consume(value);
                    }
                    """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
                TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
                    if({{setName}}.Add(value)){
                        {{consumerName}}.Consume(value);
                    }
                    """,second.CanTryProvideSpan,second.CanProvideSpan,second.GetDirectSpanExpression(secondName));
                return builder.ToString().TrimEnd();
            }
            TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
                {{setName}}.Add(value);
                """,second.CanTryProvideSpan,second.CanProvideSpan,second.GetDirectSpanExpression(secondName));
            TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
                if(!{{setName}}.{{operation.SourceAcceptMethod}}(value)){
                    continue;
                }
                {{consumerName}}.Consume(value);
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
            return builder.ToString().TrimEnd();
        }
        TraversalSupport.AppendHashSetInitialization(builder,setName,FuncReturnTypeName(keySelector),secondName,Comparer);
        AppendSetByCollectLoop(builder,secondName,setName);
        AppendSetByConsumeLoop(builder,sourceName,consumerName,setName,operation.SourceAcceptMethod);
        return builder.ToString().TrimEnd();
    }
    private void AppendSetByCollectLoop(StringBuilder builder,string enumeratorName,string setName){
        TraversalSupport.AppendTraversalLoop(builder,enumeratorName,second.ElementType,"value",$$"""
            {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
            {{FuncAssignIndented("",keySelector,nameof(keySelector),key,"value")}}
            {{setName}}.Add({{key}});
            """,second.CanTryProvideSpan,second.CanProvideSpan,second.GetDirectSpanExpression(enumeratorName));
    }
    private void AppendSetByConsumeLoop(StringBuilder builder,string enumeratorName,string consumerName,string setName,string operationMethod){
        TraversalSupport.AppendTraversalLoop(builder,enumeratorName,source.ElementType,"value",$$"""
            {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
            {{FuncAssignIndented("",keySelector,nameof(keySelector),key,"value")}}
            if(!{{setName}}.{{operationMethod}}({{key}})){
                continue;
            }
            {{consumerName}}.Consume(value);
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(enumeratorName));
    }
    private string SecondKeyExpression(string valueName){
        if(FuncReturnTypeName(keySelector)==second.ElementType){
            return valueName;
        }
        return $"({FuncReturnTypeName(keySelector)})(object?){valueName}!";
    }
    private bool UseIdentityKeySelector=>keySelector.IsIdentity&&FuncReturnTypeName(keySelector)==source.ElementType;

    private bool CanUseSortedIntSetFastPath() =>
        operation.PreloadSecondFirst
        && ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,FuncReturnTypeName(keySelector))
        && IsInt32Type(source.ElementType)
        && IsInt32Type(second.ElementType)
        && source.CanTryProvideSpan
        && second.CanTryProvideSpan;

    private void AppendSortedIntSetFastPath(StringBuilder builder,string sourceName,string secondName,string consumerName) {
        var sourceSpan=$"__setSrcSpan{番号}";
        var secondSpan=$"__setSecSpan{番号}";
        var sorted=$"__setSorted{番号}";
        var j=$"__setJ{番号}";
        var value=$"__setValue{番号}";
        var sourceDirectSpanExpression=source.GetDirectSpanExpression(sourceName);
        var secondDirectSpanExpression=second.GetDirectSpanExpression(secondName);
        if(sourceDirectSpanExpression is not null && secondDirectSpanExpression is not null) {
            builder.AppendLine($"var {sourceSpan}={sourceDirectSpanExpression};");
            builder.AppendLine($"var {secondSpan}={secondDirectSpanExpression};");
            AppendSortedIntSetFastPathBody(builder,sourceSpan,secondSpan,sorted,j,value,consumerName,string.Empty);
            return;
        }
        if(source.CanProvideSpan && second.CanProvideSpan) {
            builder.AppendLine($"{sourceName}.TryGetSpan(out var {sourceSpan});");
            builder.AppendLine($"{secondName}.TryGetSpan(out var {secondSpan});");
            AppendSortedIntSetFastPathBody(builder,sourceSpan,secondSpan,sorted,j,value,consumerName,string.Empty);
            return;
        }
        builder.AppendLine($"if({sourceName}.TryGetSpan(out var {sourceSpan})&&{secondName}.TryGetSpan(out var {secondSpan})){{");
        builder.AppendLine($"    var {sorted}=true;");
        builder.AppendLine($"    for(var __i=1;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"        if({sourceSpan}[__i]<={sourceSpan}[__i-1]){{");
        builder.AppendLine($"            {sorted}=false;");
        builder.AppendLine("            break;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine($"    if({sorted}){{");
        builder.AppendLine($"        for(var __i=1;__i<{secondSpan}.Length;__i++){{");
        builder.AppendLine($"            if({secondSpan}[__i]<={secondSpan}[__i-1]){{");
        builder.AppendLine($"                {sorted}=false;");
        builder.AppendLine("                break;");
        builder.AppendLine("            }");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine($"    if({sorted}){{");
        builder.AppendLine($"        var {j}=0;");
        builder.AppendLine($"        for(var __i=0;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"            var {value}={sourceSpan}[__i];");
        builder.AppendLine($"            while({j}<{secondSpan}.Length&&{secondSpan}[{j}]<{value}){{");
        builder.AppendLine($"                {j}++;");
        builder.AppendLine("            }");
        if(operation.SourceAcceptMethod=="Add") {
            builder.AppendLine($"            if({j}>={secondSpan}.Length||{secondSpan}[{j}]!={value}){{");
            builder.AppendLine($"                {consumerName}.Consume({value});");
            builder.AppendLine("            }");
        } else {
            builder.AppendLine($"            if({j}<{secondSpan}.Length&&{secondSpan}[{j}]=={value}){{");
            builder.AppendLine($"                {consumerName}.Consume({value});");
            builder.AppendLine("            }");
        }
        builder.AppendLine("        }");
        builder.AppendLine("        return;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private void AppendSortedIntSetFastPathBody(
        StringBuilder builder,
        string sourceSpan,
        string secondSpan,
        string sorted,
        string j,
        string value,
        string consumerName,
        string indent) {
        builder.AppendLine($"{indent}var {sorted}=true;");
        builder.AppendLine($"{indent}for(var __i=1;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"{indent}    if({sourceSpan}[__i]<={sourceSpan}[__i-1]){{");
        builder.AppendLine($"{indent}        {sorted}=false;");
        builder.AppendLine($"{indent}        break;");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}if({sorted}){{");
        builder.AppendLine($"{indent}    for(var __i=1;__i<{secondSpan}.Length;__i++){{");
        builder.AppendLine($"{indent}        if({secondSpan}[__i]<={secondSpan}[__i-1]){{");
        builder.AppendLine($"{indent}            {sorted}=false;");
        builder.AppendLine($"{indent}            break;");
        builder.AppendLine($"{indent}        }}");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}if({sorted}){{");
        builder.AppendLine($"{indent}    var {j}=0;");
        builder.AppendLine($"{indent}    for(var __i=0;__i<{sourceSpan}.Length;__i++){{");
        builder.AppendLine($"{indent}        var {value}={sourceSpan}[__i];");
        builder.AppendLine($"{indent}        while({j}<{secondSpan}.Length&&{secondSpan}[{j}]<{value}){{");
        builder.AppendLine($"{indent}            {j}++;");
        builder.AppendLine($"{indent}        }}");
        if(operation.SourceAcceptMethod=="Add") {
            builder.AppendLine($"{indent}        if({j}>={secondSpan}.Length||{secondSpan}[{j}]!={value}){{");
            builder.AppendLine($"{indent}            {consumerName}.Consume({value});");
            builder.AppendLine($"{indent}        }}");
        } else {
            builder.AppendLine($"{indent}        if({j}<{secondSpan}.Length&&{secondSpan}[{j}]=={value}){{");
            builder.AppendLine($"{indent}            {consumerName}.Consume({value});");
            builder.AppendLine($"{indent}        }}");
        }
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    return;");
        builder.AppendLine($"{indent}}}");
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}
