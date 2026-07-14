namespace Seamless.FLinq.Linq.Fragments;

public interface ISetOperationStrategy {
    bool PreloadSecondFirst { get; }
    string SourceAcceptMethod { get; }
    string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer);
}

public interface ISetByOperationStrategy {
    bool PreloadSecondFirst { get; }
    string SourceAcceptMethod { get; }
    string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer);
}

public static class SetOperationStrategies {
    public static ISetOperationStrategy Union { get; } = new UnionStrategy();
    public static ISetOperationStrategy Except { get; } = new ExceptStrategy();
    public static ISetOperationStrategy Intersect { get; } = new IntersectStrategy();
    public static ISetByOperationStrategy ByUnion { get; } = new ByUnionStrategy();
    public static ISetByOperationStrategy ByExcept { get; } = new ByExceptStrategy();
    public static ISetByOperationStrategy ByIntersect { get; } = new ByIntersectStrategy();

    private sealed class UnionStrategy:ISetOperationStrategy {
        public bool PreloadSecondFirst => false;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer) =>
            $"global::Seamless.System.Carrier.ValueEnumerableExtensions.Union({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})";
    }
    private sealed class ExceptStrategy:ISetOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer) =>
            $"global::Seamless.System.Carrier.ValueEnumerableExtensions.Except({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})";
    }
    private sealed class IntersectStrategy:ISetOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Remove";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer) =>
            $"global::Seamless.System.Carrier.ValueEnumerableExtensions.Intersect({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})";
    }
    private sealed class ByUnionStrategy:ISetByOperationStrategy {
        public bool PreloadSecondFirst => false;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer) =>
            $"global::Seamless.System.Carrier.ValueEnumerableExtensions.UnionBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    }
    private sealed class ByExceptStrategy:ISetByOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer) =>
            $"global::Seamless.System.Carrier.ValueEnumerableExtensions.ExceptBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    }
    private sealed class ByIntersectStrategy:ISetByOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Remove";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer) =>
            $"global::Seamless.System.Carrier.ValueEnumerableExtensions.IntersectBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    }
}
