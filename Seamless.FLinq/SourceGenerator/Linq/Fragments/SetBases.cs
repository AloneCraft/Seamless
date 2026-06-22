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
    public override string StructMethod=>string.Empty;
    protected override string StructTypeParameters=>"<TEnum1,TEnum2>";
    protected override string StructTypeConstraints=>
        $"where TEnum1:struct,global::System.Collections.Generic.IEnumerator<{source.ElementType}>,global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2:struct,global::System.Collections.Generic.IEnumerator<{second.ElementType}>,global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName=>$"{this.Struct}<{source.EnumeratorTypeName},{second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression},{second.GetEnumeratorExpression},{comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorFields=>operation.PreloadSecondFirst ? $$"""
            private readonly global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{this.lookup}};
            private bool {{this.yielded}};
            private static global::System.Collections.Generic.HashSet<{{source.ElementType}}> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
                    return new global::System.Collections.Generic.HashSet<{{source.ElementType}}>({{this.count}},comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
                }
            }
            """ : $$"""
            private readonly global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{this.lookup}};
            private bool {{this.yielded}};
            """;
    public override string GetEnumeratorConstructor=>operation.PreloadSecondFirst
        ? $"this.{this.lookup}=CreateLookup(this.second,this.comparer);"
        : $"this.{this.lookup}=new(this.comparer);";
    public override string GetEnumerableExpression=>operation.GetEnumerableExpression(source,second,comparer);
    public override string GetTryGetNextBody=>operation.PreloadSecondFirst ? $$"""
            if(!this.{{this.yielded}}){
                while(second.TryGetNext(out var {{this.item}})){
                    this.{{this.lookup}}.Add({{this.SecondValueExpression(this.item)}});
                }
                this.{{this.yielded}}=true;
            }
            while(source.TryGetNext(out var {{this.item}})){
                if(this.{{this.lookup}}.{{operation.SourceAcceptMethod}}({{this.item}})){
                    current=this.{{this.currentValue}}={{this.item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """ : $$"""
            while(source.TryGetNext(out var {{this.item}})){
                if(this.{{this.lookup}}.{{operation.SourceAcceptMethod}}({{this.item}})){
                    current=this.{{this.currentValue}}={{this.item}};
                    return true;
                }
            }
            if(!this.{{this.yielded}}){
                this.{{this.yielded}}=true;
                while(second.TryGetNext(out var {{this.item}})){
                    var value=({{source.ElementType}})(object?){{this.item}}!;
                    if(this.{{this.lookup}}.Add(value)){
                        current=this.{{this.currentValue}}=value;
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression=> this.currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>"source.Dispose();\nsecond.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var secondName=$"__sec{番号}";
        var setName=$"__set{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        TraversalSupport.AppendHashSetInitialization(builder,setName,source.ElementType,operation.PreloadSecondFirst?secondName:sourceName,comparer);
        if(!operation.PreloadSecondFirst){
            TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
                if({{setName}}.Add(value)){
                    {{consumerName}}.Consume(value);
                }
                """,source.CanTryProvideSpan);
            TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
                if({{setName}}.Add({{this.SecondValueExpression("value")}})){
                    {{consumerName}}.Consume({{this.SecondValueExpression("value")}});
                }
                """,second.CanTryProvideSpan);
            return builder.ToString().TrimEnd();
        }
        TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
            {{setName}}.Add({{this.SecondValueExpression("value")}});
            """,second.CanTryProvideSpan);
        TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
            if({{setName}}.{{operation.SourceAcceptMethod}}(value)){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan);
        return builder.ToString().TrimEnd();
    }
    private string SecondValueExpression(string valueName){
        if(source.ElementType==second.ElementType){
            return valueName;
        }
        return $"({source.ElementType})(object?){valueName}!";
    }
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
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructTypeParameters=>"<TEnum1,TEnum2>";
    protected override string StructTypeConstraints=>
        $"where TEnum1:struct,global::System.Collections.Generic.IEnumerator<{source.ElementType}>,global::Seamless.Linq.IValueEnumerator<{source.ElementType}>\n    where TEnum2:struct,global::System.Collections.Generic.IEnumerator<{second.ElementType}>,global::Seamless.Linq.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName=>$"{this.Struct}<{source.EnumeratorTypeName},{second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression},{second.GetEnumeratorExpression},{comparer}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields=>$$"""
        private readonly global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> {{this.lookup}};
        private byte {{this.state}};
        private static global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer){
            if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
                return new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>({{this.count}},comparer);
            }else{
                return new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            }
        }
        """;
    public override string GetEnumeratorConstructor=>$"this.{this.lookup}=CreateLookup(this.second,this.comparer);";
    public override string GetEnumerableExpression=>operation.GetEnumerableExpression(source,second,keySelector,comparer);
    public override string GetTryGetNextBody=> this.UseIdentityKeySelector ? this.GetTryGetNextIdentityBody : operation.PreloadSecondFirst ? $$"""
            if(this.{{this.state}}==0){
                while(second.TryGetNext(out var {{this.temp}})) this.{{this.lookup}}.Add({{this.SecondKeyExpression(this.temp)}});
                this.{{this.state}}=1;
            }
            while(source.TryGetNext(out var {{this.item}})){
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
                if(this.{{this.lookup}}.{{operation.SourceAcceptMethod}}({{this.key}})){
                    current=this.{{this.currentValue}}={{this.item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """ : $$"""
            while(source.TryGetNext(out var {{this.item}})){
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
                if(this.{{this.lookup}}.{{operation.SourceAcceptMethod}}({{this.key}})){
                    current=this.{{this.currentValue}}={{this.item}};
                    return true;
                }
            }
            if(this.{{this.state}}==0){
                this.{{this.state}}=1;
                while(second.TryGetNext(out var {{this.temp}})){
                    var {{this.item}}=({{source.ElementType}})(object?){{this.temp}}!;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                    if(this.{{this.lookup}}.Add({{this.key}})){
                        current=this.{{this.currentValue}}={{this.item}};
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """;
    private string GetTryGetNextIdentityBody=>operation.PreloadSecondFirst ? $$"""
            if(this.{{this.state}}==0){
                while(second.TryGetNext(out var {{this.temp}})) this.{{this.lookup}}.Add({{this.temp}});
                this.{{this.state}}=1;
            }
            while(source.TryGetNext(out var {{this.item}})){
                if(this.{{this.lookup}}.{{operation.SourceAcceptMethod}}({{this.item}})){
                    current=this.{{this.currentValue}}={{this.item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """ : $$"""
            while(source.TryGetNext(out var {{this.item}})){
                if(this.{{this.lookup}}.{{operation.SourceAcceptMethod}}({{this.item}})){
                    current=this.{{this.currentValue}}={{this.item}};
                    return true;
                }
            }
            if(this.{{this.state}}==0){
                this.{{this.state}}=1;
                while(second.TryGetNext(out var {{this.temp}})){
                    if(this.{{this.lookup}}.Add({{this.temp}})){
                        current=this.{{this.currentValue}}={{this.temp}};
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression=> this.currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>"source.Dispose();\nsecond.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var secondName=$"__sec{番号}";
        var setName=$"__set{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        var hoistedDeclarations=this.HoistedLocalDeclarations(keySelector);
        if(!string.IsNullOrEmpty(hoistedDeclarations)) {
            builder.AppendLine(hoistedDeclarations);
        }
        TraversalSupport.AppendHashSetInitialization(builder,setName,FuncReturnTypeName(keySelector),secondName,comparer);
        if(this.UseIdentityKeySelector){
            if(!operation.PreloadSecondFirst){
                TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
                    if({{setName}}.Add(value)){
                        {{consumerName}}.Consume(value);
                    }
                    """,source.CanTryProvideSpan);
                TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
                    if({{setName}}.Add(value)){
                        {{consumerName}}.Consume(value);
                    }
                    """,second.CanTryProvideSpan);
                return builder.ToString().TrimEnd();
            }
            TraversalSupport.AppendTraversalLoop(builder,secondName,second.ElementType,"value",$$"""
                {{setName}}.Add(value);
                """,second.CanTryProvideSpan);
            TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
                if(!{{setName}}.{{operation.SourceAcceptMethod}}(value)){
                    continue;
                }
                {{consumerName}}.Consume(value);
                """,source.CanTryProvideSpan);
            return builder.ToString().TrimEnd();
        }
        this.AppendSetByCollectLoop(builder,secondName,setName);
        this.AppendSetByConsumeLoop(builder,sourceName,consumerName,setName,operation.SourceAcceptMethod);
        return builder.ToString().TrimEnd();
    }
    private void AppendSetByCollectLoop(StringBuilder builder,string enumeratorName,string setName){
        TraversalSupport.AppendTraversalLoop(builder,enumeratorName,second.ElementType,"value",$$"""
            {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
            {{this.FuncAssignIndented("",keySelector,nameof(keySelector),this.key,"value")}}
            {{setName}}.Add({{this.key}});
            """,second.CanTryProvideSpan);
    }
    private void AppendSetByConsumeLoop(StringBuilder builder,string enumeratorName,string consumerName,string setName,string operationMethod){
        TraversalSupport.AppendTraversalLoop(builder,enumeratorName,source.ElementType,"value",$$"""
            {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
            {{this.FuncAssignIndented("",keySelector,nameof(keySelector),this.key,"value")}}
            if(!{{setName}}.{{operationMethod}}({{this.key}})){
                continue;
            }
            {{consumerName}}.Consume(value);
            """,source.CanTryProvideSpan);
    }
    private string SecondKeyExpression(string valueName){
        if(FuncReturnTypeName(keySelector)==second.ElementType){
            return valueName;
        }
        return $"({FuncReturnTypeName(keySelector)})(object?){valueName}!";
    }
    private bool UseIdentityKeySelector=>keySelector.IsIdentity&&FuncReturnTypeName(keySelector)==source.ElementType;
}
