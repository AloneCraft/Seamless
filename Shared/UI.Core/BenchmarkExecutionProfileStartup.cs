namespace UI;

public static class ベンチマーク起動実行プロファイル {
    public const string 環境変数名="SEAMLESS_BENCHMARK_EXECUTION_PROFILE";

    public static ベンチマーク実行プロファイル 解決(string? 環境変数値) {
        if(string.IsNullOrWhiteSpace(環境変数値)) {
            return ベンチマーク実行プロファイル.高速;
        }
        if(Enum.TryParse<ベンチマーク実行プロファイル>(環境変数値,ignoreCase:true,out var プロファイル)
            &&Enum.IsDefined(プロファイル)) {
            return プロファイル;
        }
        throw new InvalidDataException(
            $"環境変数{環境変数名}の値'{環境変数値}'は不正です。有効値: "
            +string.Join(",",Enum.GetNames<ベンチマーク実行プロファイル>()));
    }
}
