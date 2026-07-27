using Seamless.Collections;
using Seamless.Collections.Persisted;
using Seamless.Collections.Tsavorite;
using Seamless.TestKit;

using System.Collections.Concurrent;
using System.Diagnostics;

using ストア型 = Seamless.Collections.Tsavorite.Store;

namespace Benchmark.Tsavorite.ParameterSweep;

public sealed class パラメータスイープ実行器 {
    public async Task<IReadOnlyList<パラメータスイープ結果>> 実行Async(
        IReadOnlyList<パラメータスイープケース> ケース一覧,
        パラメータスイープ実行オプション? オプション = null,
        IProgress<パラメータスイープ結果>? 進捗 = null,
        CancellationToken キャンセルトークン = default) {
        ArgumentNullException.ThrowIfNull(ケース一覧);
        var 実オプション = オプション??new();
        パラメータスイープ計画.オプションを検証(実オプション);
        var 実行ID = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff",System.Globalization.CultureInfo.InvariantCulture);
        var 結果一覧 = new List<パラメータスイープ結果>(ケース一覧.Count);
        foreach(var ケース in ケース一覧) {
            キャンセルトークン.ThrowIfCancellationRequested();
            var 結果 = await this.ケースを実行Async(実行ID,ケース,実オプション,キャンセルトークン);
            結果一覧.Add(結果);
            進捗?.Report(結果);
        }
        return 結果一覧;
    }

    public Task<パラメータスイープ結果> ケースを実行Async(
        パラメータスイープケース ケース,
        パラメータスイープ実行オプション? オプション = null,
        CancellationToken キャンセルトークン = default) {
        ArgumentNullException.ThrowIfNull(ケース);
        var 実オプション = オプション??new();
        パラメータスイープ計画.オプションを検証(実オプション);
        var 実行ID = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff",System.Globalization.CultureInfo.InvariantCulture);
        return this.ケースを実行Async(実行ID,ケース,実オプション,キャンセルトークン);
    }

    private Task<パラメータスイープ結果> ケースを実行Async(
        string 実行ID,
        パラメータスイープケース ケース,
        パラメータスイープ実行オプション オプション,
        CancellationToken キャンセルトークン) => Task.Run(() => this.ケースを実行(実行ID,ケース,オプション,キャンセルトークン),CancellationToken.None);

    private パラメータスイープ結果 ケースを実行(
        string 実行ID,
        パラメータスイープケース ケース,
        パラメータスイープ実行オプション オプション,
        CancellationToken キャンセルトークン) {
        var 計測日時 = DateTimeOffset.UtcNow;
        try {
            パラメータスイープ設定バリデーター.検証(ケース.設定);
            using var ワークロード = パラメータスイープワークロードファクトリー.作成(ケース,オプション);
            return this.ワークロードを実行(実行ID,計測日時,ケース,ワークロード,オプション,キャンセルトークン);
        } catch(Exception 例外) when(例外 is ArgumentException or NotSupportedException) {
            return 失敗結果を作成(実行ID,計測日時,ケース,パラメータスイープ失敗種別.無効な構成,例外);
        } catch(OutOfMemoryException 例外) {
            return 失敗結果を作成(実行ID,計測日時,ケース,メモリ不足を分類(例外),例外);
        } catch(Exception 例外) {
            return 失敗結果を作成(実行ID,計測日時,ケース,パラメータスイープ失敗種別.例外,例外);
        }
    }

    private パラメータスイープ結果 ワークロードを実行(
        string 実行ID,
        DateTimeOffset 計測日時,
        パラメータスイープケース ケース,
        Iパラメータスイープワークロード ワークロード,
        パラメータスイープ実行オプション オプション,
        CancellationToken キャンセルトークン) {
        var CPU番号一覧 = オプション.論理CPU一覧.Count==0 ? [null] : オプション.論理CPU一覧.Select(static CPU => (int?)CPU).ToArray();
        var ワーカー一覧 = CPU番号一覧.Select((CPU,番号) => new パラメータスイープワーカー実行(番号,CPU,ワークロード.ワーカーを作成())).ToArray();
        foreach(var ワーカー in ワーカー一覧) {
            ワーカー.ワーカー数=ワーカー一覧.Length;
        }
        using var 連結キャンセルソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン);
        var 追加開始 = new ManualResetEventSlim(false);
        var 追加完了 = new CountdownEvent(ワーカー一覧.Length);
        var 検索開始 = new ManualResetEventSlim(false);
        var 検索完了 = new CountdownEvent(ワーカー一覧.Length);
        var 準備完了 = new CountdownEvent(ワーカー一覧.Length);
        var 失敗一覧 = new ConcurrentQueue<パラメータスイープワーカー失敗>();
        var ケースストップウォッチ = Stopwatch.StartNew();
        var スレッド一覧 = ワーカー一覧.Select(ワーカー => this.ワーカースレッドを作成(
            ワーカー,
            オプション,
            ケースストップウォッチ,
            連結キャンセルソース,
            準備完了,
            追加開始,
            追加完了,
            検索開始,
            検索完了,
            失敗一覧)).ToArray();
        foreach(var スレッド in スレッド一覧) {
            スレッド.Start();
        }
        準備完了.Wait(CancellationToken.None);
        var 追加ストップウォッチ = Stopwatch.StartNew();
        追加開始.Set();
        追加完了.Wait(CancellationToken.None);
        追加ストップウォッチ.Stop();
        var 検索ストップウォッチ = Stopwatch.StartNew();
        検索開始.Set();
        検索完了.Wait(CancellationToken.None);
        検索ストップウォッチ.Stop();
        foreach(var スレッド in スレッド一覧) {
            スレッド.Join();
        }
        var ワーカー指標一覧 = ワーカー一覧.Select(ワーカー => ワーカー.指標を作成()).ToArray();
        var 先頭失敗 = 失敗一覧.TryPeek(out var 失敗) ? 失敗 : null;
        var 追加Count = ワーカー指標一覧.Sum(static 指標 => 指標.追加操作Count);
        var 失敗種別 = 先頭失敗?.種別??パラメータスイープ失敗種別.完了;
        var 失敗メッセージ = 先頭失敗?.メッセージ??string.Empty;
        var 検索Count = ワーカー指標一覧.Sum(static 指標 => 指標.検索操作Count);
        double? 検索平均 = 検索Count>0&&失敗種別==パラメータスイープ失敗種別.完了
            ? 検索ストップウォッチ.Elapsed.TotalMilliseconds*1_000_000/検索Count
            : null;
        return 結果を作成(
            実行ID,
            計測日時,
            ケース,
            追加Count,
            ワークロード.エントリCount,
            失敗種別,
            失敗メッセージ,
            追加ストップウォッチ.Elapsed.TotalMilliseconds,
            検索平均) with {
            ワーカー指標一覧=ワーカー指標一覧,
            CPUセットキー=string.Join(",",CPU番号一覧.Where(static CPU => CPU is not null).Select(static CPU => CPU!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            並行数=ワーカー一覧.Length,
        };
    }

    private Thread ワーカースレッドを作成(
        パラメータスイープワーカー実行 ワーカー,
        パラメータスイープ実行オプション オプション,
        Stopwatch ケースストップウォッチ,
        CancellationTokenSource キャンセルソース,
        CountdownEvent 準備完了,
        ManualResetEventSlim 追加開始,
        CountdownEvent 追加完了,
        ManualResetEventSlim 検索開始,
        CountdownEvent 検索完了,
        ConcurrentQueue<パラメータスイープワーカー失敗> 失敗一覧) => new(() => {
            try {
                パラメータスイープCPUアフィニティ.現在のスレッドへの適用を試みる(ワーカー.論理CPU);
                準備完了.Signal();
                追加開始.Wait();
                this.追加フェーズを実行(ワーカー,オプション,ケースストップウォッチ,キャンセルソース,失敗一覧);
                追加完了.Signal();
                検索開始.Wait();
                this.検索フェーズを実行(ワーカー,オプション,ケースストップウォッチ,キャンセルソース,失敗一覧);
            } catch(Exception 例外) {
                this.失敗を記録(ワーカー,失敗を分類(例外),例外.Message,キャンセルソース,失敗一覧);
                if(準備完了.CurrentCount>0) {
                    準備完了.Signal();
                }
                if(追加完了.CurrentCount>0) {
                    追加完了.Signal();
                }
            } finally {
                検索完了.Signal();
                ワーカー.Dispose();
            }
        }) {
            IsBackground=true,
            Name="TsavoriteParameterSweep"+(ワーカー.論理CPU?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"Worker"),
        };

    private void 追加フェーズを実行(
        パラメータスイープワーカー実行 ワーカー,
        パラメータスイープ実行オプション オプション,
        Stopwatch ケースストップウォッチ,
        CancellationTokenSource キャンセルソース,
        ConcurrentQueue<パラメータスイープワーカー失敗> 失敗一覧) {
        if(キャンセルソース.IsCancellationRequested) {
            return;
        }
        var ストップウォッチ = Stopwatch.StartNew();
        try {
            var 範囲 = 範囲を作成(オプション.最大成功Count,ワーカー.番号,ワーカー.ワーカー数);
            for(var 番号 = 範囲.開始;番号<範囲.終了;番号++) {
                this.停止時に例外を送出(ケースストップウォッチ,オプション,キャンセルソース.Token);
                ワーカー.ワークロード.追加((int)番号);
                ワーカー.追加操作Count++;
            }
        } catch(Exception 例外) {
            this.失敗を記録(ワーカー,失敗を分類(例外),例外.Message,キャンセルソース,失敗一覧);
        } finally {
            ストップウォッチ.Stop();
            ワーカー.追加経過ミリ秒=ストップウォッチ.Elapsed.TotalMilliseconds;
        }
    }

    private void 検索フェーズを実行(
        パラメータスイープワーカー実行 ワーカー,
        パラメータスイープ実行オプション オプション,
        Stopwatch ケースストップウォッチ,
        CancellationTokenSource キャンセルソース,
        ConcurrentQueue<パラメータスイープワーカー失敗> 失敗一覧) {
        if(キャンセルソース.IsCancellationRequested) {
            return;
        }
        var 試行Count = Math.Min(オプション.最大成功Count,オプション.検索試行Count);
        var ストップウォッチ = Stopwatch.StartNew();
        try {
            var 範囲 = 範囲を作成(試行Count,ワーカー.番号,ワーカー.ワーカー数);
            for(var 試行番号 = 範囲.開始;試行番号<範囲.終了;試行番号++) {
                this.停止時に例外を送出(ケースストップウォッチ,オプション,キャンセルソース.Token);
                var 値インデックス = (int)(試行番号*オプション.最大成功Count/試行Count);
                if(!ワーカー.ワークロード.検索で見つかるか(値インデックス)) {
                    throw new InvalidOperationException("追加済みの値を検索できませんでした。");
                }
                ワーカー.検索操作Count++;
            }
        } catch(Exception 例外) {
            this.失敗を記録(ワーカー,失敗を分類(例外),例外.Message,キャンセルソース,失敗一覧);
        } finally {
            ストップウォッチ.Stop();
            ワーカー.検索経過ミリ秒=ストップウォッチ.Elapsed.TotalMilliseconds;
        }
    }

    private void 停止時に例外を送出(Stopwatch ストップウォッチ,パラメータスイープ実行オプション オプション,CancellationToken キャンセルトークン) {
        キャンセルトークン.ThrowIfCancellationRequested();
        if(ストップウォッチ.Elapsed>=オプション.ケース別タイムアウト) {
            throw new TimeoutException("ケース別タイムアウトに達しました。");
        }
    }

    private void 失敗を記録(
        パラメータスイープワーカー実行 ワーカー,
        パラメータスイープ失敗種別 失敗種別,
        string メッセージ,
        CancellationTokenSource キャンセルソース,
        ConcurrentQueue<パラメータスイープワーカー失敗> 失敗一覧) {
        ワーカー.失敗種別=失敗種別;
        ワーカー.失敗メッセージ=メッセージ;
        失敗一覧.Enqueue(new(失敗種別,メッセージ));
        キャンセルソース.Cancel();
    }

    private static パラメータスイープ範囲 範囲を作成(long 合計,int ワーカーインデックス,int ワーカーCount) {
        var 開始 = 合計*ワーカーインデックス/ワーカーCount;
        var 終了 = 合計*(ワーカーインデックス+1)/ワーカーCount;
        return new(開始,終了);
    }

    private static パラメータスイープ結果 失敗結果を作成(
        string 実行ID,
        DateTimeOffset 計測日時,
        パラメータスイープケース ケース,
        パラメータスイープ失敗種別 失敗種別,
        Exception 例外) =>
        結果を作成(
            実行ID,
            計測日時,
            ケース,
            0,
            0,
            失敗種別,
            例外.GetType().Name+": "+例外.Message,
            0,
            null);

    private static パラメータスイープ結果 結果を作成(
        string 実行ID,
        DateTimeOffset 計測日時,
        パラメータスイープケース ケース,
        long 最大成功Count,
        long エントリCount,
        パラメータスイープ失敗種別 失敗種別,
        string 失敗メッセージ,
        double 追加経過ミリ秒,
        double? 検索平均ナノ秒) =>
        new(
            実行ID,
            計測日時,
            ケース.ケース名,
            ケース.コレクション,
            ケース.ジェネリック名,
            ケース.バックエンド,
            ケース.スイープ軸,
            ケース.設定.Indexサイズ,
            ケース.設定.Pageサイズ,
            ケース.設定.Segmentサイズ,
            ケース.設定.Memoryサイズ,
            ケース.設定.可変Fraction,
            最大成功Count,
            エントリCount,
            失敗種別,
            失敗メッセージ,
            追加経過ミリ秒,
            検索平均ナノ秒);

    private static パラメータスイープ失敗種別 メモリ不足を分類(OutOfMemoryException 例外) {
        var メッセージ = 例外.Message;
        return メッセージ.Contains("log",StringComparison.OrdinalIgnoreCase)
            ||メッセージ.Contains("Tsavorite",StringComparison.OrdinalIgnoreCase)
            ? パラメータスイープ失敗種別.ログ満杯
            : パラメータスイープ失敗種別.メモリ不足;
    }

    private static パラメータスイープ失敗種別 失敗を分類(Exception 例外) => 例外 switch {
        TimeoutException => パラメータスイープ失敗種別.タイムアウト,
        OutOfMemoryException メモリ不足例外 => メモリ不足を分類(メモリ不足例外),
        ArgumentException or NotSupportedException => パラメータスイープ失敗種別.無効な構成,
        _ => パラメータスイープ失敗種別.例外,
    };

    private readonly record struct パラメータスイープ範囲(long 開始,long 終了);

    private sealed record パラメータスイープワーカー失敗(パラメータスイープ失敗種別 種別,string メッセージ);

    private sealed class パラメータスイープワーカー実行(int 番号,int? 論理CPU,Iパラメータスイープワーカー ワークロード):IDisposable {
        public int 番号 { get; } = 番号;
        public int? 論理CPU { get; } = 論理CPU;
        public Iパラメータスイープワーカー ワークロード { get; } = ワークロード;
        public int ワーカー数 { get; set; }
        public long 追加操作Count { get; set; }
        public double 追加経過ミリ秒 { get; set; }
        public long 検索操作Count { get; set; }
        public double 検索経過ミリ秒 { get; set; }
        public パラメータスイープ失敗種別 失敗種別 { get; set; } = パラメータスイープ失敗種別.完了;
        public string 失敗メッセージ { get; set; } = string.Empty;

        public パラメータスイープワーカー指標 指標を作成() => new(
            this.論理CPU,
            this.追加操作Count,
            this.追加経過ミリ秒,
            this.検索操作Count,
            this.検索経過ミリ秒,
            this.失敗種別,
            this.失敗メッセージ);

        public void Dispose() => this.ワークロード.Dispose();
    }
}

internal static class パラメータスイープ設定バリデーター {
    public static void 検証(パラメータスイープ設定 設定) {
        二の累乗を検証(設定.Indexサイズ,nameof(設定.Indexサイズ));
        二の累乗を検証(設定.Pageサイズ,nameof(設定.Pageサイズ));
        二の累乗を検証(設定.Segmentサイズ,nameof(設定.Segmentサイズ));
        二の累乗を検証(設定.Memoryサイズ,nameof(設定.Memoryサイズ));
        if(設定.Indexサイズ<64) {
            throw new ArgumentOutOfRangeException(nameof(設定.Indexサイズ));
        }
        if(設定.Pageサイズ<4096) {
            throw new ArgumentOutOfRangeException(nameof(設定.Pageサイズ));
        }
        if(設定.Pageサイズ>設定.Segmentサイズ) {
            throw new ArgumentOutOfRangeException(nameof(設定.Segmentサイズ));
        }
        if(設定.Memoryサイズ<2*設定.Pageサイズ) {
            throw new ArgumentOutOfRangeException(nameof(設定.Memoryサイズ));
        }
        if(double.IsNaN(設定.可変Fraction)||設定.可変Fraction<0||設定.可変Fraction>1) {
            throw new ArgumentOutOfRangeException(nameof(設定.可変Fraction));
        }
    }

    private static void 二の累乗を検証(long 値,string 名前) {
        if(値<=0||(値&(値-1))!=0) {
            throw new ArgumentOutOfRangeException(名前);
        }
    }
}

internal interface Iパラメータスイープワークロード:IDisposable {
    long エントリCount { get; }
    Iパラメータスイープワーカー ワーカーを作成();
}

internal interface Iパラメータスイープワーカー:IDisposable {
    void 追加(int 番号);
    bool 検索で見つかるか(int 番号);
}

internal static class パラメータスイープワークロードファクトリー {
    public static Iパラメータスイープワークロード 作成(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション) =>
        ケース.コレクション switch {
            パラメータスイープコレクション種別.ハッシュセット => ハッシュセットを作成(ケース,オプション),
            パラメータスイープコレクション種別.セット => セットを作成(ケース,オプション),
            パラメータスイープコレクション種別.辞書 => 辞書を作成(ケース,オプション),
            _ => throw new ArgumentOutOfRangeException(nameof(ケース)),
        };

    private static Iパラメータスイープワークロード ハッシュセットを作成(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション) =>
        ケース.ジェネリック名 switch {
            "Int32" => new ハッシュセットワークロード<int>(ケース,オプション,static 番号 => 番号),
            "String" => new ハッシュセットワークロード<string>(ケース,オプション,static 番号 => "K"+番号),
            "UnmanagedStruct" => new ハッシュセットワークロード<UnmanagedStruct>(ケース,オプション,static 番号 => new() { Value=番号 }),
            "ManagedStruct" => new ハッシュセットワークロード<ManagedStruct>(ケース,オプション,static 番号 => new() { Name="N"+番号 }),
            "ManagedClass" => new ハッシュセットワークロード<ManagedClass>(ケース,オプション,static 番号 => new() { Name="C"+番号 }),
            _ => throw new ArgumentOutOfRangeException(nameof(ケース.ジェネリック名),ケース.ジェネリック名,null),
        };

    private static Iパラメータスイープワークロード セットを作成(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション) =>
        ケース.ジェネリック名 switch {
            "非管理セットレコード" => new セットワークロード<int,パラメータスイープ非管理セットレコード>(ケース,オプション,static 番号 => new(番号,番号)),
            "管理セットレコード" => new セットワークロード<int,パラメータスイープ管理セットレコード>(ケース,オプション,static 番号 => new(番号,"N"+番号)),
            _ => throw new ArgumentOutOfRangeException(nameof(ケース.ジェネリック名),ケース.ジェネリック名,null),
        };

    private static Iパラメータスイープワークロード 辞書を作成(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション) =>
        ケース.ジェネリック名 switch {
            "String/String" => new 辞書ワークロード<string,string>(ケース,オプション,static 番号 => "K"+番号,static 番号 => "V"+番号),
            "String/Int32" => new 辞書ワークロード<string,int>(ケース,オプション,static 番号 => "K"+番号,static 番号 => 番号),
            "Int32/String" => new 辞書ワークロード<int,string>(ケース,オプション,static 番号 => 番号,static 番号 => "V"+番号),
            "Int32/Int32" => new 辞書ワークロード<int,int>(ケース,オプション,static 番号 => 番号,static 番号 => 番号),
            "UnmanagedStruct/UnmanagedStruct" => new 辞書ワークロード<UnmanagedStruct,UnmanagedStruct>(ケース,オプション,static 番号 => new() { Value=番号 },static 番号 => new() { Value=番号 }),
            "ManagedStruct/ManagedStruct" => new 辞書ワークロード<ManagedStruct,ManagedStruct>(ケース,オプション,static 番号 => new() { Name="K"+番号 },static 番号 => new() { Name="V"+番号 }),
            "ManagedClass/ManagedClass" => new 辞書ワークロード<ManagedClass,ManagedClass>(ケース,オプション,static 番号 => new() { Name="K"+番号 },static 番号 => new() { Name="V"+番号 }),
            _ => throw new ArgumentOutOfRangeException(nameof(ケース.ジェネリック名),ケース.ジェネリック名,null),
        };
}

internal abstract class ワークロード基底:IDisposable {
    private readonly string? ストレージディレクトリ;
    protected IDisposable? 破棄対象 { get; set; }
    private bool 破棄済みか;

    protected ワークロード基底(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション) {
        if(ケース.バックエンド==パラメータスイープバックエンド.ストレージ) {
            var ルート = オプション.作業ディレクトリ??Path.Combine(Path.GetTempPath(),"Seamless","TsavoriteParameterSweep");
            this.ストレージディレクトリ=Path.Combine(ルート,Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.ストレージディレクトリ);
        }
    }

    public abstract long エントリCount { get; }

    public void Dispose() {
        if(this.破棄済みか) {
            return;
        }
        this.破棄済みか=true;
        this.中核を破棄();
        this.破棄対象?.Dispose();
        if(this.ストレージディレクトリ is not null&&Directory.Exists(this.ストレージディレクトリ)) {
            try {
                Directory.Delete(this.ストレージディレクトリ,true);
            } catch(IOException) {
            } catch(UnauthorizedAccessException) {
            }
        }
    }

    protected abstract void 中核を破棄();

    protected ストア型 ストアを作成(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション,out TsavoriteStorageDevicePair? デバイス) {
        var 設定 = ケース.設定;
        var ストア = Seamless.Collections.Tsavorite.Store.Create(
            IndexSize: 設定.Indexサイズ,
            PageSize: 設定.Pageサイズ,
            SegmentSize: 設定.Segmentサイズ,
            MemorySize: 設定.Memoryサイズ,
            MutableFraction: 設定.可変Fraction);
        if(ケース.バックエンド==パラメータスイープバックエンド.メモリ) {
            デバイス=null;
            return ストア;
        }
        var ディレクトリ = this.ストレージディレクトリ??throw new InvalidOperationException("ストレージディレクトリが初期化されていません。");
        var チューニング = new StorageTuning(
            FileAccess.ReadWrite,
            StorageAccessPattern.Sequential,
            StorageBufferingMode.Buffered);
        デバイス=TsavoriteStorageDevices.CreatePair(
            Path.Combine(ディレクトリ,"log.data"),
            Path.Combine(ディレクトリ,"objectlog.data"),
            チューニング,
            deleteOnClose: true,
            capacity: オプション.ストレージデバイス容量);
        return ストア;
    }
}

internal sealed class ハッシュセットワークロード<Tキー>:ワークロード基底, Iパラメータスイープワークロード
    where Tキー : IEquatable<Tキー> {
    private readonly Func<int,Tキー> キーファクトリー;
    private readonly Seamless.Collections.Tsavorite.HashSetStore<Tキー> ストア;

    public ハッシュセットワークロード(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション,Func<int,Tキー> キーファクトリー) : base(ケース,オプション) {
        this.キーファクトリー=キーファクトリー;
        var ストアファクトリー = this.ストアを作成(ケース,オプション,out var デバイス);
        try {
            this.ストア=ケース.バックエンド==パラメータスイープバックエンド.ストレージ
                ? ストアファクトリー.Create<Tキー>(デバイス!.Value.LogDevice,デバイス.Value.ObjectLogDevice)
                : ストアファクトリー.Create<Tキー>();
            this.破棄対象=this.ストア;
        } catch {
            デバイス?.Dispose();
            throw;
        }
    }

    public override long エントリCount => this.ストア.Store.EntryCount;

    public Iパラメータスイープワーカー ワーカーを作成() => new ハッシュセットワーカー<Tキー>(this.ストア.Create(),this.キーファクトリー);

    protected override void 中核を破棄() {
    }
}

internal sealed class セットワークロード<Tキー, Tレコード>:ワークロード基底, Iパラメータスイープワークロード
    where Tキー : IEquatable<Tキー>
    where Tレコード : IRecord<Tキー> {
    private readonly Func<int,Tレコード> レコードファクトリー;
    private readonly Seamless.Collections.Tsavorite.SetStore<Tキー,Tレコード> ストア;

    public セットワークロード(パラメータスイープケース ケース,パラメータスイープ実行オプション オプション,Func<int,Tレコード> レコードファクトリー) : base(ケース,オプション) {
        this.レコードファクトリー=レコードファクトリー;
        var ストアファクトリー = this.ストアを作成(ケース,オプション,out var デバイス);
        try {
            this.ストア=ケース.バックエンド==パラメータスイープバックエンド.ストレージ
                ? ストアファクトリー.CreateSet<Tキー,Tレコード>(デバイス!.Value.LogDevice,デバイス.Value.ObjectLogDevice)
                : ストアファクトリー.CreateSet<Tキー,Tレコード>();
            this.破棄対象=this.ストア;
        } catch {
            デバイス?.Dispose();
            throw;
        }
    }

    public override long エントリCount => this.ストア.Store.EntryCount;

    public Iパラメータスイープワーカー ワーカーを作成() => new セットワーカー<Tキー,Tレコード>(this.ストア.Create(),this.レコードファクトリー);

    protected override void 中核を破棄() {
    }
}

internal sealed class 辞書ワークロード<Tキー, T値>:ワークロード基底, Iパラメータスイープワークロード
    where Tキー : IEquatable<Tキー>
    where T値 : IEquatable<T値> {
    private readonly Func<int,Tキー> キーファクトリー;
    private readonly Func<int,T値> 値ファクトリー;
    private readonly Seamless.Collections.Tsavorite.DictionaryStore<Tキー,T値> ストア;

    public 辞書ワークロード(
        パラメータスイープケース ケース,
        パラメータスイープ実行オプション オプション,
        Func<int,Tキー> キーファクトリー,
        Func<int,T値> 値ファクトリー) : base(ケース,オプション) {
        this.キーファクトリー=キーファクトリー;
        this.値ファクトリー=値ファクトリー;
        var ストアファクトリー = this.ストアを作成(ケース,オプション,out var デバイス);
        try {
            this.ストア=ケース.バックエンド==パラメータスイープバックエンド.ストレージ
                ? ストアファクトリー.Create<Tキー,T値>(デバイス!.Value.LogDevice,デバイス.Value.ObjectLogDevice)
                : ストアファクトリー.Create<Tキー,T値>();
            this.破棄対象=this.ストア;
        } catch {
            デバイス?.Dispose();
            throw;
        }
    }

    public override long エントリCount => this.ストア.Store.EntryCount;

    public Iパラメータスイープワーカー ワーカーを作成() => new 辞書ワーカー<Tキー,T値>(this.ストア.Create(),this.キーファクトリー,this.値ファクトリー);

    protected override void 中核を破棄() {
    }
}

internal sealed class ハッシュセットワーカー<Tキー>(Seamless.Collections.Tsavorite.HashSet<Tキー> セット,Func<int,Tキー> キーファクトリー):Iパラメータスイープワーカー
    where Tキー : IEquatable<Tキー> {
    public void 追加(int 番号) => セット.Add(キーファクトリー(番号));

    public bool 検索で見つかるか(int 番号) => セット.Contains(キーファクトリー(番号));

    public void Dispose() => セット.Dispose();
}

internal sealed class セットワーカー<Tキー, Tレコード>(Seamless.Collections.Tsavorite.Set<Tキー,Tレコード> セット,Func<int,Tレコード> レコードファクトリー):Iパラメータスイープワーカー
    where Tキー : IEquatable<Tキー>
    where Tレコード : IRecord<Tキー> {
    public void 追加(int 番号) => セット.Add(レコードファクトリー(番号));

    public bool 検索で見つかるか(int 番号) => セット.ContainsKey(レコードファクトリー(番号).Key);

    public void Dispose() => セット.Dispose();
}

internal sealed class 辞書ワーカー<Tキー, T値>(Seamless.Collections.Tsavorite.IDictionary<Tキー,T値> 辞書,Func<int,Tキー> キーファクトリー,Func<int,T値> 値ファクトリー):Iパラメータスイープワーカー
    where Tキー : IEquatable<Tキー>
    where T値 : IEquatable<T値> {
    public void 追加(int 番号) => 辞書.Add(キーファクトリー(番号),値ファクトリー(番号));

    public bool 検索で見つかるか(int 番号) => 辞書.TryGetValue(キーファクトリー(番号),out _);

    public void Dispose() => 辞書.Dispose();
}

internal static class パラメータスイープCPUアフィニティ {
    public static void 現在のスレッドへの適用を試みる(int? 論理CPU) {
        if(論理CPU is not { } CPU||CPU<0) {
            return;
        }
        if(OperatingSystem.IsWindows()) {
            Windowsへの適用を試みる(CPU);
        } else if(OperatingSystem.IsLinux()) {
            Linuxへの適用を試みる(CPU);
        }
    }

    private static void Windowsへの適用を試みる(int 論理CPU) {
        try {
            var 残り = 論理CPU;
            var グループCount = GetActiveProcessorGroupCount();
            for(ushort グループ = 0;グループ<グループCount;グループ++) {
                var 追加Count = (int)GetActiveProcessorCount(グループ);
                if(残り>=追加Count) {
                    残り-=追加Count;
                    continue;
                }
                var アフィニティ = new GroupAffinity {
                    Mask=new UIntPtr(1UL<<残り),
                    Group=グループ,
                };
                _=SetThreadGroupAffinity(GetCurrentThread(),ref アフィニティ,IntPtr.Zero);
                return;
            }
        } catch(DllNotFoundException) {
        } catch(EntryPointNotFoundException) {
        }
    }

    private static void Linuxへの適用を試みる(int 論理CPU) {
        var サイズ = Math.Max(8,(論理CPU/8)+1);
        var CPUセット = System.Runtime.InteropServices.Marshal.AllocHGlobal(サイズ);
        try {
            for(var 番号 = 0;番号<サイズ;番号++) {
                System.Runtime.InteropServices.Marshal.WriteByte(CPUセット,番号,0);
            }
            var バイトインデックス = 論理CPU/8;
            var 現在値 = System.Runtime.InteropServices.Marshal.ReadByte(CPUセット,バイトインデックス);
            System.Runtime.InteropServices.Marshal.WriteByte(CPUセット,バイトインデックス,(byte)(現在値|(1<<(論理CPU%8))));
            _=sched_setaffinity(0,(nuint)サイズ,CPUセット);
        } catch(DllNotFoundException) {
        } catch(EntryPointNotFoundException) {
        } finally {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(CPUセット);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError = true)]
    private static extern bool SetThreadGroupAffinity(IntPtr threadHandle,ref GroupAffinity groupAffinity,IntPtr previousGroupAffinity);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern ushort GetActiveProcessorGroupCount();

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetActiveProcessorCount(ushort groupNumber);

    [System.Runtime.InteropServices.DllImport("libc",SetLastError = true)]
    private static extern int sched_setaffinity(int pid,nuint cpusetsize,IntPtr mask);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct GroupAffinity {
        public UIntPtr Mask;
        public ushort Group;
        public ushort Reserved0;
        public ushort Reserved1;
        public ushort Reserved2;
    }
}
