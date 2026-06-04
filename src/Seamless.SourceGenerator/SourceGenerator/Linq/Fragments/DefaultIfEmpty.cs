namespace Seamless.SourceGenerator.Linq.Fragments;

public class DefaultIfEmpty(int 番号, Enumerable source) : BufferedSequence(番号, source.ElementType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source) {
                source.TryGetSpan(out var __span);
                var result=new global::System.Collections.Generic.List<{{source.ElementType}}>(__span.Length==0?1:__span.Length);
                foreach(var item in __span) result.Add(item);
                if(result.Count==0) result.Add(default!);
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source) {
                var result=new global::System.Collections.Generic.List<{{source.ElementType}}>();
                while(source.MoveNext()) result.Add(source.Current);
                if(result.Count==0) result.Add(default!);
                return result;
            }
            """;
}

public class DefaultIfEmpty_defaultValue(int 番号, Enumerable source, string defaultValue) : BufferedSequence(番号, source.ElementType) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source, {source.ElementType} defaultValue";
    protected override string BuildExpression => "Build(source, defaultValue)";
    protected override string BuildMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, {{source.ElementType}} defaultValue) {
                source.TryGetSpan(out var __span);
                var result=new global::System.Collections.Generic.List<{{source.ElementType}}>(__span.Length==0?1:__span.Length);
                foreach(var item in __span) result.Add(item);
                if(result.Count==0) result.Add(defaultValue);
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source, {{source.ElementType}} defaultValue) {
                var result=new global::System.Collections.Generic.List<{{source.ElementType}}>();
                while(source.MoveNext()) result.Add(source.Current);
                if(result.Count==0) result.Add(defaultValue);
                return result;
            }
            """;
}
