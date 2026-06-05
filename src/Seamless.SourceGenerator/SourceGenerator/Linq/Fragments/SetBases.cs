namespace Seamless.SourceGenerator.Linq.Fragments;

public abstract class SetBase(int 番号,Enumerable source,Enumerable second,string comparer,string operation):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    protected override string StructTypeParameters=>"<TEnum1,TEnum2>";
    protected override string StructTypeConstraints=>
        $"where TEnum1:struct,global::System.Collections.Generic.IEnumerator<{source.ElementType}>,global::Seamless.Linq.Carrier.IValueEnumerator<{source.ElementType}>\n    where TEnum2:struct,global::System.Collections.Generic.IEnumerator<{second.ElementType}>,global::Seamless.Linq.Carrier.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName=>$"{Struct}<{source.EnumeratorTypeName},{second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression},{second.GetEnumeratorExpression},{comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorFields=>operation switch{
        "union"=>$$"""
            private readonly global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{lookup}}=new(comparer);
            private bool {{yielded}};
            """,
        _=>$$"""
            private readonly global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{lookup}}=CreateLookup(second,comparer);
            private bool {{yielded}};
            private static global::System.Collections.Generic.HashSet<{{source.ElementType}}> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
                if(source.TryGetNonEnumeratedCount(out var {{count}})){
                    return new global::System.Collections.Generic.HashSet<{{source.ElementType}}>({{count}},comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
                }
            }
            """
    };
    public override string GetEnumerableExpression=>operation switch{
        "except"=>$"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Except({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})",
        "intersect"=>$"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Intersect({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})",
        _=>$"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.Union({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})",
    };
    public override string GetTryGetNextBody=>operation switch{
        "except"=>$$"""
            if(!this.{{yielded}}){
                while(second.TryGetNext(out var {{item}})) this.{{lookup}}.Add(({{source.ElementType}})(object?){{item}}!);
                this.{{yielded}}=true;
            }
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.Add({{item}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """,
        "intersect"=>$$"""
            if(!this.{{yielded}}){
                while(second.TryGetNext(out var {{item}})) this.{{lookup}}.Add(({{source.ElementType}})(object?){{item}}!);
                this.{{yielded}}=true;
            }
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.Remove({{item}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """,
        _=>$$"""
            while(source.TryGetNext(out var {{item}})){
                if(this.{{lookup}}.Add({{item}})){
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
            """
    };
    public override string GetCurrentExpression=>currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>"source.Dispose();\nsecond.Dispose();";
    public string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var secondName=$"__sec{番号}";
        var setName=$"__set{番号}";
        var builder=new System.Text.StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        builder.AppendLine($"var {setName}=new global::System.Collections.Generic.HashSet<{source.ElementType}>((global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>?){comparer});");
        if(operation=="union"){
            AppendConsumeLoop(builder,sourceName,$"{source.ElementType}","value",$"if({setName}.Add(value)) {consumerName}.Consume(value);");
            AppendConsumeLoop(builder,secondName,second.ElementType,"value",$"if({setName}.Add(({source.ElementType})(object?)value!)) {consumerName}.Consume(({source.ElementType})(object?)value!);");
            return builder.ToString().TrimEnd();
        }
        AppendCollectLoop(builder,secondName,second.ElementType,$"{setName}.Add(({source.ElementType})(object?)value!);");
        if(operation=="except"){
            AppendConsumeLoop(builder,sourceName,source.ElementType,"value",$"if({setName}.Add(value)) {consumerName}.Consume(value);");
        }else{
            AppendConsumeLoop(builder,sourceName,source.ElementType,"value",$"if({setName}.Remove(value)) {consumerName}.Consume(value);");
        }
        return builder.ToString().TrimEnd();
    }
    private static void AppendCollectLoop(System.Text.StringBuilder builder,string enumeratorName,string elementType,string statement){
        var spanName=$"__sp_{enumeratorName}";
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"    foreach(var value in {spanName}){{");
        builder.AppendLine($"        {statement}");
        builder.AppendLine("    }");
        builder.AppendLine("}else{");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out {elementType} value)){{");
        builder.AppendLine($"        {statement}");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }
    private static void AppendConsumeLoop(System.Text.StringBuilder builder,string enumeratorName,string elementType,string valueName,string statement){
        var spanName=$"__sp_{enumeratorName}";
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"    foreach(var {valueName} in {spanName}){{");
        builder.AppendLine($"        {statement}");
        builder.AppendLine("    }");
        builder.AppendLine("}else{");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out {elementType} {valueName})){{");
        builder.AppendLine($"        {statement}");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }
}

public abstract class SetByBase(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector,string comparer,string operation):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>FuncSupport(keySelector,nameof(keySelector));
    protected override string StructTypeParameters=>"<TEnum1,TEnum2>";
    protected override string StructTypeConstraints=>
        $"where TEnum1:struct,global::System.Collections.Generic.IEnumerator<{source.ElementType}>,global::Seamless.Linq.Carrier.IValueEnumerator<{source.ElementType}>\n    where TEnum2:struct,global::System.Collections.Generic.IEnumerator<{second.ElementType}>,global::Seamless.Linq.Carrier.IValueEnumerator<{second.ElementType}>";
    public override string EnumeratorTypeName=>$"{Struct}<{source.EnumeratorTypeName},{second.EnumeratorTypeName}>";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression},{second.GetEnumeratorExpression},{comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields=>$$"""
        private readonly global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> {{lookup}}=CreateLookup(second,comparer);
        private byte {{state}};
        private static global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer){
            if(source.TryGetNonEnumeratedCount(out var {{count}})){
                return new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>({{count}},comparer);
            }else{
                return new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            }
        }
        """;
    public override string GetEnumerableExpression=>operation switch{
        "except"=>$"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.ExceptBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})",
        "intersect"=>$"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.IntersectBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})",
        _=>$"global::Seamless.Linq.Carrier.ValueEnumerableExtensions.UnionBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})",
    };
    public override string GetTryGetNextBody=>operation switch{
        "except"=>$$"""
            if(this.{{state}}==0){
                while(second.TryGetNext(out var {{temp}})) this.{{lookup}}.Add(({{FuncReturnTypeName(keySelector)}})(object?){{temp}}!);
                this.{{state}}=1;
            }
            while(source.TryGetNext(out var {{item}})){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.Add({{key}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """,
        "intersect"=>$$"""
            if(this.{{state}}==0){
                while(second.TryGetNext(out var {{temp}})) this.{{lookup}}.Add(({{FuncReturnTypeName(keySelector)}})(object?){{temp}}!);
                this.{{state}}=1;
            }
            while(source.TryGetNext(out var {{item}})){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.Remove({{key}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            current=default!;
            return false;
            """,
        _=>$$"""
            while(source.TryGetNext(out var {{item}})){
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(this.{{lookup}}.Add({{key}})){
                    current=this.{{currentValue}}={{item}};
                    return true;
                }
            }
            if(this.{{state}}==0){
                this.{{state}}=1;
                while(second.TryGetNext(out var {{temp}})){
                    var {{item}}=({{source.ElementType}})(object?){{temp}}!;
                    {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                    {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                    if(this.{{lookup}}.Add({{key}})){
                        current=this.{{currentValue}}={{item}};
                        return true;
                    }
                }
            }
            current=default!;
            return false;
            """
    };
    public override string GetCurrentExpression=>currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>"source.Dispose();\nsecond.Dispose();";
    public string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var secondName=$"__sec{番号}";
        var setName=$"__set{番号}";
        var builder=new System.Text.StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"using var {secondName}={second.GetEnumeratorExpression};");
        builder.AppendLine($"var {setName}=new global::System.Collections.Generic.HashSet<{FuncReturnTypeName(keySelector)}>((global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>?){comparer});");
        AppendSetByCollectLoop(builder,secondName,second.ElementType,setName);
        AppendSetByConsumeLoop(builder,sourceName,consumerName,setName,operation);
        return builder.ToString().TrimEnd();
    }
    private void AppendSetByCollectLoop(System.Text.StringBuilder builder,string enumeratorName,string elementType,string setName){
        var spanName=$"__sp_{enumeratorName}";
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"    foreach(var value in {spanName}){{");
        builder.AppendLine($"        {FuncReturnTypeName(keySelector)} {key}=default!;");
        builder.AppendLine(IndentBlock(FuncAssign(keySelector,nameof(keySelector),key,"value"),"        "));
        builder.AppendLine($"        {setName}.Add({key});");
        builder.AppendLine("    }");
        builder.AppendLine("}else{");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out {elementType} value)){{");
        builder.AppendLine($"        {FuncReturnTypeName(keySelector)} {key}=default!;");
        builder.AppendLine(IndentBlock(FuncAssign(keySelector,nameof(keySelector),key,"value"),"        "));
        builder.AppendLine($"        {setName}.Add({key});");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }
    private void AppendSetByConsumeLoop(System.Text.StringBuilder builder,string enumeratorName,string consumerName,string setName,string operationName){
        var spanName=$"__sp_{enumeratorName}";
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"    foreach(var value in {spanName}){{");
        builder.AppendLine($"        {FuncReturnTypeName(keySelector)} {key}=default!;");
        builder.AppendLine(IndentBlock(FuncAssign(keySelector,nameof(keySelector),key,"value"),"        "));
        if(operationName=="except"){
            builder.AppendLine($"        if(!{setName}.Add({key})){{");
        }else if(operationName=="intersect"){
            builder.AppendLine($"        if(!{setName}.Remove({key})){{");
        }else{
            builder.AppendLine($"        if(!{setName}.Add({key})){{");
        }
        builder.AppendLine("            continue;");
        builder.AppendLine("        }");
        builder.AppendLine($"        {consumerName}.Consume(value);");
        builder.AppendLine("    }");
        builder.AppendLine("}else{");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out {source.ElementType} value)){{");
        builder.AppendLine($"        {FuncReturnTypeName(keySelector)} {key}=default!;");
        builder.AppendLine(IndentBlock(FuncAssign(keySelector,nameof(keySelector),key,"value"),"        "));
        if(operationName=="except"){
            builder.AppendLine($"        if(!{setName}.Add({key})){{");
        }else if(operationName=="intersect"){
            builder.AppendLine($"        if(!{setName}.Remove({key})){{");
        }else{
            builder.AppendLine($"        if(!{setName}.Add({key})){{");
        }
        builder.AppendLine("            continue;");
        builder.AppendLine("        }");
        builder.AppendLine($"        {consumerName}.Consume(value);");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }
    private static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0?line:indent+line));
    }
}
