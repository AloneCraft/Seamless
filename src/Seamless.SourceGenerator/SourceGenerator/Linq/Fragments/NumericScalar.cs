namespace Seamless.SourceGenerator.Linq.Fragments;

public class Sum(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            dynamic sum = default({{resultType}})!;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) sum += (dynamic)e.Current!;
            return ({{resultType}})sum;
        }
        """;
}

public class Sum_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector, nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => selector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => selector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}> selector";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            dynamic sum = default({{resultType}})!;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                sum += (dynamic){{this.FuncInvoke(selector,nameof(selector),this.item)}}!;
            }
            return ({{resultType}})sum;
        }
        """;
}

public class Average(int 番号, Enumerable source, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            long count = 0;
            double sum = 0;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                sum += global::System.Convert.ToDouble(e.Current);
                count++;
            }
            if(count == 0) throw global::Seamless.Linq.Enumerable.NoElements();
            return ({{resultType}})(object)(sum / count);
        }
        """;
}

public class Average_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : BufferedScalar(番号, resultType) {
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector, nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => selector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => selector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}> selector";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            long count = 0;
            double sum = 0;
            using var e = source.GetEnumerator();
            while(e.MoveNext()) {
                var {{this.item}} = e.Current;
                sum += global::System.Convert.ToDouble({{this.FuncInvoke(selector,nameof(selector),this.item)}});
                count++;
            }
            if(count == 0) throw global::Seamless.Linq.Enumerable.NoElements();
            return ({{resultType}})(object)(sum / count);
        }
        """;
}

public class Max(int 番号, Enumerable source, string resultType) : MinMaxBase(番号, source, resultType, "greater") { }
public class Min(int 番号, Enumerable source, string resultType) : MinMaxBase(番号, source, resultType, "less") { }

public abstract class MinMaxBase(int 番号, Enumerable source, string resultType, string direction) : BufferedScalar(番号, resultType) {
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            using var e = source.GetEnumerator();
            if(!e.MoveNext()) throw global::Seamless.Linq.Enumerable.NoElements();
            {{resultType}} result = ({{resultType}})(object)e.Current!;
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(e.MoveNext()) {
                var value = ({{resultType}})(object)e.Current!;
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
}

public class Max_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : MinMaxSelectorBase(番号, source, selector, resultType, "greater") { }
public class Min_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : MinMaxSelectorBase(番号, source, selector, resultType, "less") { }

public abstract class MinMaxSelectorBase(int 番号, Enumerable source, FuncFragment selector, string resultType, string direction) : BufferedScalar(番号, resultType) {
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector, nameof(selector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => selector.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => selector.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {FuncReturnTypeName(selector)}> selector";
    protected override string ComputeExpression => "Compute(source)";
    protected override string ComputeMembers => $$"""
        private static {{resultType}} Compute(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source) {
            using var e = source.GetEnumerator();
            if(!e.MoveNext()) throw global::Seamless.Linq.Enumerable.NoElements();
            var {{this.item}} = e.Current;
            {{resultType}} result = ({{resultType}})(object){{this.FuncInvoke(selector,nameof(selector),this.item)}}!;
            var comparer = global::System.Collections.Generic.Comparer<{{resultType}}>.Default;
            while(e.MoveNext()) {
                {{this.item}} = e.Current;
                var value = ({{resultType}})(object){{this.FuncInvoke(selector,nameof(selector),this.item)}}!;
                if({{(direction=="greater" ? "comparer.Compare(value, result) > 0" : "comparer.Compare(value, result) < 0")}}) result = value;
            }
            return result;
        }
        """;
}
