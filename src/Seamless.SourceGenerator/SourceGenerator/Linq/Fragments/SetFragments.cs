namespace Seamless.SourceGenerator.Linq.Fragments;

public class Distinct(int 番号, Enumerable source) : Distinct_comparer(番号, source, "null") { }

public class Distinct_comparer(int 番号, Enumerable source, string comparer) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string BuildExpression => "Build(source, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                if(seen.Add(e.Current)) result.Add(e.Current);
            }
            return result;
        }
        """;
}

public class DistinctBy(int 番号, Enumerable source, FuncFragment keySelector) : DistinctBy_comparer(番号, source, keySelector, "null") { }

public class DistinctBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => keySelector.CanInlineAssign ? string.Empty : this.DefineStruct(keySelector, nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => keySelector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {comparer})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(keySelector,nameof(keySelector))}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => keySelector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}> keySelector, global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer";
    protected override string BuildExpression => "Build(source, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                var {{this.key}} = {{this.FuncInvoke(keySelector,nameof(keySelector),this.item)}};
                if(seen.Add({{this.key}})) result.Add({{this.item}});
            }
            return result;
        }
        """;
}

public class Union(int 番号, Enumerable source, Enumerable second) : Union_comparer(番号, source, second, "null") { }

public class Union_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string BuildExpression => "Build(source, second, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var seen = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            using(var e = source.GetEnumerator()) while(e.MoveNext()) if(seen.Add(e.Current)) result.Add(e.Current);
            using(var e = second.GetEnumerator()) while(e.MoveNext()) {
                var value = ({{source.ElementType}})(object)e.Current!;
                if(seen.Add(value)) result.Add(value);
            }
            return result;
        }
        """;
}

public class Except(int 番号, Enumerable source, Enumerable second) : Except_comparer(番号, source, second, "null") { }

public class Except_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string BuildExpression => "Build(source, second, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var excluded = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            using(var e = second.GetEnumerator()) while(e.MoveNext()) excluded.Add(({{source.ElementType}})(object)e.Current!);
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var yielded = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            using(var e = source.GetEnumerator()) while(e.MoveNext()) if(!excluded.Contains(e.Current) && yielded.Add(e.Current)) result.Add(e.Current);
            return result;
        }
        """;
}

public class Intersect(int 番号, Enumerable source, Enumerable second) : Intersect_comparer(番号, source, second, "null") { }

public class Intersect_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : BufferedSequence(番号, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct+"\n"+second.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {second.GetEnumerableExpression}, {comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Collections.Generic.IEnumerable<{second.ElementType}> second, global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    protected override string BuildExpression => "Build(source, second, comparer)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Collections.Generic.IEnumerable<{{second.ElementType}}> second, global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer) {
            var allowed = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            using(var e = second.GetEnumerator()) while(e.MoveNext()) allowed.Add(({{source.ElementType}})(object)e.Current!);
            var result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            var yielded = new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            using(var e = source.GetEnumerator()) while(e.MoveNext()) if(allowed.Contains(e.Current) && yielded.Add(e.Current)) result.Add(e.Current);
            return result;
        }
        """;
}
