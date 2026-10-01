namespace Sample.NetShop.Core;

/// <summary>
/// DB・ストア・クエリが使ってよいメモリの大きさ。
/// どれも上限を超えた分はディスクへ退避するので、小さくしても結果は変わらず遅くなるだけ。
/// </summary>
public sealed record 資源設定{
    /// <summary>DB の各表の索引の大きさ(バイト、2の累乗)。</summary>
    public required long DB索引バイト数{get;init;}
    /// <summary>DB の各表がメモリに置くログの大きさ(バイト、2の累乗)。超えた分はディスクへ。</summary>
    public required long DBメモリバイト数{get;init;}
    /// <summary>DB 全体の管理対象メモリの上限。表のメモリ・取引の変更集合もここから予約する。</summary>
    public required long DB管理メモリ上限バイト数{get;init;}
    /// <summary>取引の変更集合をメモリに置く上限。超えると一時ストアへ退避する。</summary>
    public required long DB取引メモリ上限バイト数{get;init;}
    /// <summary>共通ストアのメモリ上限。</summary>
    public required long ストアメモリ上限バイト数{get;init;}
    /// <summary>scope.Query の管理対象メモリ上限。並べ替えや GroupBy はこれを超えるとディスクへ退避する。</summary>
    public required long クエリメモリ上限バイト数{get;init;}
    /// <summary>ストアとクエリが使ってよいディスクの上限。</summary>
    public required long ディスク上限バイト数{get;init;}

    /// <summary>メモリを最小限にしてディスク中心で動かす設定。テストと --low-memory で使う。</summary>
    public static 資源設定 最小{get;}=new(){
        DB索引バイト数=1L<<12,
        DBメモリバイト数=1L<<16,
        DB管理メモリ上限バイト数=16L<<20,
        DB取引メモリ上限バイト数=1L<<20,
        ストアメモリ上限バイト数=8L<<20,
        クエリメモリ上限バイト数=1L<<20,
        ディスク上限バイト数=4L<<30,
    };

    /// <summary>通常の設定。クエリのメモリ上限は、退避が起きる様子を見せるため小さめにしている。</summary>
    public static 資源設定 標準{get;}=new(){
        DB索引バイト数=1L<<16,
        DBメモリバイト数=1L<<22,
        DB管理メモリ上限バイト数=64L<<20,
        DB取引メモリ上限バイト数=8L<<20,
        ストアメモリ上限バイト数=64L<<20,
        クエリメモリ上限バイト数=8L<<20,
        ディスク上限バイト数=16L<<30,
    };
}
