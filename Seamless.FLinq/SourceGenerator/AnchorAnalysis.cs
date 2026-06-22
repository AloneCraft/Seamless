using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.FLinq;

internal static class AnchorAnalysis {
    private const string AnchorPropertyName = "Anchor";
    private const string AnchorExtensionsTypeName = "Seamless.Tsavorite.Collections.Linq.AnchorExtensions";

    public static bool ContainsAnchor(ExpressionSyntax expression,SemanticModel? semanticModel) =>
        expression.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(memberAccess => IsAnchorAccess(memberAccess,semanticModel));

    public static bool IsAnchorAccess(MemberAccessExpressionSyntax memberAccess,SemanticModel? semanticModel) {
        if(memberAccess.Name.Identifier.ValueText!=AnchorPropertyName) {
            return false;
        }
        if(semanticModel is null) {
            return true;
        }
        var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
        if(symbol is null) {
            return true;
        }
        return symbol switch {
            IPropertySymbol property => IsAnchorProperty(property) || property.Name==AnchorPropertyName,
            IMethodSymbol method => IsAnchorMethod(method) || method.Name==AnchorPropertyName,
            _ => true
        };
    }

    public static bool LooksLikeAnchorText(string expression) =>
        expression.Contains(".Anchor",StringComparison.Ordinal)
        || expression.Contains("Seamless.Tsavorite.Collections.Linq.AnchorExtensions.Anchor",StringComparison.Ordinal)
        || expression.Contains("global::Seamless.Linq.AnchorExtensions.Anchor",StringComparison.Ordinal);

    private static bool IsAnchorProperty(IPropertySymbol property) =>
        property.Name==AnchorPropertyName
        && property.ContainingType?.ToDisplayString()==AnchorExtensionsTypeName;

    private static bool IsAnchorMethod(IMethodSymbol method) =>
        method.Name==AnchorPropertyName
        && method.ContainingType?.ToDisplayString()==AnchorExtensionsTypeName;
}
