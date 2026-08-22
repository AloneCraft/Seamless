namespace Seamless.FLinq.Generator;

internal interface ISetOperationStrategy {
    bool PreloadSecondFirst { get; }
    string SourceAcceptMethod { get; }
    string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer);
}

internal interface ISetByOperationStrategy {
    bool PreloadSecondFirst { get; }
    string SourceAcceptMethod { get; }
    string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer);
}

internal static class SetOperationStrategies {
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
            $"global::Seamless.Linq.Enumerable.Union({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})";
    }
    private sealed class ExceptStrategy:ISetOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer) =>
            $"global::Seamless.Linq.Enumerable.Except({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})";
    }
    private sealed class IntersectStrategy:ISetOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Remove";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,string comparer) =>
            $"global::Seamless.Linq.Enumerable.Intersect({source.GetEnumerableExpression},{second.GetEnumerableExpression},{comparer})";
    }
    private sealed class ByUnionStrategy:ISetByOperationStrategy {
        public bool PreloadSecondFirst => false;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer) =>
            $"global::Seamless.Linq.Enumerable.UnionBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    }
    private sealed class ByExceptStrategy:ISetByOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Add";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer) =>
            $"global::Seamless.Linq.Enumerable.ExceptBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    }
    private sealed class ByIntersectStrategy:ISetByOperationStrategy {
        public bool PreloadSecondFirst => true;
        public string SourceAcceptMethod => "Remove";
        public string GetEnumerableExpression(Enumerable source,Enumerable second,FuncFragment keySelector,string comparer) =>
            $"global::Seamless.Linq.Enumerable.IntersectBy({source.GetEnumerableExpression},{second.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    }
}
