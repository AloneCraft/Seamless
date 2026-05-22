namespace Seamless.SourceGenerator.Linq.Fragments;

file static class GeneratedMethodFragments {
    public static string Execute(Enumerable source,string bufferName) => $$"""
        {{source.GetBeginLoopOutside}}
        var {{bufferName}}=new global::System.Collections.Generic.List<{{source.ElementType}}>();
        {{source.GetBeginLoop}}
        {{source.GetBeginLoopInside}}
        {{bufferName}}.Add({{source.GetElement}});
        {{source.GetEndLoopInside}}
        {{source.GetEndLoop}}
        {{source.GetEndLoopOutside}}
        {{source.GetEndGlobal}}
        """;
}

public abstract class GeneratedSequenceFragment(int 番号, Enumerable source, string elementType) : Enumerable(番号) {
    protected Enumerable Source => source;
    protected string MaterializedSource => $"{this.result}_source";
    protected abstract string Invocation { get; }
    public override bool CanExpandInLocalMethodBody => false;
    protected virtual string EnumeratorInvocation => this.Invocation.Replace(this.MaterializedSource, source.GetEnumerableExpression, System.StringComparison.Ordinal);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.EnumeratorInvocation})";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.IEnumerator<{{this.ElementType}}> {{this.e}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{this.ElementType}}> source) {
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = default!;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            {{this.currentValue}} = {{this.e}}.Current;
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;

    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        """;

    public override string GetBeginLoopOutside => $$"""
        {{GeneratedMethodFragments.Execute(source,this.MaterializedSource)}}
        var {{this.result}}={{this.Invocation}};
        using var {{this.e}}={{this.result}}.GetEnumerator();
        """;

    public override string GetBeginLoop => $$"""
        goto {{this.Start}};
        {{this.Head}}:
        var {{this.item}} = {{this.e}}.Current;
        """;

    public override string GetBeginLoopInside => string.Empty;
    public override string GetEndLoopInside => string.Empty;

    public override string GetEndLoop => $$"""
        {{this.Start}}:
        if ({{this.e}}.MoveNext()) goto {{this.Head}};
        """;

    public override string GetEndLoopOutside => string.Empty;
    public override string GetEndGlobal => string.Empty;
    public override string GetAdd => string.Empty;
    public override string GetElement => this.item;
    public override string MoveNext => $"{this.e}.MoveNext()";
    public override string ElementType => elementType;
    public override string Result => this.result;
}

public abstract class GeneratedBinarySequenceFragment(int 番号, Enumerable source, Enumerable second, string elementType) : Enumerable(番号) {
    protected Enumerable Source => source;
    protected Enumerable Second => second;
    protected string MaterializedSource => $"{this.result}_source";
    protected string MaterializedSecond => $"{this.result}_second";
    protected abstract string Invocation { get; }
    public override bool CanExpandInLocalMethodBody => false;
    protected virtual string EnumeratorInvocation => this.Invocation
        .Replace(this.MaterializedSource, source.GetEnumerableExpression, System.StringComparison.Ordinal)
        .Replace(this.MaterializedSecond, second.GetEnumerableExpression, System.StringComparison.Ordinal);
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{second.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.EnumeratorInvocation})";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.IEnumerator<{{this.ElementType}}> {{this.e}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{this.ElementType}}> source) {
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = default!;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            {{this.currentValue}} = {{this.e}}.Current;
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;

    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        {{second.GetBeginGlobal}}
        """;

    public override string GetBeginLoopOutside => $$"""
        {{GeneratedMethodFragments.Execute(source,this.MaterializedSource)}}
        {{GeneratedMethodFragments.Execute(second,this.MaterializedSecond)}}
        var {{this.result}}={{this.Invocation}};
        using var {{this.e}}={{this.result}}.GetEnumerator();
        """;

    public override string GetBeginLoop => $$"""
        goto {{this.Start}};
        {{this.Head}}:
        var {{this.item}} = {{this.e}}.Current;
        """;

    public override string GetBeginLoopInside => string.Empty;
    public override string GetEndLoopInside => string.Empty;

    public override string GetEndLoop => $$"""
        {{this.Start}}:
        if ({{this.e}}.MoveNext()) goto {{this.Head}};
        """;

    public override string GetEndLoopOutside => string.Empty;
    public override string GetEndGlobal => string.Empty;
    public override string GetAdd => string.Empty;
    public override string GetElement => this.item;
    public override string MoveNext => $"{this.e}.MoveNext()";
    public override string ElementType => elementType;
    public override string Result => this.result;
}

public abstract class GeneratedScalarFragment(int 番号, Enumerable source) : Scalar(番号) {
    protected Enumerable Source => source;
    protected string MaterializedSource => $"{this.result}_source";
    protected abstract string Invocation { get; }
    public override string GetEnumeratorStruct => $$"""
        private struct {{Struct}}<TResult> : global::Seamless.Linq.Carrier.IValueEnumerator<TResult> {
            {{this.GetEnumeratorFields}}
            {{this.GetEnumeratorConstructor}}
            public TResult Current => {{this.GetCurrentExpression}};
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<TResult> span) {
                span = default;
                return false;
            }
            public bool TryCopyTo(scoped global::System.Span<TResult> destination, global::System.Index offset) => false;
            public bool TryGetNext(out TResult current) {
                {{this.GetTryGetNextBody}}
            }
            public void Dispose() {
            }
        }
        """;
    public override string GetEnumeratorFields => $$"""
        private readonly TResult {{this.result}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(TResult result) {
            {{this.result}} = result;
            {{this.yielded}} = false;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default!;
            return false;
        }
        {{this.yielded}} = true;
        current = {{this.result}};
        return true;
        """;
    public override string GetCurrentExpression => this.result;

    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        """;

    public override string GetBeginLoopOutside => $$"""
        {{GeneratedMethodFragments.Execute(source,this.MaterializedSource)}}
        var {{this.result}}={{this.Invocation}};
        """;

    public override string GetBeginLoop => string.Empty;
    public override string GetBeginLoopInside => string.Empty;
    public override string GetEndLoopInside => string.Empty;
    public override string GetEndLoop => string.Empty;
    public override string GetEndLoopOutside => string.Empty;
    public override string GetEndGlobal => string.Empty;
    public override string Result => this.result;
}

public abstract class GeneratedBinaryScalarFragment(int 番号, Enumerable source, Enumerable second) : Scalar(番号) {
    protected Enumerable Source => source;
    protected Enumerable Second => second;
    protected string MaterializedSource => $"{this.result}_source";
    protected string MaterializedSecond => $"{this.result}_second";
    protected abstract string Invocation { get; }
    public override string GetEnumeratorStruct => $$"""
        private struct {{Struct}}<TResult> : global::Seamless.SourceGenerator.Linq.Carrier.IValueEnumerator<TResult> {
            {{this.GetEnumeratorFields}}
            {{this.GetEnumeratorConstructor}}
            public TResult Current => {{this.GetCurrentExpression}};
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<TResult> span) {
                span = default;
                return false;
            }
            public bool TryCopyTo(scoped global::System.Span<TResult> destination, global::System.Index offset) => false;
            public bool TryGetNext(out TResult current) {
                {{this.GetTryGetNextBody}}
            }
            public void Dispose() {
            }
        }
        """;
    public override string GetEnumeratorFields => $$"""
        private readonly TResult {{this.result}};
        private bool {{this.yielded}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(TResult result) {
            {{this.result}} = result;
            {{this.yielded}} = false;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.yielded}}) {
            current = default!;
            return false;
        }
        {{this.yielded}} = true;
        current = {{this.result}};
        return true;
        """;
    public override string GetCurrentExpression => this.result;

    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        {{second.GetBeginGlobal}}
        """;

    public override string GetBeginLoopOutside => $$"""
        {{GeneratedMethodFragments.Execute(source,this.MaterializedSource)}}
        {{GeneratedMethodFragments.Execute(second,this.MaterializedSecond)}}
        var {{this.result}}={{this.Invocation}};
        """;

    public override string GetBeginLoop => string.Empty;
    public override string GetBeginLoopInside => string.Empty;
    public override string GetEndLoopInside => string.Empty;
    public override string GetEndLoop => string.Empty;
    public override string GetEndLoopOutside => string.Empty;
    public override string GetEndGlobal => string.Empty;
    public override string Result => this.result;
}

public class Append(int 番号, Enumerable source, string element) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Append({this.MaterializedSource}, {element})";
}

public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : GeneratedSequenceFragment(番号, source, resultElementType) {
    public override string StructMethod => $$"""
        {{this.DefineStruct(keySelector, nameof(keySelector))}}
        {{this.DefineStruct(func, nameof(func))}}
        """;
    protected override string Invocation => $"global::System.Linq.Enumerable.AggregateBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {seed}, {this.FuncDelegate(func, nameof(func))})";
}

public class AsEnumerable(int 番号, Enumerable source) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.AsEnumerable({this.MaterializedSource})";
}

public class Average(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Average({this.MaterializedSource})";
}

public class Average_selector(int 番号, Enumerable source, FuncFragment selector) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    protected override string Invocation => $"global::System.Linq.Enumerable.Average({this.MaterializedSource}, {this.FuncDelegate(selector, nameof(selector))})";
}

public class Chunk(int 番号, Enumerable source, string size) : GeneratedSequenceFragment(番号, source, $"{source.ElementType}[]") {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Chunk({this.MaterializedSource}, {size})";
}

public class Cast(int 番号, Enumerable source, string resultElementType) : GeneratedSequenceFragment(番号, source, resultElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Cast<{resultElementType}>({this.MaterializedSource})";
}

public class Concat(int 番号, Enumerable source, Enumerable second) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Concat({this.MaterializedSource}, {this.MaterializedSecond})";
}

public class Contains(int 番号, Enumerable source, string value) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Contains({this.MaterializedSource}, {value})";
}

public class Count(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Count({this.MaterializedSource})";
}

public class Count_predicate(int 番号, Enumerable source, FuncFragment predicate) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.Count({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class CountBy(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>") {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.CountBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class DefaultIfEmpty(int 番号, Enumerable source) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.DefaultIfEmpty({this.MaterializedSource})";
}

public class DefaultIfEmpty_defaultValue(int 番号, Enumerable source, string defaultValue) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.DefaultIfEmpty({this.MaterializedSource}, {defaultValue})";
}

public class Distinct(int 番号, Enumerable source) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Distinct({this.MaterializedSource})";
}

public class Distinct_comparer(int 番号, Enumerable source, string comparer) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Distinct({this.MaterializedSource}, {comparer})";
}

public class DistinctBy(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.DistinctBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class DistinctBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.DistinctBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class ElementAt(int 番号, Enumerable source, string index) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.ElementAt({this.MaterializedSource}, {index})";
}

public class ElementAtOrDefault(int 番号, Enumerable source, string index) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.ElementAtOrDefault({this.MaterializedSource}, {index})";
}

public class Except(int 番号, Enumerable source, Enumerable second) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Except({this.MaterializedSource}, {this.MaterializedSecond})";
}

public class Except_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Except({this.MaterializedSource}, {this.MaterializedSecond}, {comparer})";
}

public class ExceptBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.ExceptBy({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class ExceptBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.ExceptBy({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class First(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.First({this.MaterializedSource})";
}

public class First_predicate(int 番号, Enumerable source, FuncFragment predicate) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.First({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class FirstOrDefault(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.FirstOrDefault({this.MaterializedSource})";
}

public class FirstOrDefault_defaultValue(int 番号, Enumerable source, string defaultValue) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.FirstOrDefault({this.MaterializedSource}, {defaultValue})";
}

public class FirstOrDefault_predicate(int 番号, Enumerable source, FuncFragment predicate) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.FirstOrDefault({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class GroupBy(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, $"global::System.Linq.IGrouping<{FuncReturnTypeName(keySelector)}, {source.ElementType}>") {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.GroupBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class Intersect(int 番号, Enumerable source, Enumerable second) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Intersect({this.MaterializedSource}, {this.MaterializedSecond})";
}

public class Intersect_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Intersect({this.MaterializedSource}, {this.MaterializedSecond}, {comparer})";
}

public class IntersectBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.IntersectBy({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class IntersectBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.IntersectBy({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class Last(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Last({this.MaterializedSource})";
}

public class Last_predicate(int 番号, Enumerable source, FuncFragment predicate) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.Last({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class LastOrDefault(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.LastOrDefault({this.MaterializedSource})";
}

public class LongCount(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.LongCount({this.MaterializedSource})";
}

public class LongCount_predicate(int 番号, Enumerable source, FuncFragment predicate) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.LongCount({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class Max(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Max({this.MaterializedSource})";
}

public class Max_selector(int 番号, Enumerable source, FuncFragment selector) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    protected override string Invocation => $"global::System.Linq.Enumerable.Max({this.MaterializedSource}, {this.FuncDelegate(selector, nameof(selector))})";
}

public class Min(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Min({this.MaterializedSource})";
}

public class Min_selector(int 番号, Enumerable source, FuncFragment selector) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    protected override string Invocation => $"global::System.Linq.Enumerable.Min({this.MaterializedSource}, {this.FuncDelegate(selector, nameof(selector))})";
}

public class OrderBy(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.OrderBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class OrderBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.OrderBy({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class OrderByDescending(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.OrderByDescending({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class OrderByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.OrderByDescending({this.MaterializedSource}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class Prepend(int 番号, Enumerable source, string element) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Prepend({this.MaterializedSource}, {element})";
}

public class Reverse(int 番号, Enumerable source) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Reverse({this.MaterializedSource})";
}

public class Select_index(int 番号, Enumerable source, FuncFragment selector) : Enumerable(番号) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{source.ElementType}}, int, {{this.ElementType}}> {{nameof(selector)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{this.ElementType}} {{this.currentValue}};
        private int {{this.index}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{source.ElementType}}, int, {{this.ElementType}}> selector) {
            {{this.e}} = source.GetEnumerator();
            this.{{nameof(selector)}} = selector;
            {{this.currentValue}} = default!;
            {{this.index}} = -1;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.index}}++;
            {{this.currentValue}} = {{nameof(selector)}}({{this.item}}, {{this.index}});
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        {{var_List(this.result,this.ElementType)}}
        """;
    public override string GetBeginLoopOutside => $$"""
        {{source.GetBeginLoopOutside}}
        {{this.result}}.Clear();
        var {{this.index}}=-1;
        """;
    public override string GetBeginLoop => source.GetBeginLoop;
    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        {{this.index}}++;
        """;
    public override string GetEndLoopInside => source.GetEndLoopInside;
    public override string GetEndLoop => source.GetEndLoop;
    public override string GetEndLoopOutside => source.GetEndLoopOutside;
    public override string GetEndGlobal => source.GetEndGlobal;
    public override string GetAdd => $$"""
        {{this.result}}.Add({{this.GetElement}});
        """;
    public override string GetElement => $$"""
        {{this.FuncInvoke(selector,nameof(selector),source.GetElement,this.index)}}
        """;
    public override string MoveNext => source.MoveNext;
    public override string ElementType => FuncReturnTypeName(selector);
    public override string Result => this.result;
}

public class SelectMany(int 番号, Enumerable source, FuncFragment selector) : GeneratedSequenceFragment(番号, source, SequenceElementTypeName(FuncReturnTypeName(selector))) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    protected override string Invocation => $"global::System.Linq.Enumerable.SelectMany({this.MaterializedSource}, {this.FuncDelegate(selector, nameof(selector))})";
}

public class SequenceEqual(int 番号, Enumerable source, Enumerable second) : GeneratedBinaryScalarFragment(番号, source, second) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.SequenceEqual({this.MaterializedSource}, {this.MaterializedSecond})";
}

public class SequenceEqual_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : GeneratedBinaryScalarFragment(番号, source, second) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.SequenceEqual({this.MaterializedSource}, {this.MaterializedSecond}, {comparer})";
}

public class Skip(int 番号, Enumerable source, string count) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Skip({this.MaterializedSource}, {count})";
}

public class SkipWhile(int 番号, Enumerable source, FuncFragment predicate) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.SkipWhile({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class SkipWhile_index(int 番号, Enumerable source, FuncFragment predicate) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.SkipWhile({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class Sum(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Sum({this.MaterializedSource})";
}

public class Sum_selector(int 番号, Enumerable source, FuncFragment selector) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    protected override string Invocation => $"global::System.Linq.Enumerable.Sum({this.MaterializedSource}, {this.FuncDelegate(selector, nameof(selector))})";
}

public class Take(int 番号, Enumerable source, string count) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Take({this.MaterializedSource}, {count})";
}

public class TakeWhile(int 番号, Enumerable source, FuncFragment predicate) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.TakeWhile({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class TakeWhile_index(int 番号, Enumerable source, FuncFragment predicate) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    protected override string Invocation => $"global::System.Linq.Enumerable.TakeWhile({this.MaterializedSource}, {this.FuncDelegate(predicate, nameof(predicate))})";
}

public class ThenBy(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.ThenBy((global::System.Linq.IOrderedEnumerable<{Source.ElementType}>)({this.MaterializedSource}), {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class ThenBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.ThenBy((global::System.Linq.IOrderedEnumerable<{Source.ElementType}>)({this.MaterializedSource}), {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class ThenByDescending(int 番号, Enumerable source, FuncFragment keySelector) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.ThenByDescending((global::System.Linq.IOrderedEnumerable<{Source.ElementType}>)({this.MaterializedSource}), {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class ThenByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : GeneratedSequenceFragment(番号, source, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.ThenByDescending((global::System.Linq.IOrderedEnumerable<{Source.ElementType}>)({this.MaterializedSource}), {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class ToArray(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.ToArray({this.MaterializedSource})";
}

public class ToList(int 番号, Enumerable source) : GeneratedScalarFragment(番号, source) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.ToList({this.MaterializedSource})";
}

public class Union(int 番号, Enumerable source, Enumerable second) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Union({this.MaterializedSource}, {this.MaterializedSecond})";
}

public class Union_comparer(int 番号, Enumerable source, Enumerable second, string comparer) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Union({this.MaterializedSource}, {this.MaterializedSecond}, {comparer})";
}

public class UnionBy(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.UnionBy({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(keySelector, nameof(keySelector))})";
}

public class UnionBy_comparer(int 番号, Enumerable source, Enumerable second, FuncFragment keySelector, string comparer) : GeneratedBinarySequenceFragment(番号, source, second, source.ElementType) {
    public override string StructMethod => this.DefineStruct(keySelector, nameof(keySelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.UnionBy({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(keySelector, nameof(keySelector))}, {comparer})";
}

public class Zip(int 番号, Enumerable source, Enumerable second) : GeneratedBinarySequenceFragment(番号, source, second, $"({source.ElementType} First, {second.ElementType} Second)") {
    public override string StructMethod => string.Empty;
    protected override string Invocation => $"global::System.Linq.Enumerable.Zip({this.MaterializedSource}, {this.MaterializedSecond})";
}

public class Zip_resultSelector(int 番号, Enumerable source, Enumerable second, FuncFragment resultSelector) : GeneratedBinarySequenceFragment(番号, source, second, FuncReturnTypeName(resultSelector)) {
    public override string StructMethod => this.DefineStruct(resultSelector, nameof(resultSelector));
    protected override string Invocation => $"global::System.Linq.Enumerable.Zip({this.MaterializedSource}, {this.MaterializedSecond}, {this.FuncDelegate(resultSelector, nameof(resultSelector))})";
}
