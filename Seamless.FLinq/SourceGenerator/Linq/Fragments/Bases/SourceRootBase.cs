namespace Seamless.FLinq.Linq.Fragments;

[Flags]
public enum SourceCapability {
    None = 0,
    KnownCount = 1,
    DirectSpan = 1<<1,
    Indexable = 1<<2,
    ChunkStreaming = 1<<3,
    NativeIterate = 1<<4,
    Unordered = 1<<5,
}

public abstract class SourceRootBase(int number,string sourceExpression):Enumerable(number) {
    public string SourceExpression { get; } = sourceExpression;
    public abstract SourceCapability Capabilities { get; }
    public bool HasCapability(SourceCapability capability) => (this.Capabilities&capability)==capability;
}

public abstract class EnumeratorRootBase(
    int number,
    string sourceExpression):SourceRootBase(number,sourceExpression);

public abstract class DirectSpanRootBase(
    int number,
    string sourceExpression,
    string elementType):SourceRootBase(number,sourceExpression) {
    protected abstract string SourceTypeName { get; }
    protected abstract string CountExpression(string sourceName);
    protected abstract string RemainingCountExpression(string sourceName,string indexName);
    protected abstract string SpanExpression(string sourceName,string startExpression);
    protected virtual string IndexTypeName => "int";
    protected virtual string ElementExpression(string sourceName,string indexName) => $"{sourceName}[{indexName}]";

    public override SourceCapability Capabilities =>
        SourceCapability.KnownCount|SourceCapability.DirectSpan|SourceCapability.Indexable|SourceCapability.ChunkStreaming;
    public override bool CanProvideSpan => true;
    public override string? GetDirectSpanExpression(string enumeratorName) => $"{enumeratorName}.GetSpan()";
    public override string? NonEnumeratedCountExpression => this.CountExpression(this.SourceExpression);
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.SourceExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"{this.SourceTypeName} source";
    public override string GetEnumeratorFields => $"private {this.IndexTypeName} {this.e}=-1;";
    public override string GetTryGetNextBody => $$"""
        if(++{{this.e}}<{{this.CountExpression("source")}}){
            current={{this.ElementExpression("source",this.e)}};
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => "Current";
    public override string ElementType => elementType;
    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}}{{this.StructTypeParameters}}:global::Seamless.Linq.IValueEnumerator<{{elementType}}>{
            private {{this.SourceTypeName}} source;
            private {{this.IndexTypeName}} {{this.e}}=-1;
            public {{this.Struct}}({{this.SourceTypeName}} source){this.source=source;}
            public {{elementType}} Current{get;private set;}=default!;
            {{AggressiveInliningAttribute}}
            public bool TryGetNonEnumeratedCount(out int count){
                count={{this.RemainingCountExpression("source",this.e)}};
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetSpan(out global::System.ReadOnlySpan<{{elementType}}> span){
                span=this.GetSpan();
                return true;
            }
            {{AggressiveInliningAttribute}}
            public global::System.ReadOnlySpan<{{elementType}}> GetSpan()
                =>{{this.SpanExpression("source",$"{this.e}+1")}};
            {{AggressiveInliningAttribute}}
            public bool TryCopyTo(scoped global::System.Span<{{elementType}}> destination,global::System.Index offset){
                var remaining=this.GetSpan();
                var start=offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            {{AggressiveInliningAttribute}}
            public bool TryGetNext(out {{elementType}} current){
                if(++{{this.e}}<{{this.CountExpression("source")}}){
                    current=this.Current={{this.ElementExpression("source",this.e)}};
                    return true;
                }
                current=default!;
                return false;
            }
            {{AggressiveInliningAttribute}}
            public void Dispose(){}
        }
        """;
}

public abstract class ProviderNativeRootBase(
    int number,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName,
    bool useEnumerableInterface):Root(number,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface) {
    public override SourceCapability Capabilities =>
        SourceCapability.KnownCount|SourceCapability.ChunkStreaming|SourceCapability.NativeIterate|SourceCapability.Unordered;
    public virtual string NativeCountExpression => $"{this.SourceExpression}.Count";
    public override string? NonEnumeratedCountExpression => this.NativeCountExpression;
    public abstract string ProviderName { get; }
    public abstract string SourceVariablePrefix { get; }
    public abstract string StrategyInterfaceTypeName { get; }
    public virtual string NativeOperationName => "Iterate";
    public virtual string NativeExecutionMethodName => "Iterate";
    // provider が O(1) の membership lookup を持つ場合に、その式を返す。
    // 既定 null は「O(1) lookup 無し」で、呼び出し側は従来の native scan へ落ちる。
    // Persisted の Set は Phase2-2 の .hindex 索引で Contains が O(1) になっており、
    // ランタイム側（Internal/ProviderEnumerators.cs の TryContains）は既にこれを使っている。
    public virtual string? NativeContainsExpression(string valueExpression) => null;
}
