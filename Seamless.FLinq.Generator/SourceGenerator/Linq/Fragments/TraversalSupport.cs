namespace Seamless.FLinq;

internal static class TraversalSupport {
    public static void AppendTraversalLoop(
        StringBuilder builder,
        string enumeratorName,
        string elementType,
        string valueName,
        string statement,
        bool canTryProvideSpan,
        bool canProvideSpan = false,
        string? directSpanExpression = null) =>
        new EnumeratorTraversalRenderModel(
            enumeratorName,
            elementType,
            valueName,
            statement,
            canTryProvideSpan,
            canProvideSpan,
            directSpanExpression).AppendTo(builder);
    public static void AppendHashSetInitialization(StringBuilder builder,string setName,string elementType,string enumeratorName,string comparer,string? capacityHint = null) {
        var countName = $"__count_{setName}";
        if(capacityHint is not null) {
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();");
            builder.AppendLine($"using var {setName}=new global::Seamless.Linq.HashSetSlim<{elementType}>({capacityHint},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
            return;
        }
        var hasCountName = $"__hasCount_{setName}";
        builder.AppendLine($"var {countName}=0;");
        builder.AppendLine($"var {hasCountName}={enumeratorName}.TryGetNonEnumeratedCount(out {countName});");
        builder.AppendLine($"if({hasCountName}) global::Seamless.Linq.LinqCounterScope.IncrementHashCapacityHintUsed();");
        builder.AppendLine($"using var {setName}={hasCountName}");
        builder.AppendLine($"    ? new global::Seamless.Linq.HashSetSlim<{elementType}>({countName},(global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer})");
        builder.AppendLine($"    : new global::Seamless.Linq.HashSetSlim<{elementType}>((global::System.Collections.Generic.IEqualityComparer<{elementType}>?){comparer});");
    }

    public static string IndentBlock(string text,string indent) {
        return string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(line => line.Length==0 ? line : indent+line));
    }
}
