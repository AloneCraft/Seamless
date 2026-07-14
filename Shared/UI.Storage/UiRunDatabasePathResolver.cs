namespace Seamless.UI.Storage;

public static class UI実行DBパスリゾルバー{
    public const string ベンチマークDBファイル名="Benchmark.db";
    public const string スケーラブルDBファイル名="Benchmark.Scalable.db";
    public const string パラメータスイープDBファイル名="Benchmark.ParameterSweep.db";

    public static string 解決(UI実行アプリ種別 アプリ種別,string? ソリューションルート = null){
        var ルート=!string.IsNullOrWhiteSpace(ソリューションルート)
            ? ソリューションルート
            : ソリューションルートを検索()??Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Seamless");
        var ファイル名=アプリ種別 switch{
            UI実行アプリ種別.ベンチマーク=>ベンチマークDBファイル名,
            UI実行アプリ種別.スケーラブルベンチマーク=>スケーラブルDBファイル名,
            UI実行アプリ種別.パラメータスイープ=>パラメータスイープDBファイル名,
            _=>throw new ArgumentOutOfRangeException(nameof(アプリ種別),アプリ種別,null),
        };
        return Path.Combine(ルート,"Work","UI",ファイル名);
    }

    private static string? ソリューションルートを検索(){
        var ディレクトリ=new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null){
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))){
                return ディレクトリ.FullName;
            }

            ディレクトリ=ディレクトリ.Parent;
        }

        return null;
    }
}
