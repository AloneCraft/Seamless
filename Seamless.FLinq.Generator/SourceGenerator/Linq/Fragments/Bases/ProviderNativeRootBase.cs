namespace Seamless.FLinq.Generator;

// Persisted / Tsavorite の provider を根にする Root の共通基底。
// PersistedSetRoot / TsavoriteHashSetRoot / TsavoriteSetRoot / ProviderIterateTerminal の
// 4 ファイルから使われる、実際に共有された基底なので Bases に置く。
// Root 系のうちここ以外の基底（SourceRootBase / EnumeratorRootBase / DirectSpanRootBase）は
// 派生が Root.cs 内だけなので Root.cs へ集約してある。
internal abstract class ProviderNativeRootBase(
    int number,
    string sourceExpression,
    string elementType,
    string enumeratorTypeName,
    bool useEnumerableInterface):Root(number,sourceExpression,elementType,enumeratorTypeName,useEnumerableInterface) {
    public override SourceCapability Capabilities {
        get {
            var capabilities = SourceCapability.ChunkStreaming|SourceCapability.NativeIterate|SourceCapability.Unordered;
            if(this.NativeCountExpression is not null) {
                capabilities|=SourceCapability.KnownCount;
            }
            return capabilities;
        }
    }
    // O(1) で件数を返す式。null は「件数を持たない」(Tsavorite のマルチ用など)で、件数を前提にした
    // 先取り・TryGetNonEnumeratedCount の即答をせず、走査しながら集める経路へ落ちる。
    public virtual string? NativeCountExpression => $"{this.SourceExpression}.Count";
    // 要素を取り出さずに全件を数えるメソッドの名前(引数なし・long を返す)。件数を持たない根だけが持つ。
    public virtual string? ScanCountMethodName => null;
    public override string? NonEnumeratedCountExpression => this.NativeCountExpression;
    public abstract string ProviderName { get; }
    public abstract string SourceVariablePrefix { get; }
    public abstract string StrategyInterfaceTypeName { get; }
    public virtual string NativeOperationName => "Iterate";
    public virtual string NativeExecutionMethodName => "Iterate";
    // provider が O(1) の membership lookup を持つ場合に、その式を返す。
    // 既定 null は「O(1) lookup 無し」で、呼び出し側は従来の native scan へ落ちる。
    // Persisted の Set は Phase2-2 の .hindex 索引で Contains が O(1) になっている。
    // ランタイム側は Seamless.Linq/Linq/Contains.cs の provider 特殊化オーバーロードが同じ経路を使う。
    public virtual string? NativeContainsExpression(string valueExpression) => null;
}
