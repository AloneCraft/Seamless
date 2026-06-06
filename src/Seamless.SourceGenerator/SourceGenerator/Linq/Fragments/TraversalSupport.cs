namespace Seamless.SourceGenerator.Linq.Fragments;

internal static class TraversalSupport{
    public static void AppendTraversalLoop(System.Text.StringBuilder builder,string enumeratorName,string elementType,string valueName,string statement){
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

    public static void AppendHashSetInitialization(System.Text.StringBuilder builder,string setName,string elementType,string enumeratorName,string comparer){
        var countName=$"__count_{setName}";
        builder.AppendLine($"global::System.Collections.Generic.HashSet<{elementType}> {setName};");
        builder.AppendLine($"if({enumeratorName}.TryGetNonEnumeratedCount(out var {countName})){{");
        builder.AppendLine($"    {setName}=new global::System.Collections.Generic.HashSet<{elementType}>({countName},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
        builder.AppendLine("}else{");
        builder.AppendLine($"    {setName}=new global::System.Collections.Generic.HashSet<{elementType}>((global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
        builder.AppendLine("}");
    }

    public static string IndentBlock(string text,string indent){
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line=>line.Length==0?line:indent+line));
    }
}
