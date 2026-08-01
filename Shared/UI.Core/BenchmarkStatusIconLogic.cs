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
