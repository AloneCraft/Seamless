using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace UI;

public interface Iベンチマークカタログサービス {
    Task<IReadOnlyList<ベンチマークケース>> 読込Async(CancellationToken キャンセルトークン);
}

public sealed class ベンチマークカタログサービス:Iベンチマークカタログサービス {
    public Task<IReadOnlyList<ベンチマークケース>> 読込Async(CancellationToken キャンセルトークン) {
        return Task.Run(() => {
            キャンセルトークン.ThrowIfCancellationRequested();
#if BENCHMARK_UI_TYPED_CATALOG
            return ベンチマークカタログ.読み込む(型付きベンチマークアセンブリカタログ.アセンブリ一覧);
#else
            return ベンチマークカタログ.読み込む();
#endif
        },キャンセルトークン);
    }
}

public interface ICPUトポロジーサービス {
    IReadOnlyList<CPU選択項目> 読み込む();
}

public sealed class CPUトポロジーサービス:ICPUトポロジーサービス {
    public IReadOnlyList<CPU選択項目> 読み込む() {
        return ベンチマークCPUトポロジープロバイダー.読み込む()
            .OrderBy(static CPU => CPU.論理プロセッサインデックス)
            .Select((CPU,番号) => new CPU選択項目(
                CPU.論理プロセッサインデックス,
                CPU.コア種別,
                CPU.物理コアインデックス,
                CPU.種別内コアインデックス,
                番号==0))
            .ToArray();
    }
}

internal static class ベンチマークCPUトポロジープロバイダー {
    public static IReadOnlyList<CPUコア情報> 読み込む() {
        try {
            if(OperatingSystem.IsWindows()) {
                var Windows一覧 = Windowsを読み込む();
                if(Windows一覧.Count>0) {
                    return Windows一覧;
                }
            } else if(OperatingSystem.IsLinux()) {
                var Linux一覧 = Linuxを読み込む();
                if(Linux一覧.Count>0) {
                    return Linux一覧;
                }
            } else if(OperatingSystem.IsMacOS()||OperatingSystem.IsMacCatalyst()) {
                return MacOSを読み込む();
            }
        } catch {
        }

        return フォールバックを読み込む(OperatingSystem.IsWindows()||OperatingSystem.IsLinux());
    }

    private static IReadOnlyList<CPUコア情報> フォールバックを読み込む(bool アフィニティ対応か) {
        return global::System.Linq.Enumerable.Range(0,Environment.ProcessorCount)
            .Select(番号 => CPUを作成(番号,CPUコア種別.不明,番号,番号,アフィニティ対応か))
            .ToArray();
    }

    private static IReadOnlyList<CPUコア情報> Windowsを読み込む() {
        var 長さ = 0;
        _=GetLogicalProcessorInformationEx(プロセッサーコア関係,IntPtr.Zero,ref 長さ);
        if(長さ<=0) {
            return [];
        }

        var バッファ = Marshal.AllocHGlobal(長さ);
        try {
            if(!GetLogicalProcessorInformationEx(プロセッサーコア関係,バッファ,ref 長さ)) {
                return [];
            }

            var コア一覧 = new List<Windowsコア>();
            var オフセット = 0;
            while(オフセット<長さ) {
                var 項目 = IntPtr.Add(バッファ,オフセット);
                var サイズ = Marshal.ReadInt32(項目,4);
                if(サイズ<=0) {
                    break;
                }

                var 効率クラス = Marshal.ReadByte(項目,9);
                var グループ数 = Marshal.ReadInt16(項目,30);
                var 論理プロセッサー一覧 = new List<int>();
                var グループアフィニティオフセット = 32;
                var グループアフィニティサイズ = IntPtr.Size+8;
                for(var グループ番号 = 0;グループ番号<グループ数;グループ番号++) {
                    var アフィニティ = IntPtr.Add(項目,グループアフィニティオフセット+(グループ番号*グループアフィニティサイズ));
                    var マスク = UIntPtrを読む(アフィニティ);
                    var グループ = Marshal.ReadInt16(アフィニティ,IntPtr.Size);
                    if(グループ<0) {
                        continue;
                    }

                    マスクビットを追加(論理プロセッサー一覧,グループ*64,マスク);
                }

                if(論理プロセッサー一覧.Count>0) {
                    コア一覧.Add(new Windowsコア(効率クラス,論理プロセッサー一覧.Order().ToArray()));
                }

                オフセット+=サイズ;
            }

            return コアグループから作成(コア一覧.Select(static コア => new コアグループ(
                コア.論理プロセッサー一覧,
                コア.効率クラス))).ToArray();
        } finally {
            Marshal.FreeHGlobal(バッファ);
        }
    }

    private static IReadOnlyList<CPUコア情報> Linuxを読み込む() {
        const string CPUルート = "/sys/devices/system/cpu";
        var オンライン一覧 = LinuxオンラインCPU一覧を読む(Path.Combine(CPUルート,"online"));
        if(オンライン一覧.Count==0) {
            オンライン一覧=global::System.Linq.Enumerable.Range(0,Environment.ProcessorCount).ToArray();
        }

        var グループ一覧 = オンライン一覧
            .Select(CPU => new {
                CPU,
                コアキー = Linuxコアキーを読む(CPUルート,CPU),
                容量 = Linux整数を読む(Path.Combine(CPUルート,$"cpu{CPU}","cpu_capacity")),
            })
            .GroupBy(static 項目 => 項目.コアキー)
            .Select(static グループ => new コアグループ(
                グループ.Select(static 項目 => 項目.CPU).Order().ToArray(),
                グループ.Max(static 項目 => 項目.容量)))
            .ToArray();

        return コアグループから作成(グループ一覧).ToArray();
    }

    private static IReadOnlyList<CPUコア情報> MacOSを読み込む() {
        var 論理数 = Sysctl整数を読む("hw.logicalcpu")??Environment.ProcessorCount;
        var 高性能数 = Sysctl整数を読む("hw.perflevel0.logicalcpu");
        var 高効率数 = Sysctl整数を読む("hw.perflevel1.logicalcpu");
        var 項目一覧 = new List<CPUコア情報>(論理数);
        var 番号 = 0;

        if(高性能数 is >0) {
            for(var 種別内番号 = 0;種別内番号<高性能数&&番号<論理数;種別内番号++, 番号++) {
                項目一覧.Add(CPUを作成(番号,CPUコア種別.高性能,番号,種別内番号,false));
            }
        }

        if(高効率数 is >0) {
            for(var 種別内番号 = 0;種別内番号<高効率数&&番号<論理数;種別内番号++, 番号++) {
                項目一覧.Add(CPUを作成(番号,CPUコア種別.高効率,番号,種別内番号,false));
            }
        }

        while(番号<論理数) {
            項目一覧.Add(CPUを作成(番号,CPUコア種別.不明,番号,番号,false));
            番号++;
        }

        return 項目一覧;
    }

    private static IEnumerable<CPUコア情報> コアグループから作成(IEnumerable<コアグループ> 元グループ一覧) {
        var グループ一覧 = 元グループ一覧
            .Where(static グループ => グループ.論理プロセッサー一覧.Count>0)
            .OrderBy(static グループ => グループ.論理プロセッサー一覧.Min())
            .ToArray();
        if(グループ一覧.Length==0) {
            yield break;
        }

        var 既知スコア一覧 = グループ一覧
            .Select(static グループ => グループ.種別スコア)
            .Where(static スコア => スコア.HasValue)
            .Select(static スコア => スコア!.Value)
            .Distinct()
            .Order()
            .ToArray();
        var ハイブリッドスコアありか = 既知スコア一覧.Length>1;
        var 最大スコア = ハイブリッドスコアありか ? 既知スコア一覧[^1] : (int?)null;
        var 最小スコア = ハイブリッドスコアありか ? 既知スコア一覧[0] : (int?)null;
        if(ハイブリッドスコアありか) {
            グループ一覧=単独高効率グループを統合(グループ一覧,最小スコア);
        }

        var 次の種別内番号 = new Dictionary<CPUコア種別,int>();
        for(var 物理番号 = 0;物理番号<グループ一覧.Length;物理番号++) {
            var グループ = グループ一覧[物理番号];
            var 種別 = CPUコア種別.不明;
            if(ハイブリッドスコアありか&&グループ.種別スコア==最大スコア) {
                種別=CPUコア種別.高性能;
            } else if(ハイブリッドスコアありか&&グループ.種別スコア==最小スコア) {
                種別=CPUコア種別.高効率;
            }

            var 種別内番号 = 次の種別内番号.TryGetValue(種別,out var 現在番号) ? 現在番号 : 0;
            次の種別内番号[種別]=種別内番号+1;
            foreach(var 論理プロセッサー in グループ.論理プロセッサー一覧.Order()) {
                yield return CPUを作成(論理プロセッサー,種別,物理番号,種別内番号,アフィニティを使用できるか(論理プロセッサー));
            }
        }
    }

    private static コアグループ[] 単独高効率グループを統合(コアグループ[] グループ一覧,int? 高効率スコア) {
        if(高効率スコア is null) {
            return グループ一覧;
        }

        var 統合一覧 = new List<コアグループ>(グループ一覧.Length);
        for(var 番号 = 0;番号<グループ一覧.Length;) {
            var グループ = グループ一覧[番号];
            if(グループ.種別スコア!=高効率スコア||グループ.論理プロセッサー一覧.Count!=1) {
                統合一覧.Add(グループ);
                番号++;
                continue;
            }

            var 連続一覧 = new List<コアグループ>();
            while(番号<グループ一覧.Length
                &&グループ一覧[番号].種別スコア==高効率スコア
                &&グループ一覧[番号].論理プロセッサー一覧.Count==1) {
                連続一覧.Add(グループ一覧[番号]);
                番号++;
            }

            for(var 連続番号 = 0;連続番号<連続一覧.Count;連続番号+=4) {
                統合一覧.Add(new コアグループ(
                    連続一覧
                        .Skip(連続番号)
                        .Take(4)
                        .SelectMany(static 項目 => 項目.論理プロセッサー一覧)
                        .Order()
                        .ToArray(),
                    高効率スコア));
            }
        }

        return 統合一覧.ToArray();
    }

    private static CPUコア情報 CPUを作成(int 論理プロセッサー番号,CPUコア種別 種別,int 物理コア番号,int 種別内コア番号,bool アフィニティ対応か) {
        var アフィニティ利用可能か = アフィニティ対応か&&アフィニティを使用できるか(論理プロセッサー番号);
        var マスク = アフィニティ利用可能か ? 1L<<論理プロセッサー番号 : 0L;
        return new CPUコア情報(論理プロセッサー番号,種別,物理コア番号,種別内コア番号,マスク,アフィニティ利用可能か);
    }

    private static bool アフィニティを使用できるか(int 論理プロセッサー番号) {
        return 論理プロセッサー番号>=0&&論理プロセッサー番号<(IntPtr.Size*8)-1;
    }

    private static UIntPtr UIntPtrを読む(IntPtr アドレス) {
        return IntPtr.Size==8
            ? new UIntPtr(unchecked((ulong)Marshal.ReadInt64(アドレス)))
            : new UIntPtr(unchecked((uint)Marshal.ReadInt32(アドレス)));
    }

    private static void マスクビットを追加(List<int> 論理プロセッサー一覧,int 基準番号,UIntPtr マスク) {
        var 値 = マスク.ToUInt64();
        for(var ビット = 0;ビット<64;ビット++) {
            if((値&(1UL<<ビット))!=0) {
                論理プロセッサー一覧.Add(基準番号+ビット);
            }
        }
    }

    private static IReadOnlyList<int> LinuxオンラインCPU一覧を読む(string パス) {
        if(!File.Exists(パス)) {
            return [];
        }

        var 文字列 = File.ReadAllText(パス).Trim();
        var 結果一覧 = new List<int>();
        foreach(var 部分 in 文字列.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)) {
            var 範囲 = 部分.Split('-',StringSplitOptions.TrimEntries);
            if(範囲.Length==2
                &&int.TryParse(範囲[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out var 開始)
                &&int.TryParse(範囲[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out var 終了)) {
                for(var CPU = 開始;CPU<=終了;CPU++) {
                    結果一覧.Add(CPU);
                }
            } else if(int.TryParse(部分,NumberStyles.Integer,CultureInfo.InvariantCulture,out var 単一)) {
                結果一覧.Add(単一);
            }
        }

        return 結果一覧.Order().Distinct().ToArray();
    }

    private static string Linuxコアキーを読む(string CPUルート,int CPU) {
        var トポロジー = Path.Combine(CPUルート,$"cpu{CPU}","topology");
        var パッケージID = Linux文字列を読む(Path.Combine(トポロジー,"physical_package_id"))??"0";
        var コアID = Linux文字列を読む(Path.Combine(トポロジー,"core_id"))??CPU.ToString(CultureInfo.InvariantCulture);
        return パッケージID+":"+コアID;
    }

    private static string? Linux文字列を読む(string パス) {
        return File.Exists(パス) ? File.ReadAllText(パス).Trim() : null;
    }

    private static int? Linux整数を読む(string パス) {
        var 文字列 = Linux文字列を読む(パス);
        return int.TryParse(文字列,NumberStyles.Integer,CultureInfo.InvariantCulture,out var 値)
            ? 値
            : null;
    }

    private static int? Sysctl整数を読む(string キー) {
        try {
            using var プロセス = new Process {
                StartInfo=new ProcessStartInfo {
                    FileName="sysctl",
                    UseShellExecute=false,
                    RedirectStandardOutput=true,
                    RedirectStandardError=true,
                    CreateNoWindow=true,
                },
            };
            プロセス.StartInfo.ArgumentList.Add("-n");
            プロセス.StartInfo.ArgumentList.Add(キー);
            プロセス.Start();
            var 出力 = プロセス.StandardOutput.ReadToEnd();
            プロセス.WaitForExit(2000);
            return プロセス.ExitCode==0&&int.TryParse(出力.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var 値)
                ? 値
                : null;
        } catch {
            return null;
        }
    }

    [DllImport("kernel32.dll",SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(int 関係種別,IntPtr バッファ,ref int 返却長);

    private const int プロセッサーコア関係 = 0;

    private sealed record Windowsコア(byte 効率クラス,IReadOnlyList<int> 論理プロセッサー一覧);
    private sealed record コアグループ(IReadOnlyList<int> 論理プロセッサー一覧,int? 種別スコア);
}

public interface Iベンチマークワーカープロセスサービス:Iベンチマークジョブ実行器 {
}

public sealed class ベンチマークワーカープロセスサービス:Iベンチマークワーカープロセスサービス {
    private readonly ベンチマークワーカークライアント クライアント = new();

    public Task<ベンチマークジョブ実行結果> 実行Async(ベンチマークジョブ ジョブ,string 実行ルート,int 論理CPU,TimeSpan タイムアウト,CancellationToken キャンセルトークン) =>
        this.クライアント.実行Async(ジョブ,実行ルート,論理CPU,タイムアウト,キャンセルトークン);

}

public sealed class ベンチマーク永続ワーカープロセスサービス:Iベンチマークジョブ実行器, Iベンチマーク動的CPUセット実行器, IAsyncDisposable {
    private readonly ベンチマークワーカークライアント クライアント = new();
    private readonly ConcurrentDictionary<int,ベンチマークワーカーセッション> セッション一覧 = new();
    private readonly ConcurrentDictionary<int,int> 実行中ジョブ数一覧 = new();

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) {
        var セッション = await this.セッションを取得Async(実行ルート,論理CPU,キャンセルトークン);
        using var タイムアウトソース = new CancellationTokenSource(タイムアウト);
        using var 連結ソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
        this.実行中ジョブ数一覧.AddOrUpdate(論理CPU,1,static (_,件数) => 件数+1);
        try {
            var 結果 = await セッション.実行Async(ジョブ.ベンチマークケースに変換(),ジョブ.ソースタブ,ジョブ.準備済み要素数一覧,ジョブ.要素数,ジョブ.ループ回数,"1",連結ソース.Token);
            return 結果 with { 実行項目インデックス=ジョブ.実行項目インデックス };
        } catch(OperationCanceledException) {
            await this.セッションを再設定Async(論理CPU,セッション);
            if(タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested) {
                throw new TimeoutException($"ベンチマークジョブが {タイムアウト} 後にタイムアウトしました。");
            }

            throw;
        } catch {
            await this.セッションを再設定Async(論理CPU,セッション);
            throw;
        } finally {
            this.実行中ジョブ数一覧.AddOrUpdate(論理CPU,0,static (_,件数) => Math.Max(0,件数-1));
        }
    }

    public async Task CPUセットを適用Async(IReadOnlyList<int> 論理CPU一覧) {
        var 有効CPU一覧 = 論理CPU一覧.ToHashSet();
        foreach(var 組 in this.セッション一覧.ToArray()) {
            if(有効CPU一覧.Contains(組.Key)||this.実行中ジョブ数一覧.GetValueOrDefault(組.Key)>0) {
                continue;
            }

            await this.セッションを再設定Async(組.Key,組.Value);
        }
    }

    public async ValueTask DisposeAsync() {
        var セッション一覧 = this.セッション一覧.Values.ToArray();
        this.セッション一覧.Clear();
        foreach(var セッション in セッション一覧) {
            await セッション.DisposeAsync();
        }
    }

    private async Task<ベンチマークワーカーセッション> セッションを取得Async(
        string 実行ルート,
        int 論理CPU,
        CancellationToken キャンセルトークン) {
        if(this.セッション一覧.TryGetValue(論理CPU,out var 既存セッション)) {
            return 既存セッション;
        }

        var 作成セッション = await this.クライアント.セッション開始Async(実行ルート,論理CPU,キャンセルトークン);
        if(this.セッション一覧.TryAdd(論理CPU,作成セッション)) {
            return 作成セッション;
        }

        await 作成セッション.DisposeAsync();
        return this.セッション一覧[論理CPU];
    }

    private async Task セッションを再設定Async(int 論理CPU,ベンチマークワーカーセッション セッション) {
        if(this.セッション一覧.TryGetValue(論理CPU,out var 現在セッション)
            &&ReferenceEquals(現在セッション,セッション)
            &&this.セッション一覧.TryRemove(論理CPU,out _)) {
            await セッション.DisposeAsync();
        }
    }
}

public sealed class ベンチマーク高速ワーカープロセスサービス:Iベンチマークジョブ実行器, IAsyncDisposable {
    private readonly SemaphoreSlim セッションゲート = new(1,1);
    private ベンチマーク高速ワーカーセッション? セッション;
    private string 実行ルート = string.Empty;

    public async Task<ベンチマークジョブ実行結果> 実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン) {
        var 現在セッション = await this.セッションを取得Async(実行ルート,キャンセルトークン);
        using var タイムアウトソース = new CancellationTokenSource(タイムアウト);
        using var 連結ソース = CancellationTokenSource.CreateLinkedTokenSource(キャンセルトークン,タイムアウトソース.Token);
        try {
            return await 現在セッション.実行Async(ジョブ,論理CPU,連結ソース.Token);
        } catch(OperationCanceledException) {
            await this.セッションを再設定Async(現在セッション);
            if(タイムアウトソース.IsCancellationRequested&&!キャンセルトークン.IsCancellationRequested) {
                throw new TimeoutException($"ベンチマークジョブが {タイムアウト} 後にタイムアウトしました。");
            }

            throw;
        } catch {
            await this.セッションを再設定Async(現在セッション);
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        await this.セッションゲート.WaitAsync();
        try {
            if(this.セッション is not null) {
                await this.セッション.DisposeAsync();
                this.セッション=null;
            }
        } finally {
            this.セッションゲート.Release();
            this.セッションゲート.Dispose();
        }
    }

    private async Task<ベンチマーク高速ワーカーセッション> セッションを取得Async(string 実行ルート,CancellationToken キャンセルトークン) {
        await this.セッションゲート.WaitAsync(キャンセルトークン);
        try {
            if(this.セッション is not null&&string.Equals(this.実行ルート,実行ルート,StringComparison.OrdinalIgnoreCase)) {
                return this.セッション;
            }

            if(this.セッション is not null) {
                await this.セッション.DisposeAsync();
                this.セッション=null;
            }

            this.実行ルート=実行ルート;
            this.セッション=await ベンチマーク高速ワーカーセッション.開始Async(実行ルート,キャンセルトークン);
            return this.セッション;
        } finally {
            this.セッションゲート.Release();
        }
    }

    private async Task セッションを再設定Async(ベンチマーク高速ワーカーセッション 現在セッション) {
        await this.セッションゲート.WaitAsync();
        try {
            if(ReferenceEquals(this.セッション,現在セッション)) {
                this.セッション=null;
                await 現在セッション.DisposeAsync();
            }
        } finally {
            this.セッションゲート.Release();
        }
    }
}

public interface Iベンチマーク結果サービス {
    報告行 報告行に変換(ベンチマーク結果項目 項目);
}

public sealed class ベンチマーク結果サービス:Iベンチマーク結果サービス {
    public 報告行 報告行に変換(ベンチマーク結果項目 項目) {
        var メソッド = 項目.要素数>0
            ? 項目.メソッド+" [N="+項目.要素数.ToString(CultureInfo.InvariantCulture)+"]"
            : 項目.メソッド;
        return new 報告行(
            項目.プロジェクト,
            メソッド,
            項目.型完全名,
            メソッド,
            項目.状態,
            項目.平均,
            string.Empty,
            string.Empty,
            項目.割り当てバイト数値?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            項目.平均ナノ秒??0,
            項目.誤差ナノ秒??0,
            項目.標準偏差ナノ秒??0,
            項目.割り当てバイト数値??0,
            0,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            項目.成果物フォルダー,
            項目.終了コード,
            項目.引数キー,
            項目.引数一覧);
    }
}

public interface Iベンチマーク比較サービス {
    IReadOnlyList<ベンチマーク比較行> 比較行を構築(IEnumerable<ベンチマーク結果項目> 結果一覧);
}

public sealed class ベンチマーク比較サービス(Iベンチマーク結果サービス 結果サービス):Iベンチマーク比較サービス {
    public IReadOnlyList<ベンチマーク比較行> 比較行を構築(IEnumerable<ベンチマーク結果項目> 結果一覧) {
        var 報告行一覧 = 結果一覧.Select(結果サービス.報告行に変換).ToArray();
        return ベンチマーク比較フォーマッター.比較行を構築(報告行一覧);
    }

}

public interface Iベンチマーク実行サービス {
    Task<ベンチマーク実行報告> 実行Async(
        IReadOnlyList<ベンチマークケース> ベンチマーク一覧,
        IReadOnlyList<int> 論理CPU一覧,
        Func<IReadOnlyList<int>> 論理CPU取得器,
        IReadOnlyList<int> 要素数一覧,
        ベンチマークソースタブ ソースタブ,
        ベンチマーク実行プロファイル 実行プロファイル,
        TimeSpan 適応しきい値,
        bool 要求要素数から再開するか,
        string? 実行ルート上書き,
        Func<IReadOnlyList<ベンチマークケース>>? ベンチマーク取得器,
        Action<ベンチマーク結果項目> 結果受信時,
        Action<string> 状態変更時,
        ベンチマーク実行オプション? 実行オプション,
        CancellationToken キャンセルトークン);
}

public sealed class ベンチマーク実行サービス(Iベンチマークワーカープロセスサービス ワーカーサービス):Iベンチマーク実行サービス {
    public async Task<ベンチマーク実行報告> 実行Async(
        IReadOnlyList<ベンチマークケース> ベンチマーク一覧,
        IReadOnlyList<int> 論理CPU一覧,
        Func<IReadOnlyList<int>> 論理CPU取得器,
        IReadOnlyList<int> 要素数一覧,
        ベンチマークソースタブ ソースタブ,
        ベンチマーク実行プロファイル 実行プロファイル,
        TimeSpan 適応しきい値,
        bool 要求要素数から再開するか,
        string? 実行ルート上書き,
        Func<IReadOnlyList<ベンチマークケース>>? ベンチマーク取得器,
        Action<ベンチマーク結果項目> 結果受信時,
        Action<string> 状態変更時,
        ベンチマーク実行オプション? 実行オプション,
        CancellationToken キャンセルトークン) {
        var オーケストレーションルート = ソリューションルートを検索()??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless");
        var ベンチマークルート = Path.Combine(オーケストレーションルート,"Work",ベンチマークアプリプロファイル.作業ルート名,ベンチマーク実行プロファイル名.フォルダー名に変換(実行プロファイル));
        var ジョブ実行器 = this.ジョブ実行器を作成(実行プロファイル,適応しきい値,実行オプション);
        var オーケストレーター = new ベンチマークオーケストレーター(
            new ベンチマーク状態マネージャー(ベンチマークルート),
            ジョブ実行器);
        var 要求 = new ベンチマーク実行要求(
            ベンチマーク一覧,
            論理CPU一覧,
            要素数一覧,
            適応しきい値,
            TimeSpan.FromMinutes(60),
            論理CPU取得器,
            要求要素数から再開するか: 要求要素数から再開するか,
            実行プロファイル: 実行プロファイル,
            実行ルート上書き: 実行ルート上書き,
            ソースタブ: ソースタブ,
            ベンチマーク取得器: ベンチマーク取得器);
        try {
            var オーケストレーター結果 = await オーケストレーター.実行Async(
                要求,
                更新 => 結果受信時(new ベンチマーク結果項目(更新.ジョブ)),
                状態変更時,
                キャンセルトークン);
            return new ベンチマーク実行報告(オーケストレーター結果.実行ルート);
        } finally {
            if(ジョブ実行器 is IAsyncDisposable 非同期破棄対象) {
                await 非同期破棄対象.DisposeAsync();
            } else if(ジョブ実行器 is IDisposable 破棄対象) {
                破棄対象.Dispose();
            }
        }

    }

    private Iベンチマークジョブ実行器 ジョブ実行器を作成(ベンチマーク実行プロファイル 実行プロファイル,TimeSpan テスト待機時間,ベンチマーク実行オプション? 実行オプション) =>
        実行プロファイル switch {
            ベンチマーク実行プロファイル.正確 => ワーカーサービス,
            ベンチマーク実行プロファイル.中間 => new ベンチマーク永続ワーカープロセスサービス(),
            ベンチマーク実行プロファイル.高速 => new ベンチマーク高速ワーカープロセスサービス(),
            ベンチマーク実行プロファイル.テスト => new ベンチマークテスト待機ジョブ実行器(テスト待機時間,実行オプション?.無限テスト実行器を使うか==true),
            _ => ワーカーサービス,
        };

    private static string? ソリューションルートを検索() {
        var ディレクトリ = new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null) {
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
                return ディレクトリ.FullName;
            }
            ディレクトリ=ディレクトリ.Parent;
        }

        return null;
    }

}
