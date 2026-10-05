namespace Seamless.Collections.Tsavorite;
/// <summary>Tsavorite の索引とログの観測値。取得した時点の値であり、並行して書き込んでいる間は項目どうしが整合しない。</summary>
/// <param name="IndexSize">索引のバケット数。1 バケットは 64 バイト。</param>
/// <param name="IndexBytes">索引のバイト数。溢れバケットを含まない。</param>
/// <param name="EntryCount">索引の使用中の枠の数。ハッシュ衝突があるため、コレクションの件数ではない。</param>
/// <param name="OverflowBucketCount">初期予約を含む溢れバケットの数。</param>
/// <param name="OverflowBucketAllocations">初期予約を除く溢れバケットの割当数。</param>
/// <param name="BeginAddress">有効なログの先頭アドレス。</param>
/// <param name="HeadAddress">メモリ上のログの先頭アドレス。</param>
/// <param name="ReadOnlyAddress">読み取り専用領域と変更可能領域の境界アドレス。</param>
/// <param name="TailAddress">ログの末尾アドレス。</param>
/// <param name="MemoryLogBytes">メモリ上のログのバイト数。TailAddress から HeadAddress を引いた値。</param>
/// <param name="DiskLogBytes">ディスク上のログのバイト数。HeadAddress から BeginAddress を引いた値。</param>
/// <param name="HighWaterAllocatedPageCount">メモリ上のログに確保したページ数の最大値。</param>
/// <param name="Count">データ本体の件数。分かっていないとき(並列区間の中など)は null。数え直すための全走査はしない。</param>
/// <param name="LogBytes">先頭から末尾までのログの大きさ。上書きや削除で無駄が増え、詰め直しで小さくなる。無駄の量は <see cref="CollectionStore{TKey,TValue,TStoreFunctions,TAllocator}.MeasureLogUsage"/>。</param>
public sealed record TsavoriteStatistics(
    long IndexSize,
    long IndexBytes,
    long EntryCount,
    long OverflowBucketCount,
    long OverflowBucketAllocations,
    long BeginAddress,
    long HeadAddress,
    long ReadOnlyAddress,
    long TailAddress,
    long MemoryLogBytes,
    long DiskLogBytes,
    long HighWaterAllocatedPageCount,
    long? Count,
    long LogBytes);

/// <summary>ログの使われ方。全走査して、生きているレコードの大きさと、上書き・削除・詰め直しの残りで無駄になっている大きさを測った値。</summary>
/// <param name="LiveRecords">削除の印を除いた、生きているレコードの数(コレクションの件数と同じ)。</param>
/// <param name="LiveBytes">生きているレコードが使っている大きさ。</param>
/// <param name="LogBytes">先頭から末尾までのログの大きさ。</param>
public readonly record struct TsavoriteLogUsage(long LiveRecords,long LiveBytes,long LogBytes) {
    /// <summary>無駄になっている大きさ。詰め直し(<see cref="CollectionStore{TKey,TValue,TStoreFunctions,TAllocator}.Compact"/>)で空きへ戻せる。</summary>
    public long WasteBytes => Math.Max(0,this.LogBytes-this.LiveBytes);
    /// <summary>ログに占める無駄の割合(0〜1)。ログが空なら 0。</summary>
    public double WasteFraction {
        get {
            if(this.LogBytes<=0) {
                return 0;
            }
            return (double)this.WasteBytes/this.LogBytes;
        }
    }
}
