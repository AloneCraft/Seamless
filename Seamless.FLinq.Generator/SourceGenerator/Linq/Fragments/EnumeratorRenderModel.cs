namespace Seamless.FLinq;

// Enumeratorの意味論を持つFragmentと、C#メンバー文字列の最終合成を分離する。
// 各入力文字列は従来と同じ順序・インデントで組み立てられる。
internal sealed class EnumeratorRenderModel(
    string structDeclaration,
    string fieldsFromParameters,
    string fields,
    string constructor,
    string currentPropertyDeclaration,
    string nonEnumeratedCount,
    string getSpan,
    string copyTo,
    string tryGetNextBody,
    string disposeBody,
    string currentType) {
    private const string AggressiveInliningAttribute = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining|global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]";

    internal string Render() => $$"""
        {{structDeclaration}}
        {{fieldsFromParameters}}
        {{fields}}
        {{constructor}}
            {{currentPropertyDeclaration}}
        {{nonEnumeratedCount}}
        {{getSpan}}
        {{copyTo}}
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{currentType}} current){
        {{tryGetNextBody}}
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){
        {{disposeBody}}
            }
        }
        """;
}