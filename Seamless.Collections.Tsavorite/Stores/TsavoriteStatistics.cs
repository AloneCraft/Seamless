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
    long HighWaterAllocatedPageCount);
