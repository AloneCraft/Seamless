internal static class ComparerExpressionSupport {
    public static bool IsDefaultEqualityComparer(string comparer,string typeName) =>
        IsNull(comparer)
        || IsDefaultComparerExpression(comparer,typeName,"EqualityComparer");

    public static bool IsDefaultOrderComparer(string comparer,string typeName) =>
        IsNull(comparer)
        || IsDefaultComparerExpression(comparer,typeName,"Comparer");

    public static string RenderEqualityComparer(string comparer,string typeName) =>
        IsDefaultComparerExpression(comparer,typeName,"EqualityComparer")
            ? $"global::System.Collections.Generic.EqualityComparer<{typeName}>.Default"
            : comparer;

    public static string RenderOrderComparer(string comparer,string typeName) =>
        IsDefaultComparerExpression(comparer,typeName,"Comparer")
            ? $"global::System.Collections.Generic.Comparer<{typeName}>.Default"
            : comparer;

    private static bool IsNull(string comparer) =>
        string.Equals(comparer,"null",System.StringComparison.Ordinal);

    private static bool IsDefaultComparerExpression(string comparer,string typeName,string comparerTypeName) {
        var normalized=Normalize(comparer);
        foreach(var candidateType in CandidateTypeNames(typeName)) {
            if(string.Equals(normalized,$"{comparerTypeName}<{candidateType}>.Default",System.StringComparison.Ordinal)
                || string.Equals(normalized,$"System.Collections.Generic.{comparerTypeName}<{candidateType}>.Default",System.StringComparison.Ordinal)
                || string.Equals(normalized,$"global::System.Collections.Generic.{comparerTypeName}<{candidateType}>.Default",System.StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    private static string Normalize(string expression) =>
        expression.Replace(" ","").Replace("\t","").Replace("\r","").Replace("\n","");

    private static System.Collections.Generic.IEnumerable<string> CandidateTypeNames(string typeName) {
        yield return typeName;
        if(IsInt32Type(typeName)) {
            yield return "int";
            yield return "System.Int32";
            yield return "global::System.Int32";
        }
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",System.StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",System.StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",System.StringComparison.Ordinal);
}
