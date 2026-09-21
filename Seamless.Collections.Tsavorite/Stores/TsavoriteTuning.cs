namespace Seamless.Collections.Tsavorite;
/// <summary>
/// Tsavorite の詳細設定。利用者には公開せず、テストとベンチマークだけが InternalsVisibleTo 経由で使う。
/// 省略した項目は共通ストアの上限と予想件数から自動で決める。値は 2 の冪であること。
/// </summary>
internal sealed record TsavoriteTuning{
    public static TsavoriteTuning Default{get;}=new();
    /// <summary>索引の大きさ(バイト)。1 バケット 64 バイト。省略時は予想件数から決める。</summary>
    public long? IndexSize{get;init;}
    public long PageSize{get;init;}=1L<<16;
    public long SegmentSize{get;init;}=1L<<30;
    /// <summary>ログのメモリ上の大きさ(バイト)。省略時はメモリ種ならストアのメモリ上限(2 の冪へ切り下げ)、それ以外は上限の半分(最大 64MB)。</summary>
    public long? LogMemorySize{get;init;}
    public double MutableFraction{get;init;}=1.0;
}
