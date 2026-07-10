namespace Seamless.FLinq.Linq.Fragments;

internal static class TraversalSupport{
    public static void AppendTraversalLoop(
        StringBuilder builder,
        string enumeratorName,
        string elementType,
        string valueName,
        string statement,
        bool canTryProvideSpan,
        bool canProvideSpan=false,
        string? directSpanExpression=null){
        var spanName=$"__sp_{enumeratorName}";
        if(directSpanExpression is not null){
            // Direct span roots avoid probing and fallback code.
            // Example: int[] first; first.Except(second).Consume(c) emits
            // `var __sp___src0=__src0.GetSpan(); for(...) ...`.
            // IEnumerable<T>, Tsavorite, and Persisted roots do not expose a direct span expression.
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();");
            builder.AppendLine($"var {spanName}={directSpanExpression};");
            AppendSpanLoop(builder,spanName,enumeratorName,valueName,statement);
            return;
        }
        if(canProvideSpan){
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();");
            builder.AppendLine($"{enumeratorName}.TryGetSpan(out var {spanName});");
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();");
            AppendSpanLoop(builder,spanName,enumeratorName,valueName,statement);
            return;
        }
        if(!canTryProvideSpan){
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();");
            builder.AppendLine($"while({enumeratorName}.TryGetNext(out {elementType} {valueName})){{");
            builder.AppendLine(IndentBlock(statement,"    "));
            builder.AppendLine("}");
            return;
        }
        builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();");
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine("    global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();");
        AppendSpanLoop(builder,spanName,enumeratorName,valueName,statement,"    ");
        builder.AppendLine("}else{");
        builder.AppendLine("    global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanFailed();");
        builder.AppendLine("    global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out {elementType} {valueName})){{");
        builder.AppendLine(IndentBlock(statement,"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static void AppendSpanLoop(StringBuilder builder,string spanName,string enumeratorName,string valueName,string statement,string indent=""){
        builder.AppendLine($"{indent}for(var __i_{enumeratorName}=0;__i_{enumeratorName}<{spanName}.Length;__i_{enumeratorName}++){{");
        builder.AppendLine($"{indent}    var {valueName}={spanName}[__i_{enumeratorName}];");
        builder.AppendLine(IndentBlock(statement,$"{indent}    "));
        builder.AppendLine($"{indent}}}");
    }

    public static void AppendHashSetInitialization(StringBuilder builder,string setName,string elementType,string enumeratorName,string comparer,string? capacityHint=null){
        var countName=$"__count_{setName}";
        if(capacityHint is not null){
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();");
            builder.AppendLine($"using var {setName}=new global::Seamless.System.Carrier.HashSetSlim<{elementType}>({capacityHint},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
            return;
        }
        var hasCountName=$"__hasCount_{setName}";
        builder.AppendLine($"var {countName}=0;");
        builder.AppendLine($"var {hasCountName}={enumeratorName}.TryGetNonEnumeratedCount(out {countName});");
        builder.AppendLine($"if({hasCountName}) global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();");
        builder.AppendLine($"using var {setName}={hasCountName}");
        builder.AppendLine($"    ? new global::Seamless.System.Carrier.HashSetSlim<{elementType}>({countName},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer})");
        builder.AppendLine($"    : new global::Seamless.System.Carrier.HashSetSlim<{elementType}>((global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
    }

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0?line:indent+line));
    }
}
