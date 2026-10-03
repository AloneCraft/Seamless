namespace Seamless.FLinq.Generator;

// direct-consumeの列挙ループを構造化した入力から描画する。
// span fast path、TryGetSpan fallback、TryGetNext loopの順序は既存TraversalSupportと同じ。
internal sealed class EnumeratorTraversalRenderModel(
    string enumeratorName,
    string elementType,
    string valueName,
    string statement,
    bool canTryProvideSpan,
    bool canProvideSpan,
    string? directSpanExpression,
    bool providesRemainingEnumerator = false) {
    internal void AppendTo(StringBuilder builder) {
        var spanName=$"__sp_{enumeratorName}";
        if(directSpanExpression is not null) {
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();");
            builder.AppendLine($"var {spanName}={directSpanExpression};");
            AppendSpanLoop(builder,spanName,valueName,statement);
            return;
        }
        if(canProvideSpan) {
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();");
            builder.AppendLine($"{enumeratorName}.TryGetSpan(out var {spanName});");
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();");
            AppendSpanLoop(builder,spanName,valueName,statement);
            return;
        }
        if(!canTryProvideSpan) {
            builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();");
            this.AppendDrainHead(builder,"");
            builder.AppendLine(TraversalSupport.IndentBlock(statement,"    "));
            builder.AppendLine("}");
            return;
        }
        builder.AppendLine("global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();");
        builder.AppendLine($"if({enumeratorName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine("    global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();");
        AppendSpanLoop(builder,spanName,valueName,statement,"    ");
        builder.AppendLine("}else{");
        builder.AppendLine("    global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanFailed();");
        builder.AppendLine("    global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();");
        this.AppendDrainHead(builder,"    ");
        builder.AppendLine(TraversalSupport.IndentBlock(statement,"        "));
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    // IEnumerable の根は列挙子を局所変数に取って MoveNext/Current を直接回す(Fragment.DrainParts と同じ理由)。
    private void AppendDrainHead(StringBuilder builder,string indent) {
        if(providesRemainingEnumerator) {
            builder.AppendLine($"{indent}var __rest_{enumeratorName}={enumeratorName}.GetRemainingEnumerator();");
            builder.AppendLine($"{indent}while(__rest_{enumeratorName}.MoveNext()){{");
            builder.AppendLine($"{indent}    {elementType} {valueName}=__rest_{enumeratorName}.Current;");
            return;
        }
        builder.AppendLine($"{indent}while({enumeratorName}.TryGetNext(out {elementType} {valueName})){{");
    }

    private void AppendSpanLoop(StringBuilder builder,string spanName,string valueName,string body,string indent = "") {
        builder.AppendLine($"{indent}for(var __i_{enumeratorName}=0;__i_{enumeratorName}<{spanName}.Length;__i_{enumeratorName}++){{");
        builder.AppendLine($"{indent}    var {valueName}={spanName}[__i_{enumeratorName}];");
        builder.AppendLine(TraversalSupport.IndentBlock(body,$"{indent}    "));
        builder.AppendLine($"{indent}}}");
    }
}