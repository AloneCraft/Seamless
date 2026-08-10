namespace Seamless.FLinq;

// Keeps enumerator struct field and constructor composition outside Fragment.
// Fragment still owns semantic rendering for TryGetNext, probe members, and Dispose.
internal sealed class EnumeratorDefinitionRenderModel(
    string structDeclaration,
    string structName,
    string constructorParameters,
    GeneratedCodeBlock constructorExtraBody,
    GeneratedCodeBlock fields,
    GeneratedCodeBlock currentPropertyDeclaration,
    GeneratedCodeBlock nonEnumeratedCount,
    GeneratedCodeBlock getSpan,
    GeneratedCodeBlock copyTo,
    GeneratedCodeBlock tryGetNextBody,
    GeneratedCodeBlock disposeBody,
    string currentType
) {
    internal static string RenderDeclaration(string structName,string typeParameters,string interfaceType,string typeConstraints) {
        var constraints=string.IsNullOrEmpty(typeConstraints) ? string.Empty : $"\n    {typeConstraints}";
        return $"private struct {structName}{typeParameters} : {interfaceType}{constraints} {{";
    }

    internal string Render() {
        var ctorParams=ParseCtorParams(constructorParameters).ToArray();
        var ctorParamFields=ctorParams.Length>0
            ? string.Join("\n",ctorParams.Select(static p => $"private {p.TypeExpr} {p.Name};"))
            : string.Empty;
        var ctorDecl=RenderConstructor(ctorParams);

        return new EnumeratorRenderModel(
            structDeclaration,
            GeneratedTextFormatting.IndentLines(ctorParamFields,"    "),
            GeneratedTextFormatting.IndentLines(fields.Render(),"    "),
            GeneratedTextFormatting.IndentLines(ctorDecl,"    "),
            currentPropertyDeclaration.Render(),
            nonEnumeratedCount.Render(),
            getSpan.Render(),
            copyTo.Render(),
            tryGetNextBody.Render(),
            disposeBody.Render(),
            currentType).Render();
    }

    private string RenderConstructor((string TypeExpr,string Name)[] ctorParams) {
        if(ctorParams.Length==0) {
            return string.Empty;
        }

        var assignments=string.Join("\n    ",ctorParams.Select(static p => $"this.{p.Name} = {p.Name};"));
        var extraBody=constructorExtraBody.Render();
        var ctorBody=string.IsNullOrWhiteSpace(extraBody)
            ? "    "+assignments
            : "    "+assignments+"\n    "+GeneratedTextFormatting.IndentLines(extraBody.Trim(),"    ");
        return $"public {structName}({constructorParameters}) {{\n{ctorBody}\n}}";
    }

    private static IEnumerable<(string TypeExpr,string Name)> ParseCtorParams(string paramList) {
        if(string.IsNullOrWhiteSpace(paramList)) yield break;
        int depth=0,start=0;
        for(var i=0;i<=paramList.Length;i++) {
            var c=i<paramList.Length ? paramList[i] : ',';
            if(c=='<'||c=='(') depth++;
            else if(c=='>'||c==')') depth--;
            else if(c==','&&depth==0) {
                var segment=paramList.Substring(start,i-start).Trim();
                var separator=segment.LastIndexOf(' ');
                if(separator>=0) yield return (segment.Substring(0,separator).Trim(),segment.Substring(separator+1).Trim());
                start=i+1;
            }
        }
    }
}
