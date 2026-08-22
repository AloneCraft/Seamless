namespace Seamless.FLinq.Generator;

// Renders optional enumerator probe members after Fragment has resolved their semantic body.
// The compact default form and the inlined full form intentionally preserve the existing output shape.
internal static class EnumeratorProbeRenderModel {
    internal static string Render(string renderedBody,string defaultBody,string signature,string compactMember,string aggressiveInliningAttribute) {
        if(Normalize(renderedBody)==Normalize(defaultBody)) {
            return $"    {compactMember}";
        }

        return $$"""
                {{aggressiveInliningAttribute}}
                {{signature}} {
            {{GeneratedTextFormatting.IndentLines(renderedBody,"        ")}}
                }
            """;
    }

    private static string Normalize(string body) =>
        body.Replace("\r\n","\n").Trim();

}