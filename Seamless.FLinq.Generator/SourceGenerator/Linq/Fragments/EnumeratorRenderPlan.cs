namespace Seamless.FLinq;

/// <summary>
/// Enumerator structの意味的な構成を保持し、既存のEnumeratorRenderModelへ描画を委譲します。
/// 移行期間中もFragmentが返す既存の文字列形式を維持します。
/// </summary>
internal sealed class EnumeratorRenderPlan {
    internal string StructDeclaration { get; }
    internal string StructName { get; }
    internal string ConstructorParameters { get; }
    internal GeneratedCodeBlock ConstructorBody { get; }
    internal GeneratedCodeBlock Fields { get; }
    internal GeneratedCodeBlock CurrentProperty { get; }
    internal GeneratedCodeBlock NonEnumeratedCount { get; }
    internal GeneratedCodeBlock GetSpan { get; }
    internal GeneratedCodeBlock CopyTo { get; }
    internal GeneratedCodeBlock TryGetNext { get; }
    internal GeneratedCodeBlock Dispose { get; }
    internal string CurrentType { get; }

    private EnumeratorRenderPlan(
        string structDeclaration,
        string structName,
        string constructorParameters,
        GeneratedCodeBlock constructorBody,
        GeneratedCodeBlock fields,
        GeneratedCodeBlock currentProperty,
        GeneratedCodeBlock nonEnumeratedCount,
        GeneratedCodeBlock getSpan,
        GeneratedCodeBlock copyTo,
        GeneratedCodeBlock tryGetNext,
        GeneratedCodeBlock dispose,
        string currentType) {
        this.StructDeclaration=structDeclaration;
        this.StructName=structName;
        this.ConstructorParameters=constructorParameters;
        this.ConstructorBody=constructorBody;
        this.Fields=fields;
        this.CurrentProperty=currentProperty;
        this.NonEnumeratedCount=nonEnumeratedCount;
        this.GetSpan=getSpan;
        this.CopyTo=copyTo;
        this.TryGetNext=tryGetNext;
        this.Dispose=dispose;
        this.CurrentType=currentType;
    }

    internal static EnumeratorRenderPlan Create(
        string structDeclaration,
        string structName,
        string constructorParameters,
        string constructorBody,
        string fields,
        string currentProperty,
        string nonEnumeratedCount,
        string getSpan,
        string copyTo,
        string tryGetNext,
        string dispose,
        string currentType) {
        return new EnumeratorRenderPlan(
            structDeclaration,
            structName,
            constructorParameters,
            GeneratedCodeBlock.Leaf(constructorBody),
            GeneratedCodeBlock.Leaf(fields),
            GeneratedCodeBlock.Leaf(currentProperty),
            GeneratedCodeBlock.Leaf(nonEnumeratedCount),
            GeneratedCodeBlock.Leaf(getSpan),
            GeneratedCodeBlock.Leaf(copyTo),
            GeneratedCodeBlock.Leaf(tryGetNext),
            GeneratedCodeBlock.Leaf(dispose),
            currentType);
    }

    internal string Render() {
        return new EnumeratorDefinitionRenderModel(
            this.StructDeclaration,
            this.StructName,
            this.ConstructorParameters,
            this.ConstructorBody,
            this.Fields,
            this.CurrentProperty,
            this.NonEnumeratedCount,
            this.GetSpan,
            this.CopyTo,
            this.TryGetNext,
            this.Dispose,
            this.CurrentType).Render();
    }
}
