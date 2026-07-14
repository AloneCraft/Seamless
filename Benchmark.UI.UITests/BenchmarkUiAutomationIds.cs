namespace Benchmark.UI.UITests;

internal static class ベンチマークUI自動化ID {
    public const string ソースタブ表示 = "ベンチマーク.ソースタブ表示";
    public const string 全タブ選択チェックホスト = "ベンチマーク.全タブ選択チェックホスト";
    public const string 全タブ選択チェック = "ベンチマーク.全タブ選択チェック";
    public const string 全選択チェックホスト = "ベンチマーク.全選択チェックホスト";
    public const string 全選択チェック = "ベンチマーク.全選択チェック";
    public const string 行チェック = "ベンチマーク.行チェック";
    public const string 行チェックホスト = "ベンチマーク.行チェックホスト";
    public const string CPUセルチェック = "CPU.セルチェック";

    public static string CPUグリッド(string コア種別) =>
        "CPU.グリッド."+コア種別;

    public static string ソースタブ(string ソースタブ名) =>
        "ベンチマーク.ソースタブ."+ソースタブ名;

    public static string グリッド(string ソースタブ名) =>
        "ベンチマーク.グリッド."+ソースタブ名;
}
