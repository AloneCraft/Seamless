using System.Text.Json;
using UI;

namespace Benchmark.Headless.Tests;

public sealed class PerformanceGateManifest{
    [Fact,Trait("Category","Integration")]
    public void 実マニフェストはHeadless実カタログの11ペア22ケースへ一意に解決する(){
        var パス=Path.Combine(AppContext.BaseDirectory,"Manifests","run249-performance-gate.json");
        var マニフェスト=JsonSerializer.Deserialize<ベンチマーク性能ゲートマニフェスト>(File.ReadAllText(パス))
            ??throw new InvalidDataException("実性能ゲートマニフェストを読み込めませんでした。");

        var 解決一覧=ベンチマーク性能ゲートロジック.検証して解決(
            マニフェスト,ベンチマークカタログ.読み込む());
        var ケースキー一覧=解決一覧.SelectMany(static 対象=>new[]{
            対象.タブ.ID+"|"+対象.FLinq.プロジェクト+"|"+対象.FLinq.アセンブリ名+"|"+対象.FLinq.型完全名+"|"+対象.FLinq.メソッド名+"|"+対象.FLinq.引数キー,
            対象.タブ.ID+"|"+対象.基準.プロジェクト+"|"+対象.基準.アセンブリ名+"|"+対象.基準.型完全名+"|"+対象.基準.メソッド名+"|"+対象.基準.引数キー,
        }).ToArray();

        Assert.Equal(11,解決一覧.Count);
        Assert.Equal(22,ケースキー一覧.Length);
        Assert.Equal(22,ケースキー一覧.Distinct(StringComparer.Ordinal).Count());
    }
}
