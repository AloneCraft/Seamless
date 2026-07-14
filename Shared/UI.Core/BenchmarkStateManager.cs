using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class ベンチマーク状態マネージャー{
    private static readonly JsonSerializerOptions JSONオプション=new(){
        WriteIndented=true,
    };

    private readonly object ファイルゲート=new();
    private readonly string ベンチマークルート;
    private static readonly ConcurrentDictionary<string,進行状態>状態一覧=new(StringComparer.OrdinalIgnoreCase);

    public ベンチマーク状態マネージャー(string ベンチマークルート){
        this.ベンチマークルート=ベンチマークルート;
    }

    public string 次の実行ルートを作成(){
        Directory.CreateDirectory(this.ベンチマークルート);
        var 次番号=Directory.EnumerateDirectories(this.ベンチマークルート)
            .Select(static パス=>実行番号の解析を試みる(パス))
            .DefaultIfEmpty(0)
            .Max()+1;
        while(true){
            var 実行ルート=Path.Combine(this.ベンチマークルート,次番号.ToString("D6",CultureInfo.InvariantCulture));
            if(!Directory.Exists(実行ルート)){
                Directory.CreateDirectory(実行ルート);
                return 実行ルート;
            }

            次番号++;
        }
    }

    public static int 実行番号の解析を試みる(string 実行ルート){
        var 名前=Path.GetFileName(Path.TrimEndingDirectorySeparator(実行ルート));
        if(名前.Length==6&&int.TryParse(名前,NumberStyles.None,CultureInfo.InvariantCulture,out var 番号)){
            return 番号;
        }
        return 0;
    }

    public 進行状態?読み込む(string 実行ルート){
        if(!状態一覧.TryGetValue(実行ルート,out var 状態)){
            return null;
        }

        状態=複製(状態);
        var 変更ありか=false;
        foreach(var ジョブ in 状態.ジョブ一覧.Where(static ジョブ=>ジョブ.状態 is ジョブ状態.実行中 or ジョブ状態.一時停止)){
            ジョブ.状態=ジョブ状態.失敗;
            ジョブ.失敗理由="クラッシュ、メモリ不足、または実行中・一時停止中のプロセス終了を検出しました。";
            ジョブ.終了日時=DateTimeOffset.UtcNow;
            変更ありか=true;
        }

        if(変更ありか){
            状態.更新日時=DateTimeOffset.UtcNow;
            this.保存(状態);
        }

        return 状態;
    }

    public void 保存(進行状態 状態){
        lock(this.ファイルゲート){
            状態.更新日時=DateTimeOffset.UtcNow;
            Directory.CreateDirectory(状態.実行ルート);
            状態一覧[状態.実行ルート]=複製(状態);
        }
    }

    private static 進行状態 複製(進行状態 元){
        var JSON=JsonSerializer.Serialize(元,JSONオプション);
        return JsonSerializer.Deserialize<進行状態>(JSON,JSONオプション)
            ??throw new InvalidDataException("進行状態が空です。");
    }
}
