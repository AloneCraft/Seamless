namespace Seamless.FLinq;

internal delegate bool PipelineEnumerableBuilder(ExpressionSyntax expression,ref int uniqueNumber,out Enumerable fragment,out string staticMethods);
internal delegate bool PipelineFuncBuilder(ExpressionSyntax expression,ITypeSymbol delegateType,ref int uniqueNumber,out FuncFragment func);
internal delegate bool PipelineElementTypeGetter(ITypeSymbol type,out ITypeSymbol elementType);

internal sealed class PipelineBuildServices(
    SymbolDisplayFormat fullyQualifiedFormat,
    PipelineEnumerableBuilder buildEnumerable,
    PipelineFuncBuilder buildFunc,
    Func<FuncFragment,bool> canUseFunc,
    Func<ExpressionSyntax,string> renderExpression,
    PipelineElementTypeGetter tryGetEnumerableElementType,
    Func<ITypeSymbol,bool> containsAnonymousType,
    Func<string,string,string> joinStaticMethods,
    string[] outerTypeParameters) {
    public SymbolDisplayFormat FullyQualifiedFormat { get; } = fullyQualifiedFormat;
    public PipelineEnumerableBuilder BuildEnumerable { get; } = buildEnumerable;
    public PipelineFuncBuilder BuildFunc { get; } = buildFunc;
    public Func<FuncFragment,bool> CanUseFunc { get; } = canUseFunc;
    public Func<ExpressionSyntax,string> RenderExpression { get; } = renderExpression;
    public PipelineElementTypeGetter TryGetEnumerableElementType { get; } = tryGetEnumerableElementType;
    public Func<ITypeSymbol,bool> ContainsAnonymousType { get; } = containsAnonymousType;
    public Func<string,string,string> JoinStaticMethods { get; } = joinStaticMethods;
    public string[] OuterTypeParameters { get; } = outerTypeParameters;
}
