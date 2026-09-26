using Seamless.Collections.Tsavorite;

namespace Benchmark.Tsavorite.ParameterSweep;

public sealed class パラメータスイープ実行オプション {
    public パラメータスイープ設定 基準設定 { get; init; } = new(
        Indexサイズ: 131072,
        Pageサイズ: Const.PageSize,
        Segmentサイズ: Const.SegmentSize,
        Memoryサイズ: 64L*1024*1024,
        可変Fraction: 1.0);

    public int 最大スイープ手順数 { get; init; } = 8;
    public int 最大成功Count { get; init; } = 65536;
    public int 検索試行Count { get; init; } = 4096;
    public TimeSpan ケース別タイムアウト { get; init; } = TimeSpan.FromSeconds(10);
    public long ストレージデバイス容量 { get; init; } = 1L<<30;
    public string? 作業ディレクトリ { get; init; }
    public IReadOnlyList<int> 論理CPU一覧 { get; init; } = [];
}

public static class パラメータスイープ計画 {
    private const long 最小Indexサイズ = 64;
    private const long 最小Pageサイズ = 1L<<12;
    private const long 最小Segmentサイズ = 1L<<12;
    private const long 最小Memoryサイズ = 1L<<13;

    public static IReadOnlyList<パラメータスイープケース> 既定ケース一覧を作成(パラメータスイープ実行オプション? オプション = null) {
        var 実オプション = オプション??new();
        オプションを検証(実オプション);
        var ケース一覧 = new List<パラメータスイープケース>();
        foreach(var バックエンド in new[] { パラメータスイープバックエンド.メモリ,パラメータスイープバックエンド.ストレージ }) {
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.ハッシュセット,"Int32",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.ハッシュセット,"String",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.ハッシュセット,"UnmanagedStruct",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.ハッシュセット,"ManagedStruct",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.ハッシュセット,"ManagedClass",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.セット,"非管理セットレコード",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.セット,"管理セットレコード",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"String/String",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"String/Int32",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"Int32/String",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"Int32/Int32",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"UnmanagedStruct/UnmanagedStruct",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"ManagedStruct/ManagedStruct",実オプション);
            コレクションケースを追加(ケース一覧,バックエンド,パラメータスイープコレクション種別.辞書,"ManagedClass/ManagedClass",実オプション);
        }
        return ケース一覧;
    }

    public static IReadOnlyList<パラメータスイープケース> スモークケース一覧を作成(パラメータスイープ実行オプション? オプション = null) {
        var 実オプション = オプション??new パラメータスイープ実行オプション {
            最大スイープ手順数=2,
            最大成功Count=128,
            検索試行Count=32,
            ケース別タイムアウト=TimeSpan.FromSeconds(2),
            ストレージデバイス容量=16L*1024*1024,
        };
        return [
            ..軸ケース一覧を作成(パラメータスイープバックエンド.メモリ,パラメータスイープコレクション種別.ハッシュセット,"Int32",パラメータスイープ軸.Indexサイズ,実オプション),
            ..軸ケース一覧を作成(パラメータスイープバックエンド.メモリ,パラメータスイープコレクション種別.セット,"非管理セットレコード",パラメータスイープ軸.Memoryサイズ,実オプション),
            ..軸ケース一覧を作成(パラメータスイープバックエンド.ストレージ,パラメータスイープコレクション種別.辞書,"Int32/Int32",パラメータスイープ軸.Segmentサイズ,実オプション),
        ];
    }

    private static void コレクションケースを追加(
        List<パラメータスイープケース> ケース一覧,
        パラメータスイープバックエンド バックエンド,
        パラメータスイープコレクション種別 コレクション,
        string ジェネリック名,
        パラメータスイープ実行オプション オプション) {
        foreach(var スイープ軸 in Enum.GetValues<パラメータスイープ軸>()) {
            ケース一覧.AddRange(軸ケース一覧を作成(バックエンド,コレクション,ジェネリック名,スイープ軸,オプション));
        }
    }

    private static IReadOnlyList<パラメータスイープケース> 軸ケース一覧を作成(
        パラメータスイープバックエンド バックエンド,
        パラメータスイープコレクション種別 コレクション,
        string ジェネリック名,
        パラメータスイープ軸 スイープ軸,
        パラメータスイープ実行オプション オプション) {
        var ケース一覧 = new List<パラメータスイープケース>(オプション.最大スイープ手順数);
        for(var 手順 = 0;手順<オプション.最大スイープ手順数;手順++) {
            var 設定 = 設定を作成(スイープ軸,手順,オプション.基準設定);
            var ケース名 = パラメータスイープフォーマッター.ケース名を作成(コレクション,ジェネリック名,バックエンド,スイープ軸,設定);
            ケース一覧.Add(new(ケース名,コレクション,ジェネリック名,バックエンド,スイープ軸,設定));
        }
        return ケース一覧;
    }

    private static パラメータスイープ設定 設定を作成(パラメータスイープ軸 スイープ軸,int 手順,パラメータスイープ設定 基準設定) {
        var 値 = スイープ軸 switch {
            パラメータスイープ軸.Indexサイズ => 最小Indexサイズ<<手順,
            パラメータスイープ軸.Memoryサイズ => 最小Memoryサイズ<<手順,
            パラメータスイープ軸.Pageサイズ => 最小Pageサイズ<<手順,
            パラメータスイープ軸.Segmentサイズ => 最小Segmentサイズ<<手順,
            _ => throw new ArgumentOutOfRangeException(nameof(スイープ軸)),
        };
        var 設定 = スイープ軸 switch {
            パラメータスイープ軸.Indexサイズ => 基準設定 with { Indexサイズ=値 },
            パラメータスイープ軸.Memoryサイズ => 基準設定 with { Memoryサイズ=値 },
            パラメータスイープ軸.Pageサイズ => 基準設定 with {
                Pageサイズ=値,
                Segmentサイズ=Math.Max(基準設定.Segmentサイズ,値),
                Memoryサイズ=Math.Max(基準設定.Memoryサイズ,2*値),
            },
            パラメータスイープ軸.Segmentサイズ => 基準設定 with { Segmentサイズ=値 },
            _ => throw new ArgumentOutOfRangeException(nameof(スイープ軸)),
        };
        return 設定;
    }

    public static void オプションを検証(パラメータスイープ実行オプション オプション) {
        ArgumentNullException.ThrowIfNull(オプション);
        if(オプション.最大スイープ手順数<=0) {
            throw new ArgumentOutOfRangeException(nameof(オプション.最大スイープ手順数));
        }
        if(オプション.最大成功Count<=0) {
            throw new ArgumentOutOfRangeException(nameof(オプション.最大成功Count));
        }
        if(オプション.検索試行Count<=0) {
            throw new ArgumentOutOfRangeException(nameof(オプション.検索試行Count));
        }
        if(オプション.ケース別タイムアウト<=TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(オプション.ケース別タイムアウト));
        }
        if(オプション.論理CPU一覧.Any(static CPU => CPU<0)) {
            throw new ArgumentOutOfRangeException(nameof(オプション.論理CPU一覧));
        }
    }
}
