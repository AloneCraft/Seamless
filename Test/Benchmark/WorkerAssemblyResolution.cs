using System.Reflection;

namespace Test.Benchmark;

// Worker\Program.cs の ベンチマーク型を解決 は Assembly.Load(new AssemblyName(アセンブリ名)) でアセンブリを解決する。
// Worker.csproj に ProjectReference が無いアセンブリは Worker.exe 起動時にロードできず、全ベンチマークが失敗表示になる。
// このテストは Test.csproj が Worker.csproj を参照していることを利用し、同じロード失敗を検出する。
public sealed class ワーカーアセンブリ解決 {
    [Fact]
    public void 標準タブの全アセンブリをリフレクションでロードできる() {
        var アセンブリ名一覧 = global::UI.ベンチマークソースタブ一覧.標準全て
            .SelectMany(static タブ => new[] { タブ.左アセンブリ名,タブ.中央アセンブリ名,タブ.右アセンブリ名 })
            .Where(static 名前 => !string.IsNullOrWhiteSpace(名前))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(アセンブリ名一覧);

        foreach(var アセンブリ名 in アセンブリ名一覧) {
            try {
                var アセンブリ = Assembly.Load(new AssemblyName(アセンブリ名));
                Assert.NotNull(アセンブリ);
            } catch(Exception 例外) {
                Assert.Fail("アセンブリ解決に失敗しました: "+アセンブリ名+" ("+例外.GetType().Name+": "+例外.Message+")");
            }
        }
    }
}
