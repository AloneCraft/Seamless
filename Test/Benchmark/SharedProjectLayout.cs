namespace Test.Benchmark;

public sealed class 共有プロジェクト配置 {
    [Fact]
    public void 仕様確認01() {
        var ルート = リポジトリルートを検索();
        var ソリューションファイル = File.ReadAllText(Path.Combine(ルート,"Seamless.slnx"));
        var パス一覧 = new[] {
            "Shared/UI.Core/UI.Core.shproj",
            "Shared/UI.Core/UI.Core.projitems",
            "Shared/Collections/Collections.shproj",
            "Shared/Collections/Collections.projitems",
            "Benchmark/Shared/UI/UI.shproj",
            "Benchmark/Shared/UI/UI.projitems",
        };

        foreach(var パス in パス一覧) {
            Assert.True(File.Exists(Path.Combine(ルート,パス.Replace('/',Path.DirectorySeparatorChar))),パス);
            Assert.Contains(パス,ソリューションファイル,StringComparison.Ordinal);
        }

        Assert.Empty(Directory.GetFiles(Path.Combine(ルート,"Shared"),"*.shproj"));
        Assert.Empty(Directory.GetFiles(Path.Combine(ルート,"Shared"),"*.projitems"));
        Assert.Empty(Directory.GetFiles(Path.Combine(ルート,"Benchmark","Shared"),"*.shproj"));
        Assert.Empty(Directory.GetFiles(Path.Combine(ルート,"Benchmark","Shared"),"*.projitems"));
    }

    private static string リポジトリルートを検索() {
        var ディレクトリ = new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null&&!File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))) {
            ディレクトリ=ディレクトリ.Parent;
        }

        return ディレクトリ?.FullName??throw new InvalidOperationException("リポジトリルートが見つかりませんでした。");
    }
}
