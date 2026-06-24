namespace Seamless.FLinq.Linq.Fragments;

internal static class TraversalSupport{
    public static void AppendTraversalLoop(StringBuilder builder,string enumeratorName,string elementType,string valueName,string statement,bool canTryProvideSpan){
        // span を提供できない断片（PipelineEnumerable など TryGetSpan が常に false を返す）では
        // span 分岐は到達不能なので while(TryGetNext) だけを生成する。
        if(!canTryProvideSpan){
            builder.AppendLine($"while({enumeratorName}.TryGetNext(out {elementType} {valueName})){{");
            builder.AppendLine(IndentBlock(statement,"    "));
            builder.AppendLine("}");
            return;
        }
        var spanName=$"__sp_{enumeratorName}";
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"    for(var __i_{enumeratorName}=0;__i_{enumeratorName}<{spanName}.Length;__i_{enumeratorName}++){{");
        builder.AppendLine($"        var {valueName}={spanName}[__i_{enumeratorName}];");
        builder.AppendLine(IndentBlock(statement,"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}else{");
        builder.AppendLine($"    while({enumeratorName}.TryGetNext(out {elementType} {valueName})){{");
        builder.AppendLine(IndentBlock(statement,"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    public static void AppendHashSetInitialization(StringBuilder builder,string setName,string elementType,string enumeratorName,string comparer,string? capacityHint=null){
        var countName=$"__count_{setName}";
        if(capacityHint is not null){
            builder.AppendLine($"using var {setName}=new global::Seamless.System.Carrier.HashSetSlim<{elementType}>({capacityHint},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
            return;
        }
        var hasCountName=$"__hasCount_{setName}";
        builder.AppendLine($"var {countName}=0;");
        builder.AppendLine($"var {hasCountName}={enumeratorName}.TryGetNonEnumeratedCount(out {countName});");
        builder.AppendLine($"using var {setName}={hasCountName}");
        builder.AppendLine($"    ? new global::Seamless.System.Carrier.HashSetSlim<{elementType}>({countName},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer})");
        builder.AppendLine($"    : new global::Seamless.System.Carrier.HashSetSlim<{elementType}>((global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
    }

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0?line:indent+line));
    }
}
