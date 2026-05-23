namespace Seamless.SourceGenerator.Linq.Fragments;

public class DefaultIfEmpty(int 番号, Enumerable source) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) result.Add(e.Current);
            if(result.Count == 0) result.Add(default!);
            return result;
        }
        """;
}

public class DefaultIfEmpty_defaultValue(int 番号, Enumerable source, string defaultValue) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {defaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, {source.ElementType} defaultValue";
    protected override string BuildExpression => "Build(source, defaultValue)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, {{source.ElementType}} defaultValue) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) result.Add(e.Current);
            if(result.Count == 0) result.Add(defaultValue);
            return result;
        }
        """;
}

public class Reverse(int 番号, Enumerable source) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            using var e = source.GetEnumerator();
            while(e.MoveNext()) result.Add(e.Current);
            result.Reverse();
            return result;
        }
        """;
}

public class Chunk(int 番号, Enumerable source, string size) : BufferedSequence(番号, $"{source.ElementType}[]") {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {size})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, int size";
    protected override string BuildExpression => "Build(source, size)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}[]> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, int size) {
            if(size <= 0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}[]>();
            var chunk = new global::System.Collections.Generic.List<{{source.ElementType}}>(size);
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                chunk.Add(e.Current);
                if(chunk.Count == size) {
                    result.Add(chunk.ToArray());
                    chunk.Clear();
                }
            }
            if(chunk.Count > 0) result.Add(chunk.ToArray());
            return result;
        }
        """;
}
