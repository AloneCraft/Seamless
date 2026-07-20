namespace UI;

public enum ベンチマークプロジェクト実行状態 {
    待機中,
    実行中,
    完了,
}

public enum ベンチマーク測定強調状態 {
    なし,
    新規,
    実行中保持,
}

public enum ベンチマーク状態画像種別 {
    なし,
    待機中,
    実行中,
    最終完了,
    失敗,
    測定なし,
}

internal static class ベンチマーク状態画像名 {
    public const string 待機中="status_waiting.png";
    public const string 完了="status_completed.png";
    public const string 最終完了=完了;
    public const string 失敗="status_failed.png";
    public const string 測定なし="status_no_measurement.png";
    public const string CPU待機中="status_cpu_idle.png";
    public const string CPU実行中="status_cpu_running.png";

    public static string 種別から取得(ベンチマーク状態画像種別 種別)=>
        種別 switch {
            ベンチマーク状態画像種別.待機中=>待機中,
            ベンチマーク状態画像種別.最終完了=>最終完了,
            ベンチマーク状態画像種別.失敗=>失敗,
            ベンチマーク状態画像種別.測定なし=>測定なし,
            ベンチマーク状態画像種別.実行中=>CPU実行中,
            _ => string.Empty,
        };
}

internal static class ベンチマークCPUコア画像名 {
    public const string 高性能="cpu_p_core.png";
    public const string 高効率="cpu_e_core.png";

    public static string コア種別から取得(CPUコア種別 種別)=>
        種別 switch {
            CPUコア種別.高性能=>高性能,
            CPUコア種別.高効率=>高効率,
            _ => string.Empty,
        };
}

internal readonly record struct ベンチマーク状態画像入力(
    bool ケースありか,
    bool 実行中か,
    bool 失敗か,
    bool 測定値ありか,
    bool 測定なし完了か,
    bool 実行予定か,
    bool 行最終完了か,
    bool 今回実行待機中か=false);

internal static class ベンチマーク状態画像ロジック {
    public static ベンチマーク状態画像種別 プロジェクト画像種別を取得(ベンチマーク状態画像入力 入力) {
        if(!入力.ケースありか) {
            return ベンチマーク状態画像種別.なし;
        }

        if(入力.実行中か) {
            return ベンチマーク状態画像種別.実行中;
        }

        if(入力.今回実行待機中か) {
            // 今回の実行対象に選ばれているがまだ計測が届いていないセルは、
            // 前回実行の値(維持表示)があっても完了系アイコンではなく待機(時計)を表示する。
            return ベンチマーク状態画像種別.待機中;
        }

        if(入力.失敗か) {
            return ベンチマーク状態画像種別.失敗;
        }

        if(入力.測定値ありか) {
            // 開放ループ較正でループ回数の段階(1,2,4...)が廃止されたため、
            // 「指定回数完了」の中間段階は設けず測定値があれば完全完了として扱う。
            return ベンチマーク状態画像種別.最終完了;
        }

        if(入力.測定なし完了か) {
            return ベンチマーク状態画像種別.測定なし;
        }

        return ベンチマーク状態画像種別.待機中;
    }
}

internal static class ベンチマークCPU状態画像ロジック {
    public static string 画像ソースを取得(string 状態)=>
        string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.実行中),StringComparison.OrdinalIgnoreCase)
            ? ベンチマーク状態画像名.CPU実行中
            : string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
                ? ベンチマーク状態画像名.最終完了
                : string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.失敗),StringComparison.OrdinalIgnoreCase)
                    ? ベンチマーク状態画像名.失敗
                    : ベンチマーク状態画像名.CPU待機中;

    public static string セル状態文を取得(string 状態,string 現在ジョブ文)=>
        string.IsNullOrWhiteSpace(現在ジョブ文)
            ? (string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.保留),StringComparison.OrdinalIgnoreCase) ? string.Empty : 状態文を取得(状態))
            : 現在ジョブ文;

    public static string 状態文を取得(string 状態){
        if(ジョブ状態名.保存値と一致するか(状態,ジョブ状態.保留)){
            return "待機中";
        }
        if(ジョブ状態名.保存値と一致するか(状態,ジョブ状態.実行中)){
            return "実行中";
        }
        if(ジョブ状態名.保存値と一致するか(状態,ジョブ状態.一時停止)){
            return "一時停止";
        }
        if(ジョブ状態名.保存値と一致するか(状態,ジョブ状態.完了)){
            return "完了";
        }
        if(ジョブ状態名.保存値と一致するか(状態,ジョブ状態.失敗)){
            return "失敗";
        }
        return 状態;
    }
}

internal static class ベンチマーク測定強調ロジック {
    public static ベンチマーク測定強調状態 実行要素数へ進める(
        ベンチマーク測定強調状態 現在,
        int? 測定要素数,
        int 実行要素数)=>
        現在==ベンチマーク測定強調状態.新規
        &&測定要素数 is { } 測定済み
        &&実行要素数>測定済み
            ? ベンチマーク測定強調状態.実行中保持
            : 現在;

    public static ベンチマーク測定強調状態 実行後にクリア()=>
        ベンチマーク測定強調状態.なし;
}

internal static class ベンチマーク実行表示元ロジック {
    public static bool 今回実行メモリを使うか(bool 選択タブに今回実行結果があるか)=>
        選択タブに今回実行結果があるか;

    public static bool 今回実行メモリ表示を維持するか(bool 今回実行結果があるか)=>
        今回実行結果があるか;

    public static bool 選択実行をDBから読み込むか(
        bool 選択タブに今回実行結果があるか,
        bool 今回実行結果があるか,
        bool ベンチマーク実行中か,
        bool 選択実行DB読込中か,
        bool 外部実行選択肢か,
        bool 新規実行選択肢か,
        bool 一時実行選択肢か,
        bool 保存済み実行選択ありか) {
        if(選択実行DB読込中か||ベンチマーク実行中か) {
            return false;
        }

        if(外部実行選択肢か||新規実行選択肢か||一時実行選択肢か) {
            return false;
        }

        if(選択タブに今回実行結果があるか||今回実行結果があるか) {
            return 保存済み実行選択ありか;
        }

        return 保存済み実行選択ありか;
    }
}

internal static class ベンチマーク日時表示 {
    public const string 書式="yyyy/MM/dd HH:mm:ss";

    public static string ローカル日時を整形(DateTimeOffset 日時)=>
        日時.ToLocalTime().ToString(書式,System.Globalization.CultureInfo.InvariantCulture);
}

internal static class ベンチマークプロジェクト実行状態ロジック {
    public static bool 待機中か(ベンチマークプロジェクト実行状態 状態,bool 測定値ありか)=>
        状態==ベンチマークプロジェクト実行状態.待機中&&!測定値ありか;

    public static bool 実行中か(ベンチマークプロジェクト実行状態 状態)=>
        状態==ベンチマークプロジェクト実行状態.実行中;

    public static bool 完了か(ベンチマークプロジェクト実行状態 状態)=>
        状態==ベンチマークプロジェクト実行状態.完了;
}

internal static class ベンチマーク要素数ロジック {
    public static IReadOnlyList<int> 適応候補を作成(IReadOnlyList<int>要素数一覧,int 最大要素数) {
        var 候補一覧=要素数一覧
            .Where(要素数=>要素数<=最大要素数)
            .Distinct()
            .Order()
            .ToArray();
        return 候補一覧.Length==0 ? [ベンチマーク要素数一覧.既定初期値[0]] : 候補一覧;
    }
}

internal readonly record struct ベンチマーク結果選択セル(
    string プロジェクト,
    string メソッド,
    string 型完全名,
    string 引数キー,
    bool ケースありか,
    bool 選択中か);

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク結果表示選択ロジック {
    public static bool 結果を選択中か(ベンチマーク結果項目 結果,IEnumerable<ベンチマーク結果選択セル> セル一覧) =>
        セル一覧.Any(セル => セル.ケースありか
            &&セル.選択中か
            &&string.Equals(セル.プロジェクト,結果.プロジェクト,StringComparison.OrdinalIgnoreCase)
            &&string.Equals(セル.メソッド,結果.メソッド,StringComparison.OrdinalIgnoreCase)
            &&ベンチマーク型一致ロジック.同じ型か(結果.型完全名,セル.型完全名)
            &&同じ引数か(結果.引数キー,セル.引数キー));

    private static bool 同じ引数か(string 左,string 右) =>
        string.IsNullOrWhiteSpace(右)
            ||string.Equals(左,右,StringComparison.OrdinalIgnoreCase);
}


public readonly record struct ベンチマークタブ進行(int 完了数,int 合計数) {
    public double 割合=>this.合計数<=0 ? 0 : (double)this.完了数/this.合計数;
    public string 割合文字列 => this.合計数<=0
        ? string.Empty
        : this.完了数.ToString(System.Globalization.CultureInfo.InvariantCulture)
            +"/"
            +this.合計数.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

internal readonly record struct ベンチマークソースタブ実行中ジョブ(
    ベンチマークソースタブ ソースタブ,
    string プロジェクト,
    string メソッド,
    string 型完全名,
    string 引数キー,
    int 軸数);

internal sealed class ベンチマークソースタブ実行中状態 {
    private readonly Lock 同期排他=new();
    private readonly System.Collections.Generic.HashSet<ベンチマークソースタブ実行中ジョブ>実行中ジョブ一覧=[];

    public bool 適用(ベンチマーク結果項目 結果) {
        var ジョブ=ジョブを作成(結果);
        lock(this.同期排他) {
            if(string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.実行中),StringComparison.OrdinalIgnoreCase)) {
                return this.実行中ジョブ一覧.Add(ジョブ);
            }

            if(string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
                ||string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.失敗),StringComparison.OrdinalIgnoreCase)) {
                return this.実行中ジョブ一覧.Remove(ジョブ);
            }
        }

        return false;
    }

    public bool 実行中か(ベンチマークソースタブ ソースタブ) {
        lock(this.同期排他) {
            foreach(var ジョブ in this.実行中ジョブ一覧) {
                if(ジョブ.ソースタブ==ソースタブ) {
                    return true;
                }
            }

            return false;
        }
    }

    public bool クリア() {
        lock(this.同期排他) {
            if(this.実行中ジョブ一覧.Count==0) {
                return false;
            }

            this.実行中ジョブ一覧.Clear();
            return true;
        }
    }

    public bool タブの実行中状態をクリア(ベンチマークソースタブ タブ) {
        lock(this.同期排他) {
            return this.実行中ジョブ一覧.RemoveWhere(ジョブ => ジョブ.ソースタブ==タブ)>0;
        }
    }

    private static ベンチマークソースタブ実行中ジョブ ジョブを作成(ベンチマーク結果項目 結果) =>
        new(
            結果.ソースタブ,
            結果.プロジェクト,
            結果.メソッド,
            結果.型完全名,
            結果.引数キー,
            結果.軸数);
}

internal static class ベンチマークタブ進行元ロジック {
    public static IReadOnlyList<ベンチマークタブ進行セル> 選択ケースからセルを作成(
        IEnumerable<ベンチマークケース> 選択ケース一覧,
        ベンチマークソースタブ定義 ソースタブ) {
        return 選択ケース一覧
            .Where(ソースタブ.ケースを含むか)
            .GroupBy(static ベンチマーク => ベンチマーク行グループ化ロジック.ケースキーを作成(
                ベンチマーク.プロジェクト,
                ベンチマーク.型完全名,
                ベンチマーク.メソッド名,
                ベンチマーク.引数キー),StringComparer.OrdinalIgnoreCase)
            .Select(static グループ => グループ.First())
            .Select(static ベンチマーク => new ベンチマークタブ進行セル(
                ベンチマーク.メソッド名,
                ベンチマーク.プロジェクト,
                ベンチマーク.型完全名,
                ベンチマーク.引数キー,
                true,
                ベンチマーク.メソッド名+"|"+ベンチマーク.引数キー))
            .ToArray();
    }
}

internal static class ベンチマーク失敗表示ロジック {
    public const string 既定失敗理由 = "失敗理由が報告されていません。";

    public static string ツールチップ理由を作成(string 失敗理由) {
        var 先頭行=最初の空でない行を取得(失敗理由);
        return "理由: "+(string.IsNullOrWhiteSpace(先頭行) ? 既定失敗理由 : 先頭行);
    }

    public static string ログ文を作成(
        string プロジェクト,
        string メソッド,
        int 要素数,
        int? 割り当て論理CPU,
        string 成果物フォルダー,
        string 失敗理由,
        string 型完全名 = "") {
        var 構築器=new System.Text.StringBuilder();
        構築器.AppendLine("ベンチマークに失敗しました。");
        構築器.AppendLine("プロジェクト="+プロジェクト);
        構築器.AppendLine("メソッド="+メソッド);
        if(!string.IsNullOrWhiteSpace(型完全名)) {
            構築器.AppendLine("型="+型完全名);
        }
        構築器.AppendLine("要素数="+要素数.ToString(System.Globalization.CultureInfo.InvariantCulture));
        構築器.AppendLine("CPU="+(割り当て論理CPU?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"-"));
        構築器.AppendLine("成果物="+(string.IsNullOrWhiteSpace(成果物フォルダー) ? "-" : 成果物フォルダー));
        構築器.AppendLine("失敗理由:");
        構築器.AppendLine(string.IsNullOrWhiteSpace(失敗理由) ? 既定失敗理由 : 失敗理由);
        return 構築器.ToString().TrimEnd();
    }

    private static string 最初の空でない行を取得(string 文字列) {
        using var 読取器 = new System.IO.StringReader(文字列??string.Empty);
        while(読取器.ReadLine() is { } 行文字列) {
            if(!string.IsNullOrWhiteSpace(行文字列)) {
                return 行文字列.Trim();
            }
        }

        return string.Empty;
    }
}

internal readonly record struct ベンチマークタブ進行セル(string メソッド,string プロジェクト,string 型完全名,string 引数キー,bool 選択中か,string ペアキー) {
    public ベンチマークタブ進行セル(string メソッド,string プロジェクト,string 型完全名,bool 選択中か)
        : this(メソッド,プロジェクト,型完全名,string.Empty,選択中か,string.Empty) {
    }

    public ベンチマークタブ進行セル(string メソッド,string プロジェクト,bool 選択中か)
        : this(メソッド,プロジェクト,string.Empty,string.Empty,選択中か,string.Empty) {
    }

    public ベンチマークタブ進行セル(string メソッド,string プロジェクト,string 型完全名,string 引数キー,bool 選択中か)
        : this(メソッド,プロジェクト,型完全名,引数キー,選択中か,string.Empty) {
    }
}

internal static class ベンチマーク引数キー一致ロジック {
    public static bool 厳密一致か(string 左,string 右) =>
        string.Equals(左,右,StringComparison.OrdinalIgnoreCase);
}

// 左/中央/右それぞれの型完全名を保存するようになった(実行ID単位で識別情報を1組に集約しない)ため、
// 「型完全名が空なら何にでも一致させる」曖昧許容は不要かつ危険(型違いの行が混線する原因だった)。
// 空は空同士でのみ一致させる。非空同士は、完全一致に加えて名前空間を除いた短縮名同士の一致も許容する
// (UI側のセル/行が型完全名を短縮名で保持している場合があるため。これは元々の挙動を維持する)。
internal static class ベンチマーク型一致ロジック {
    public static bool 同じ型か(string 左,string 右) {
        if(string.IsNullOrWhiteSpace(左)||string.IsNullOrWhiteSpace(右)) {
            return string.IsNullOrWhiteSpace(左)&&string.IsNullOrWhiteSpace(右);
        }

        return string.Equals(左,右,StringComparison.OrdinalIgnoreCase)
            ||string.Equals(型短名(左),右,StringComparison.OrdinalIgnoreCase)
            ||string.Equals(左,型短名(右),StringComparison.OrdinalIgnoreCase);
    }

    // 型完全名同士の完全一致のみを認める(短縮名への緩和をしない)厳密版。
    // 前回結果検索のように、双方が同じ由来(カタログ解決済み)の型完全名同士を比較する箇所で使う。
    public static bool 厳密に同じ型か(string 左,string 右) =>
        string.IsNullOrWhiteSpace(左)
            ? string.IsNullOrWhiteSpace(右)
            : string.Equals(左,右,StringComparison.OrdinalIgnoreCase);

    private static string 型短名(string 型完全名) {
        var インデックス = 型完全名.LastIndexOf('.');
        return インデックス<0||インデックス==型完全名.Length-1
            ? 型完全名
            : 型完全名[(インデックス+1)..];
    }
}

internal static class ベンチマークペア完了ロジック {
    public static bool セルが終端か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        IEnumerable<ベンチマークタブ進行セル> 選択セル一覧,
        ベンチマークタブ進行セル セル,
        string 左プロジェクト,
        string 右プロジェクト,
        double 閾値ナノ秒) {
        var 測定配列 = 測定一覧 as ベンチマーク要素測定[]??測定一覧.ToArray();
        if(セルが測定失敗済みか(測定配列,セル)) {
            return true;
        }

        if(セルが閾値到達済みか(測定配列,セル,閾値ナノ秒)) {
            return true;
        }

        return 選択ペアが閾値完了済みか(
            測定配列,
            選択セル一覧,
            セル,
            左プロジェクト,
            右プロジェクト,
            閾値ナノ秒);
    }

    public static bool 選択ペアが閾値完了済みか(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        IEnumerable<ベンチマークタブ進行セル> 選択セル一覧,
        ベンチマークタブ進行セル セル,
        string 左プロジェクト,
        string 右プロジェクト,
        double 閾値ナノ秒) {
        if(!ペアプロジェクトか(セル.プロジェクト,左プロジェクト,右プロジェクト)) {
            return false;
        }

        var 測定配列 = 測定一覧 as ベンチマーク要素測定[]??測定一覧.ToArray();
        return 選択セル一覧
            .Where(候補 => 候補.選択中か&&同じペアか(候補,セル,左プロジェクト,右プロジェクト))
            .Any(候補 => セルが閾値到達済みか(測定配列,候補,閾値ナノ秒));
    }

    public static bool セルが閾値到達済みか(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル セル,
        double 閾値ナノ秒) =>
        測定一覧.Any(結果 =>
            セルの測定か(結果,セル)
            &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
            &&結果.合計ナノ秒 is { } 合計
            &&合計>=閾値ナノ秒);

    public static bool セルが測定完了済みか(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル セル,
        int 要素数) =>
        測定一覧.Any(結果 =>
            セルの測定か(結果,セル)
            &&結果.要素数==要素数
            &&ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了));

    public static bool セルの測定か(ベンチマーク要素測定 結果,ベンチマークタブ進行セル セル) =>
        string.Equals(結果.プロジェクト,セル.プロジェクト,StringComparison.OrdinalIgnoreCase)
        &&string.Equals(結果.メソッド,セル.メソッド,StringComparison.OrdinalIgnoreCase)
        &&ベンチマーク型一致ロジック.同じ型か(結果.型完全名,セル.型完全名)
        &&同じ引数か(結果.引数キー,セル.引数キー)
        &&結果.要素数>0;

    private static bool セルが測定失敗済みか(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル セル) =>
        測定一覧.Any(結果 =>
            セルの測定か(結果,セル)
            &&ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.失敗));

    private static bool 同じペアか(
        ベンチマークタブ進行セル 候補,
        ベンチマークタブ進行セル セル,
        string 左プロジェクト,
        string 右プロジェクト) =>
        ペアプロジェクトか(候補.プロジェクト,左プロジェクト,右プロジェクト)
        &&ペアプロジェクトか(セル.プロジェクト,左プロジェクト,右プロジェクト)
        &&string.Equals(候補.メソッド,セル.メソッド,StringComparison.OrdinalIgnoreCase)
        &&同じペア識別か(候補,セル);

    private static bool 同じペア識別か(ベンチマークタブ進行セル 左,ベンチマークタブ進行セル 右) {
        if(!string.IsNullOrWhiteSpace(左.ペアキー)||!string.IsNullOrWhiteSpace(右.ペアキー)) {
            return !string.IsNullOrWhiteSpace(左.ペアキー)
                &&!string.IsNullOrWhiteSpace(右.ペアキー)
                &&string.Equals(左.ペアキー,右.ペアキー,StringComparison.OrdinalIgnoreCase);
        }

        return ベンチマーク型一致ロジック.同じ型か(左.型完全名,右.型完全名)
            &&同じ引数か(左.引数キー,右.引数キー);
    }

    private static bool ペアプロジェクトか(string プロジェクト,string 左プロジェクト,string 右プロジェクト) =>
        string.Equals(プロジェクト,左プロジェクト,StringComparison.OrdinalIgnoreCase)
        ||string.Equals(プロジェクト,右プロジェクト,StringComparison.OrdinalIgnoreCase);

    private static bool 同じ引数か(string 左,string 右) =>
        ベンチマーク引数キー一致ロジック.厳密一致か(左,右);
}

internal static class ベンチマークタブ進行ロジック {
    public static ベンチマークタブ進行 進行を計算(
        IEnumerable<ベンチマークタブ進行セル> セル一覧,
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string 左プロジェクト,
        string 右プロジェクト,
        int 最大要素数,
        double 閾値ナノ秒) {
        var 選択セル一覧 = セル一覧.Where(static セル => セル.選択中か).ToArray();
        if(選択セル一覧.Length==0) {
            return new ベンチマークタブ進行(0,0);
        }

        var 測定配列 = 測定一覧.ToArray();
        var 完了数 = 選択セル一覧.Count(セル => 終端測定ありか(
            測定配列,
            選択セル一覧,
            セル,
            左プロジェクト,
            右プロジェクト,
            閾値ナノ秒));
        return new ベンチマークタブ進行(完了数,選択セル一覧.Length);
    }

    private static bool 終端測定ありか(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        IEnumerable<ベンチマークタブ進行セル> 選択セル一覧,
        ベンチマークタブ進行セル セル,
        string 左プロジェクト,
        string 右プロジェクト,
        double 閾値ナノ秒) =>
        ベンチマークペア完了ロジック.セルが終端か(
            測定一覧,
            選択セル一覧,
            セル,
            左プロジェクト,
            右プロジェクト,
            閾値ナノ秒);
}

internal readonly record struct ベンチマーク行定義(
    string 行キー,
    string メソッド名,
    string ベンチマーク名,
    IReadOnlyDictionary<string,ベンチマークケース> プロジェクト別ケース);

internal static class ベンチマーク行グループ化ロジック {
    private static readonly IComparer<ベンチマークケース>ケース表示順比較器=
        Comparer<ベンチマークケース>.Create(static (左,右) => {
            var 型比較結果 = string.Compare(左.型完全名,右.型完全名,StringComparison.OrdinalIgnoreCase);
            if(型比較結果!=0) {
                return 型比較結果;
            }

            return string.Compare(左.アセンブリ名,右.アセンブリ名,StringComparison.OrdinalIgnoreCase);
        });

    public static IReadOnlyList<ベンチマーク行定義> 行を作成(
        IReadOnlyList<ベンチマークケース> ケース一覧,
        ベンチマークソースタブ定義 ソースタブ,
        int 要素数 = 0) {
        var 行一覧 = new List<ベンチマーク行定義>();
        var プロジェクト順 = ソースタブ.プロジェクト順;
        if(プロジェクト順.Count==0||ケース一覧.Count==0) {
            return 行一覧;
        }

        var アセンブリ名一覧 = new string[プロジェクト順.Count];
        for(var インデックス = 0;インデックス<プロジェクト順.Count;インデックス++) {
            アセンブリ名一覧[インデックス]=ソースタブ.アセンブリ名を取得(プロジェクト順[インデックス]);
        }

        var メソッドグループ一覧 = new Dictionary<string,ベンチマークメソッドケースグループ>(StringComparer.OrdinalIgnoreCase);
        foreach(var ベンチマーク in ケース一覧) {
            var プロジェクトインデックス = プロジェクトインデックスを検索(プロジェクト順,ベンチマーク.プロジェクト);
            if(プロジェクトインデックス<0) {
                continue;
            }
            if(!string.Equals(ベンチマーク.アセンブリ名,アセンブリ名一覧[プロジェクトインデックス],StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var キー=ベンチマーク.ベンチマークパス+"|"+ベンチマーク.メソッド名+"|"+ベンチマーク.引数キー;
            if(!メソッドグループ一覧.TryGetValue(キー,out var メソッドグループ)) {
                メソッドグループ=new ベンチマークメソッドケースグループ(ベンチマーク,プロジェクト順.Count);
                メソッドグループ一覧.Add(キー,メソッドグループ);
            }
            メソッドグループ.追加(プロジェクトインデックス,アセンブリ名一覧[プロジェクトインデックス],ベンチマーク);
        }

        foreach(var メソッドグループ in メソッドグループ一覧.Values.OrderBy(static グループ=>グループ.キー,StringComparer.OrdinalIgnoreCase)) {
            var 先頭ケース = メソッドグループ.先頭ケース;
            var プロジェクト別ケース = new Dictionary<string,ベンチマークケース[]>(StringComparer.OrdinalIgnoreCase);
            var 行数 = 1;
            for(var プロジェクトインデックス = 0;プロジェクトインデックス<プロジェクト順.Count;プロジェクトインデックス++) {
                var プロジェクトケース一覧 = メソッドグループ.ソート済みケース一覧を取得(プロジェクトインデックス);
                プロジェクト別ケース[プロジェクト順[プロジェクトインデックス]]=プロジェクトケース一覧;
                if(プロジェクトケース一覧.Length>行数) {
                    行数=プロジェクトケース一覧.Length;
                }
            }
            for(var インデックス = 0;インデックス<行数;インデックス++) {
                var 行ケース一覧 = new Dictionary<string,ベンチマークケース>(StringComparer.OrdinalIgnoreCase);
                for(var プロジェクトインデックス = 0;プロジェクトインデックス<プロジェクト順.Count;プロジェクトインデックス++) {
                    var プロジェクト = プロジェクト順[プロジェクトインデックス];
                    var プロジェクトケース一覧 = プロジェクト別ケース[プロジェクト];
                    if(インデックス<プロジェクトケース一覧.Length) {
                        行ケース一覧[プロジェクト]=プロジェクトケース一覧[インデックス];
                    }
                }

                行一覧.Add(new ベンチマーク行定義(
                    行キーを作成(ソースタブ,先頭ケース.メソッド名,行ケース一覧),
                    先頭ケース.メソッド名,
                    ベンチマーク名を作成(ソースタブ,先頭ケース.メソッド名,行ケース一覧,要素数),
                    行ケース一覧));
            }
        }

        return 行一覧;
    }

    public static string ケースキーを作成(string プロジェクト,string 型完全名,string メソッド名,string 引数キー = "") =>
        プロジェクト+"|"+型完全名+"|"+メソッド名+"|"+引数キー;

    private static string ベンチマーク名を作成(
        ベンチマークソースタブ定義 ソースタブ,
        string メソッド名,
        IReadOnlyDictionary<string,ベンチマークケース> プロジェクト別ケース,
        int 要素数) {
        var 型完全名 = string.Empty;
        var ベンチマークパス=string.Empty;
        foreach(var プロジェクト in ソースタブ.プロジェクト順) {
            if(プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク)
                &&!string.IsNullOrWhiteSpace(ベンチマーク.型完全名)) {
                型完全名=ベンチマーク.型完全名;
                ベンチマークパス=ベンチマーク.ベンチマークパス;
                break;
            }
        }
        return string.IsNullOrWhiteSpace(ベンチマークパス)
            ? スケーラブルベンチマーク名フォーマッター.ベンチマーク名を作成(ソースタブ,型完全名,メソッド名,要素数)
            : ベンチマークパス+"."+メソッド名;
    }

    public static string 行キーを作成(
        ベンチマークソースタブ定義 ソースタブ,
        string メソッド名,
        IReadOnlyDictionary<string,ベンチマークケース> プロジェクト別ケース) {
        var 部分一覧=new string[ソースタブ.プロジェクト順.Count];
        for(var インデックス = 0;インデックス<ソースタブ.プロジェクト順.Count;インデックス++) {
            var プロジェクト = ソースタブ.プロジェクト順[インデックス];
            部分一覧[インデックス]=プロジェクト別ケース.TryGetValue(プロジェクト,out var ベンチマーク)
                ? ケースキーを作成(ベンチマーク.プロジェクト,ベンチマーク.型完全名,ベンチマーク.メソッド名,ベンチマーク.引数キー)
                : プロジェクト+"||"+メソッド名;
        }
        return ソースタブ.ID+"|"+string.Join("||",部分一覧);
    }

    private static int プロジェクトインデックスを検索(IReadOnlyList<string> プロジェクト順,string プロジェクト) {
        for(var インデックス = 0;インデックス<プロジェクト順.Count;インデックス++) {
            if(string.Equals(プロジェクト順[インデックス],プロジェクト,StringComparison.OrdinalIgnoreCase)) {
                return インデックス;
            }
        }

        return -1;
    }

    private sealed class ベンチマークメソッドケースグループ {
        private readonly ベンチマークプロジェクトケースグループ[]プロジェクトグループ一覧;

        public ベンチマークメソッドケースグループ(ベンチマークケース 先頭ケース,int プロジェクト数) {
            this.先頭ケース=先頭ケース;
            this.キー=先頭ケース.メソッド名+"|"+先頭ケース.引数キー;
            this.プロジェクトグループ一覧=new ベンチマークプロジェクトケースグループ[プロジェクト数];
            for(var インデックス=0;インデックス<プロジェクト数;インデックス++) {
                this.プロジェクトグループ一覧[インデックス]=new ベンチマークプロジェクトケースグループ();
            }
        }

        public string キー{get;}
        public ベンチマークケース 先頭ケース{get;}

        public void 追加(int プロジェクトインデックス,string アセンブリ名,ベンチマークケース ベンチマーク) {
            this.プロジェクトグループ一覧[プロジェクトインデックス].追加(アセンブリ名,ベンチマーク);
        }

        public ベンチマークケース[]ソート済みケース一覧を取得(int プロジェクトインデックス)=>
            this.プロジェクトグループ一覧[プロジェクトインデックス].ソート済みケース一覧を取得();
    }

    private sealed class ベンチマークプロジェクトケースグループ {
        private readonly List<ベンチマークケース>アセンブリケース一覧=[];

        public void 追加(string アセンブリ名,ベンチマークケース ベンチマーク) {
            if(!string.IsNullOrWhiteSpace(アセンブリ名)
                &&string.Equals(ベンチマーク.アセンブリ名,アセンブリ名,StringComparison.OrdinalIgnoreCase)) {
                this.アセンブリケース一覧.Add(ベンチマーク);
            }
        }

        public ベンチマークケース[] ソート済みケース一覧を取得() {
            var 元一覧=this.アセンブリケース一覧;
            if(元一覧.Count==0) {
                return [];
            }

            元一覧.Sort(ケース表示順比較器);
            return 元一覧.ToArray();
        }
    }
}

internal readonly record struct ベンチマーク比較行一致ケース(string 型完全名,string 引数キー);

internal static class ベンチマーク比較行一致ロジック {
    public static bool 一致か(
        string メソッド名,
        string ベンチマーク名,
        ベンチマークソースタブ ソースタブ,
        IEnumerable<ベンチマーク比較行一致ケース> ケース一覧,
        ベンチマーク比較行 行) =>
        ベンチマーク名一致か(メソッド名,ベンチマーク名,行)
        &&ケース一覧.Any(項目 =>
            同じ引数か(項目.引数キー,行.引数キー)
            &&型一致か(ソースタブ,項目.型完全名,行.型完全名));

    private static bool 型一致か(ベンチマークソースタブ ソースタブ,string ベンチマーク型完全名,string 比較型完全名) {
        if(スケーラブルベンチマーク名フォーマッター.型付きベンチマーク名を使うか(ソースタブ)) {
            if(string.IsNullOrWhiteSpace(比較型完全名)) {
                return false;
            }
            var ベンチマーク型=スケーラブルベンチマーク名フォーマッター.型短名(ベンチマーク型完全名);
            if(!スケーラブルベンチマーク名フォーマッター.スケーラブルソースタブか(ソースタブ)) {
                var 従来メソッド1型=スケーラブルベンチマーク名フォーマッター.従来メソッド1比較型表示を作成(ベンチマーク型完全名);
                if(!string.IsNullOrWhiteSpace(従来メソッド1型)) {
                    ベンチマーク型=従来メソッド1型;
                }
            }
            return string.Equals(ベンチマーク型,比較型完全名,StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(比較型完全名)
            ||string.Equals(スケーラブルベンチマーク名フォーマッター.型短名(ベンチマーク型完全名),比較型完全名,StringComparison.OrdinalIgnoreCase);
    }

    private static bool ベンチマーク名一致か(string メソッド名,string ベンチマーク名,ベンチマーク比較行 行) =>
        string.Equals(行.メソッド名,メソッド名,StringComparison.OrdinalIgnoreCase)
        ||string.Equals(行.ベンチマーク,ベンチマーク名,StringComparison.OrdinalIgnoreCase)
        ||行.ベンチマーク.StartsWith(ベンチマーク名+"/",StringComparison.OrdinalIgnoreCase);

    private static bool 同じ引数か(string 左,string 右) =>
        ベンチマーク引数キー一致ロジック.厳密一致か(左,右);
}

internal sealed record ベンチマーク要素測定(
    string プロジェクト,
    string メソッド,
    string 型完全名,
    string 引数キー,
    int 要素数,
    string 状態,
    double? 平均ナノ秒,
    double? 合計ナノ秒 = null) {
    public ベンチマーク要素測定(string プロジェクト,string メソッド,string 型完全名,int 要素数,string 状態,double? 平均ナノ秒)
        : this(プロジェクト,メソッド,型完全名,string.Empty,要素数,状態,平均ナノ秒) {
    }

    public ベンチマーク要素測定(string プロジェクト,string メソッド,int 要素数,string 状態,double? 平均ナノ秒)
        : this(プロジェクト,メソッド,string.Empty,string.Empty,要素数,状態,平均ナノ秒) {
    }
}

internal static class ベンチマーク行完了ロジック {
    public static bool ループ回数最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル 左セル,
        ベンチマークタブ進行セル 右セル,
        double 閾値ナノ秒) {
        var 測定配列 = 測定一覧 as ベンチマーク要素測定[]??測定一覧.ToArray();
        var 左ループ回数一覧=閾値到達ループ回数一覧を取得(測定配列,左セル,閾値ナノ秒);
        return 左ループ回数一覧.Count>0
            &&最新完了ループ回数を取得(測定配列,右セル) is { } 右ループ回数
            &&左ループ回数一覧.Contains(右ループ回数);
    }

    public static bool ループ回数タブ最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        IEnumerable<ベンチマークタブ進行セル> 選択セル一覧,
        double 閾値ナノ秒) {
        var 測定配列 = 測定一覧 as ベンチマーク要素測定[]??測定一覧.ToArray();
        var セル一覧 = 選択セル一覧.Where(static セル => セル.選択中か).ToArray();
        if(セル一覧.Length==0) {
            return false;
        }

        return セル一覧
            .GroupBy(static セル => string.Join("|",セル.メソッド,セル.型完全名,セル.引数キー),StringComparer.OrdinalIgnoreCase)
            .All(グループ => {
                var 順序付きセル一覧=グループ.ToArray();
                var 閾値ループ回数一覧=閾値到達ループ回数一覧を取得(測定配列,順序付きセル一覧[0],閾値ナノ秒);
                if(閾値ループ回数一覧.Count==0) {
                    return false;
                }
                var 目標ループ回数=閾値ループ回数一覧.Max();
                return 順序付きセル一覧.Skip(1).All(セル=>最新完了ループ回数を取得(測定配列,セル)==目標ループ回数);
            });
    }

    private static global::System.Collections.Generic.HashSet<int> 閾値到達ループ回数一覧を取得(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル セル,
        double 閾値ナノ秒) =>
        測定一覧
            .Where(結果 =>
                ベンチマークペア完了ロジック.セルの測定か(結果,セル)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
                &&結果.合計ナノ秒 is { } 合計
                &&合計>=閾値ナノ秒)
            .Select(static 結果 => 結果.要素数)
            .ToHashSet();

    private static global::System.Collections.Generic.HashSet<int> 完了ループ回数一覧を取得(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル セル) =>
        測定一覧
            .Where(結果 =>
                ベンチマークペア完了ロジック.セルの測定か(結果,セル)
                &&string.Equals(結果.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)
                &&結果.合計ナノ秒 is not null)
            .Select(static 結果 => 結果.要素数)
            .ToHashSet();

    private static int? 最新完了ループ回数を取得(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル セル) {
        var ループ回数一覧=完了ループ回数一覧を取得(測定一覧,セル);
        return ループ回数一覧.Count==0 ? null : ループ回数一覧.Max();
    }

    public static bool 最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 左プロジェクト,
        string 右プロジェクト,
        int 最大要素数,
        double 閾値ナノ秒) =>
        最終完了か(測定一覧,メソッド,string.Empty,左プロジェクト,右プロジェクト,最大要素数,閾値ナノ秒);

    public static bool 最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 型完全名,
        string 左プロジェクト,
        string 右プロジェクト,
        int 最大要素数,
        double 閾値ナノ秒) {
        return 最終完了か(測定一覧,メソッド,型完全名,string.Empty,左プロジェクト,右プロジェクト,最大要素数,閾値ナノ秒);
    }

    public static bool 最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 型完全名,
        string 引数キー,
        string 左プロジェクト,
        string 右プロジェクト,
        int 最大要素数,
        double 閾値ナノ秒) {
        var ペアキー = 合成ペアキーを作成(メソッド,型完全名,引数キー);
        return 最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル(メソッド,左プロジェクト,型完全名,引数キー,true,ペアキー),
            new ベンチマークタブ進行セル(メソッド,右プロジェクト,型完全名,引数キー,true,ペアキー),
            最大要素数,
            閾値ナノ秒);
    }

    public static bool 最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル 左セル,
        ベンチマークタブ進行セル 右セル,
        int 最大要素数,
        double 閾値ナノ秒) {
        var 測定配列 = 測定一覧 as ベンチマーク要素測定[]??測定一覧.ToArray();
        var 選択セル一覧 = new[] {
            左セル with { 選択中か = true },
            右セル with { 選択中か = true },
        };
        if(選択セル一覧.Any(セル => ベンチマークペア完了ロジック.選択ペアが閾値完了済みか(
            測定配列,
            選択セル一覧,
            セル,
            左セル.プロジェクト,
            右セル.プロジェクト,
            閾値ナノ秒))) {
            return true;
        }

        return 最大要素数完了済みか(測定配列,選択セル一覧,最大要素数);
    }

    public static bool タブ最終完了か(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        IEnumerable<ベンチマークタブ進行セル> 選択セル一覧,
        int 最大要素数,
        double 閾値ナノ秒) {
        var 測定配列 = 測定一覧 as ベンチマーク要素測定[]??測定一覧.ToArray();
        var セル一覧 = 選択セル一覧.Where(static セル => セル.選択中か).ToArray();
        if(セル一覧.Length==0) {
            return false;
        }

        return セル一覧.All(セル =>
            ベンチマークペア完了ロジック.セルが測定完了済みか(測定配列,セル,最大要素数)
            ||ベンチマークペア完了ロジック.セルが終端か(
                測定配列,
                セル一覧,
                セル,
                セル.プロジェクト,
                ペアプロジェクトを解決(セル一覧,セル),
                閾値ナノ秒));
    }

    private static bool 最大要素数完了済みか(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        IReadOnlyList<ベンチマークタブ進行セル> 選択セル一覧,
        int 最大要素数) {
        if(最大要素数<=0) {
            return false;
        }

        return 選択セル一覧.All(セル =>
            ベンチマークペア完了ロジック.セルが測定完了済みか(測定一覧,セル,最大要素数));
    }

    private static string 合成ペアキーを作成(string メソッド,string 型完全名,string 引数キー) =>
        string.Join("|",メソッド,型完全名,引数キー);

    private static string ペアプロジェクトを解決(IEnumerable<ベンチマークタブ進行セル> セル一覧,ベンチマークタブ進行セル セル) {
        foreach(var 候補 in セル一覧) {
            if(!string.Equals(候補.プロジェクト,セル.プロジェクト,StringComparison.OrdinalIgnoreCase)
                &&string.Equals(候補.ペアキー,セル.ペアキー,StringComparison.OrdinalIgnoreCase)) {
                return 候補.プロジェクト;
            }
        }

        return セル.プロジェクト;
    }
}

internal static class ベンチマークスキップ表示ロジック {
    public static int? ペア停止要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string プロジェクト,
        int 選択要素数,
        double 閾値ナノ秒) =>
        ペア停止要素数を検索(測定一覧,メソッド,string.Empty,プロジェクト,選択要素数,閾値ナノ秒);

    public static int? ペア停止要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 型完全名,
        string プロジェクト,
        int 選択要素数,
        double 閾値ナノ秒) {
        return ペア停止要素数を検索(測定一覧,メソッド,型完全名,string.Empty,プロジェクト,選択要素数,閾値ナノ秒);
    }

    public static int? ペア停止要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 型完全名,
        string 引数キー,
        string プロジェクト,
        int 選択要素数,
        double 閾値ナノ秒) {
        var ペアプロジェクト一覧 = ベンチマーク比較ペア.ペアプロジェクトを取得(プロジェクト);
        if(ペアプロジェクト一覧 is null) {
            return null;
        }

        var ペアキー = 合成ペアキーを作成(メソッド,型完全名,引数キー);
        return ペア停止要素数を検索(
            測定一覧,
            new ベンチマークタブ進行セル(メソッド,ペアプロジェクト一覧[0],型完全名,引数キー,true,ペアキー),
            new ベンチマークタブ進行セル(メソッド,ペアプロジェクト一覧[1],型完全名,引数キー,true,ペアキー),
            選択要素数,
            閾値ナノ秒);
    }

    public static int? ペア停止要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル 左セル,
        ベンチマークタブ進行セル 右セル,
        int 選択要素数,
        double 閾値ナノ秒) {
        var セル一覧 = new[] {
            左セル with { 選択中か = true },
            右セル with { 選択中か = true },
        };
        return 測定一覧
            .Where(結果 => 結果.要素数<選択要素数
                &&ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了)
                &&結果.平均ナノ秒 is not null
                &&セル一覧.Any(セル => ベンチマークペア完了ロジック.セルの測定か(結果,セル)))
            .GroupBy(static 結果 => 結果.要素数)
            .OrderBy(static グループ => グループ.Key)
            .Where(グループ => セル一覧.All(セル => ベンチマークペア完了ロジック.セルが測定完了済みか(グループ,セル,グループ.Key)))
            .Where(グループ => セル一覧.Any(セル => ベンチマークペア完了ロジック.セルが閾値到達済みか(グループ,セル,閾値ナノ秒)))
            .Select(グループ => (int?)グループ.Key)
            .FirstOrDefault();
    }

    public static int? 直前の完了ペア要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 左プロジェクト,
        string 右プロジェクト,
        int 選択要素数) =>
        直前の完了ペア要素数を検索(測定一覧,メソッド,string.Empty,左プロジェクト,右プロジェクト,選択要素数);

    public static int? 直前の完了ペア要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 型完全名,
        string 左プロジェクト,
        string 右プロジェクト,
        int 選択要素数) =>
        直前の完了ペア要素数を検索(測定一覧,メソッド,型完全名,string.Empty,左プロジェクト,右プロジェクト,選択要素数);

    public static int? 直前の完了ペア要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        string メソッド,
        string 型完全名,
        string 引数キー,
        string 左プロジェクト,
        string 右プロジェクト,
        int 選択要素数) {
        var ペアキー = 合成ペアキーを作成(メソッド,型完全名,引数キー);
        return 直前の完了ペア要素数を検索(
            測定一覧,
            new ベンチマークタブ進行セル(メソッド,左プロジェクト,型完全名,引数キー,true,ペアキー),
            new ベンチマークタブ進行セル(メソッド,右プロジェクト,型完全名,引数キー,true,ペアキー),
            選択要素数);
    }

    public static int? 直前の完了ペア要素数を検索(
        IEnumerable<ベンチマーク要素測定> 測定一覧,
        ベンチマークタブ進行セル 左セル,
        ベンチマークタブ進行セル 右セル,
        int 選択要素数) {
        var セル一覧 = new[] {
            左セル with { 選択中か = true },
            右セル with { 選択中か = true },
        };
        return 測定一覧
            .Where(結果 => 結果.要素数>0
                &&結果.要素数<選択要素数
                &&ジョブ状態名.保存値と一致するか(結果.状態,ジョブ状態.完了)
                &&結果.平均ナノ秒 is not null
                &&セル一覧.Any(セル => ベンチマークペア完了ロジック.セルの測定か(結果,セル)))
            .GroupBy(static 結果 => 結果.要素数)
            .OrderByDescending(static グループ => グループ.Key)
            .Where(グループ => セル一覧.All(セル => ベンチマークペア完了ロジック.セルが測定完了済みか(グループ,セル,グループ.Key)))
            .Select(グループ => (int?)グループ.Key)
            .FirstOrDefault();
    }

    private static string 合成ペアキーを作成(string メソッド,string 型完全名,string 引数キー) =>
        string.Join("|",メソッド,型完全名,引数キー);
}

internal enum ベンチマークツールバーボタン役割 {
    実行,
    無限実行,
    停止,
    一時停止,
    DB保存,
    新規グリッド,
}

internal readonly record struct ベンチマークツールバー状態入力(
    bool 更新中か,
    bool ベンチマーク実行中か,
    bool ベンチマーク一時停止中か,
    bool 実行ボタンを使用可能か,
    bool ベンチマークを停止可能か,
    bool ベンチマークを一時停止可能か,
    bool DB保存可能か,
    bool 新規グリッド作成可能か) {
    public ベンチマークツールバー状態入力(
        bool 更新中か,
        bool ベンチマーク実行中か,
        bool ベンチマーク一時停止中か,
        bool 実行ボタンを使用可能か,
        bool ベンチマークを停止可能か,
        bool ベンチマークを一時停止可能か)
        : this(
            更新中か,
            ベンチマーク実行中か,
            ベンチマーク一時停止中か,
            実行ボタンを使用可能か,
            ベンチマークを停止可能か,
            ベンチマークを一時停止可能か,
            false,
            false) {
    }
}

internal readonly record struct ベンチマークツールバーボタン状態(
    bool 有効か,
    bool 主要操作を呼ぶか,
    bool 無効表示をするか,
    bool 点滅するか);

internal static class ベンチマークツールバー状態ロジック {
    public static ベンチマークツールバーボタン状態 状態を作成(ベンチマークツールバーボタン役割 役割,ベンチマークツールバー状態入力 入力) {
        if(入力.ベンチマーク一時停止中か) {
            return 役割 switch {
                ベンチマークツールバーボタン役割.実行 => new ベンチマークツールバーボタン状態(
                    入力.実行ボタンを使用可能か,
                    入力.実行ボタンを使用可能か,
                    !入力.実行ボタンを使用可能か,
                    入力.実行ボタンを使用可能か),
                ベンチマークツールバーボタン役割.無限実行 => new ベンチマークツールバーボタン状態(
                    入力.実行ボタンを使用可能か,
                    入力.実行ボタンを使用可能か,
                    !入力.実行ボタンを使用可能か,
                    入力.実行ボタンを使用可能か),
                ベンチマークツールバーボタン役割.停止 => new ベンチマークツールバーボタン状態(
                    入力.ベンチマークを停止可能か,
                    入力.ベンチマークを停止可能か,
                    !入力.ベンチマークを停止可能か,
                    入力.ベンチマークを停止可能か),
                ベンチマークツールバーボタン役割.一時停止 => new ベンチマークツールバーボタン状態(true,false,true,false),
                ベンチマークツールバーボタン役割.DB保存 => 直接操作状態を作成(入力.DB保存可能か),
                ベンチマークツールバーボタン役割.新規グリッド => 直接操作状態を作成(入力.新規グリッド作成可能か),
                _ => new ベンチマークツールバーボタン状態(false,false,true,false),
            };
        }

        return 役割 switch {
            ベンチマークツールバーボタン役割.実行 => 直接操作状態を作成(入力.実行ボタンを使用可能か),
            ベンチマークツールバーボタン役割.無限実行 => 直接操作状態を作成(入力.実行ボタンを使用可能か),
            ベンチマークツールバーボタン役割.停止 => 直接操作状態を作成(入力.ベンチマークを停止可能か),
            ベンチマークツールバーボタン役割.一時停止 => 直接操作状態を作成(入力.ベンチマークを一時停止可能か),
            ベンチマークツールバーボタン役割.DB保存 => 直接操作状態を作成(入力.DB保存可能か),
            ベンチマークツールバーボタン役割.新規グリッド => 直接操作状態を作成(入力.新規グリッド作成可能か),
            _ => new ベンチマークツールバーボタン状態(false,false,true,false),
        };
    }

    public static bool 有効か(ベンチマークツールバーボタン役割 役割,ベンチマークツールバー状態入力 入力) =>
        状態を作成(役割,入力).有効か;

    private static ベンチマークツールバーボタン状態 直接操作状態を作成(bool 有効か)=>
        new(有効か,有効か,!有効か,false);
}

internal static class ベンチマークツールバー一時停止ガードロジック {
    public static bool 判断ダイアログが必要か(ベンチマークツールバーボタン役割 役割,bool ベンチマーク一時停止中か)=>
        ベンチマーク一時停止中か&&役割==ベンチマークツールバーボタン役割.一時停止;
}

internal enum ベンチマーク終了操作 {
    キャンセル,
    停止して閉じる,
}

internal static class ベンチマーク終了ダイアログロジック {
    public static bool ダイアログが必要か(
        bool ベンチマーク実行中か,
        bool ベンチマーク一時停止中か,
        bool 未保存結果ありか) =>
        ベンチマーク実行中か||ベンチマーク一時停止中か||未保存結果ありか;

    public static bool 閉じる前に未保存結果を破棄するか(
        bool ベンチマーク実行中か,
        bool ベンチマーク一時停止中か,
        bool 未保存結果ありか,
        ベンチマーク終了操作 操作) =>
        操作==ベンチマーク終了操作.停止して閉じる
        &&!ベンチマーク実行中か
        &&!ベンチマーク一時停止中か
        &&未保存結果ありか;

    public static IReadOnlyList<(string 表示文,ベンチマーク終了操作 操作,ベンチマークツールバーボタン役割 役割)>選択肢を作成(
        bool ベンチマーク実行中か,
        bool ベンチマーク一時停止中か,
        bool 未保存結果ありか) {
        if(ベンチマーク一時停止中か) {
            return [
                ("停止して閉じる",ベンチマーク終了操作.停止して閉じる,ベンチマークツールバーボタン役割.停止),
                ("キャンセル",ベンチマーク終了操作.キャンセル,ベンチマークツールバーボタン役割.一時停止),
            ];
        }

        if(ベンチマーク実行中か) {
            return [
                ("停止して終了",ベンチマーク終了操作.停止して閉じる,ベンチマークツールバーボタン役割.停止),
                ("キャンセル",ベンチマーク終了操作.キャンセル,ベンチマークツールバーボタン役割.一時停止),
            ];
        }

        if(未保存結果ありか) {
            return [
                ("破棄して閉じる",ベンチマーク終了操作.停止して閉じる,ベンチマークツールバーボタン役割.停止),
                ("キャンセル",ベンチマーク終了操作.キャンセル,ベンチマークツールバーボタン役割.一時停止),
            ];
        }

        return [
            ("キャンセル",ベンチマーク終了操作.キャンセル,ベンチマークツールバーボタン役割.一時停止),
        ];
    }
}

internal readonly record struct ベンチマーク行UI選択セル(bool ケースありか,bool UI選択中か);

internal static class ベンチマーク行UI選択ロジック {
    public static bool 行全体を選択中か(IEnumerable<ベンチマーク行UI選択セル> セル一覧) {
        var 対象一覧 = セル一覧.Where(static セル => セル.ケースありか).ToArray();
        return 対象一覧.Length>0&&対象一覧.All(static セル => セル.UI選択中か);
    }
}

internal static class グリッドチェック切替ロジック {
    public static bool? 目標チェック状態を解決(IEnumerable<bool?>状態一覧) {
        var 値一覧=状態一覧.ToArray();
        if(値一覧.Length==0) {
            return null;
        }

        return 値一覧.All(static 値 => 値 is true) ? false : true;
    }
}

internal sealed record CPU表区画定義(string タイトル,CPUコア種別 コア種別,IReadOnlyList<CPU表行定義> 行一覧) {
    public int 列数 => this.行一覧.Count==0 ? 0 : this.行一覧.Max(static 行 => 行.論理プロセッサインデックス一覧.Count);
}

internal sealed record CPU表行定義(string ラベル,IReadOnlyList<int> 論理プロセッサインデックス一覧);

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class CPU選択表ロジック {
    public static IReadOnlyList<CPU表区画定義> 区画一覧を作成(IReadOnlyList<CPUコア情報> CPUコア一覧) {
        return [
            ..区画を作成("Pコア",CPUコア種別.高性能,CPUコア一覧),
            ..区画を作成("Eコア",CPUコア種別.高効率,CPUコア一覧),
            ..区画を作成("不明なコア",CPUコア種別.不明,CPUコア一覧),
        ];
    }

    public static void 一括選択を適用(IReadOnlyList<CPUコア情報> CPUコア一覧,IEnumerable<int> 対象論理プロセッサインデックス一覧,bool 選択するか) {
        var 対象一覧 = 対象論理プロセッサインデックス一覧
            .Distinct()
            .Order()
            .ToHashSet();
        if(対象一覧.Count==0) {
            return;
        }

        if(!選択するか) {
            var 対象外選択ありか=CPUコア一覧.Any(CPU=>CPU.選択中か&&!対象一覧.Contains(CPU.論理プロセッサインデックス));
            if(!対象外選択ありか&&CPUコア一覧.Any(static CPU=>CPU.選択中か)) {
                var 維持対象 = CPUコア一覧
                    .Where(CPU => 対象一覧.Contains(CPU.論理プロセッサインデックス)&&CPU.選択中か)
                    .OrderBy(static CPU => CPU.論理プロセッサインデックス)
                    .FirstOrDefault()
                    ??CPUコア一覧
                        .Where(CPU => 対象一覧.Contains(CPU.論理プロセッサインデックス))
                        .OrderBy(static CPU => CPU.論理プロセッサインデックス)
                        .FirstOrDefault();
                foreach(var CPU in CPUコア一覧.Where(CPU => 対象一覧.Contains(CPU.論理プロセッサインデックス))) {
                    CPU.選択中か=ReferenceEquals(CPU,維持対象);
                }
                return;
            }
        }

        foreach(var CPU in CPUコア一覧.Where(CPU => 対象一覧.Contains(CPU.論理プロセッサインデックス))) {
            CPU.選択中か=選択するか;
        }
    }

    private static IEnumerable<CPU表区画定義>区画を作成(string タイトル,CPUコア種別 種別,IReadOnlyList<CPUコア情報> CPUコア一覧) {
        var 行一覧 = CPUコア一覧
            .Where(CPU => CPU.コア種別==種別)
            .GroupBy(static CPU => CPU.物理コアインデックス)
            .OrderBy(static グループ => グループ.Min(static CPU => CPU.論理プロセッサインデックス))
            .Select(static グループ => グループ
                .OrderBy(static CPU => CPU.論理プロセッサインデックス)
                .Select(static CPU => CPU.論理プロセッサインデックス)
                .ToArray())
            .Where(static 行 => 行.Length>0)
            .Select(static 行 => new CPU表行定義(範囲ラベルを作成(行),行))
            .ToArray();
        if(行一覧.Length>0) {
            yield return new CPU表区画定義(タイトル,種別,行一覧);
        }
    }

    private static string 範囲ラベルを作成(IReadOnlyList<int>インデックス一覧) {
        var 最小=インデックス一覧.Min();
        var 最大=インデックス一覧.Max();
        return 最小==最大 ? 最小.ToString(System.Globalization.CultureInfo.InvariantCulture) : 最小+"-"+最大;
    }
}

public static class ベンチマーク結果CPU表示ロジック {
    public static int? 割り当て論理CPUを解決(int? 割り当て論理CPU,IEnumerable<int> 使用可能論理プロセッサ一覧) {
        if(割り当て論理CPU is not { } 値) {
            return null;
        }

        return 使用可能論理プロセッサ一覧.Contains(値) ? 値 : null;
    }
}

internal static class ベンチマーク実行日時表示ロジック {
    public static DateTimeOffset 実行中日時を解決(DateTimeOffset? 開始日時,DateTimeOffset 観測日時) =>
        開始日時??観測日時;
}

internal readonly record struct UIソート値(string? 文字列,double? 数値,bool 欠測か) {
    public static readonly UIソート値 欠測 = new(null,null,true);
    public static UIソート値 文字列から作成(string 文字列) => new(文字列,null,string.IsNullOrWhiteSpace(文字列));
    public static UIソート値 数値から作成(double? 値)=>値 is { } 数値&&double.IsFinite(数値) ? new(null,数値,false) : 欠測;

    public static int 比較(UIソート値 左,UIソート値 右,bool 降順か) {
        if(左.欠測か&&右.欠測か) {
            return 0;
        }

        if(左.欠測か) {
            return 1;
        }

        if(右.欠測か) {
            return -1;
        }

        var 結果 = 左.数値 is { } 左数値&&右.数値 is { } 右数値
            ? 左数値.CompareTo(右数値)
            : string.Compare(左.文字列,右.文字列,StringComparison.OrdinalIgnoreCase);
        return 降順か ? -結果 : 結果;
    }
}

