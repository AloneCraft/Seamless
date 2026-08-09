namespace Seamless.FLinq;

internal sealed class InvocationSyntaxReferenceComparer:IEqualityComparer<InvocationExpressionSyntax> {
    public static InvocationSyntaxReferenceComparer Instance { get; } = new();

    public bool Equals(InvocationExpressionSyntax? x,InvocationExpressionSyntax? y) => ReferenceEquals(x,y);
    public int GetHashCode(InvocationExpressionSyntax obj) => RuntimeHelpers.GetHashCode(obj);
}