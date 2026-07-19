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
    public const string CPUコア行チェック = "CPU.コア行チェック";

    public static string CPUグリッド(string コア種別) =>
        "CPU.グリッド."+コア種別;

    public static string ソースタブ(string ソースタブ名) =>
        "ベンチマーク.ソースタブ."+ソースタブ名;


    public static string ソースタブ進捗リング(string ソースタブ名) =>
        "ベンチマーク.ソースタブ進捗リング."+ソースタブ名;

    public static string ソースタブ進捗完了(string ソースタブ名) =>
        "ベンチマーク.ソースタブ進捗完了."+ソースタブ名;

    public static string ソースタブ実行スピナー(string ソースタブ名) =>
        "ベンチマーク.ソースタブ実行スピナー."+ソースタブ名;

    public static string ソースタブ実行スピナーホスト(string ソースタブ名) =>
        "ベンチマーク.ソースタブ実行スピナーホスト."+ソースタブ名;

    public static string グリッド(string ソースタブ名) =>
        "ベンチマーク.グリッド."+ソースタブ名;
}
