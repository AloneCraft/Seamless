using Benchmark.Aot.Protocol;

namespace Test.BenchmarkWorker;

public sealed class AOT契約 {
    [Fact,Trait("Category","Unit")]
    public void タイムアウトを計算() {
        Assert.Equal(TimeSpan.FromMinutes(30),global::Benchmark.Aot.Protocol.AOT契約.タイムアウトを計算(1));
        Assert.Equal(TimeSpan.FromMinutes(40),global::Benchmark.Aot.Protocol.AOT契約.タイムアウトを計算(30));
        Assert.Equal(TimeSpan.FromHours(4),global::Benchmark.Aot.Protocol.AOT契約.タイムアウトを計算(999));
        Assert.Throws<ArgumentOutOfRangeException>(()=>global::Benchmark.Aot.Protocol.AOT契約.タイムアウトを計算(0));
    }

    [Fact,Trait("Category","Unit")]
    public void 検証() {
        var 要求=要求を作成();
        AOT要求検証.検証(要求);
        Assert.Throws<InvalidDataException>(()=>AOT要求検証.検証(要求 with { 契約バージョン=999 }));
        Assert.Throws<InvalidDataException>(()=>AOT要求検証.検証(要求 with { ケース一覧=[..要求.ケース一覧,..要求.ケース一覧] }));
        Assert.Throws<InvalidDataException>(()=>AOT要求検証.検証(要求 with { 設定=new AOTジョブ設定(1,1,1,100) }));
    }

    [Fact,Trait("Category","Unit")]
    public void JSON往復() {
        var 要求=要求を作成();
        var JSON=global::System.Text.Json.JsonSerializer.Serialize(要求,global::Benchmark.Aot.Protocol.AOT契約.JSON設定);
        var 復元=global::System.Text.Json.JsonSerializer.Deserialize<AOTワーカー要求>(JSON,global::Benchmark.Aot.Protocol.AOT契約.JSON設定);
        Assert.NotNull(復元);
        Assert.Equal(要求.契約バージョン,復元.契約バージョン);
        Assert.Equal(要求.実行ID,復元.実行ID);
        Assert.Equal(要求.設定,復元.設定);
        Assert.Equal(要求.ケース一覧.Single().メソッド名,復元.ケース一覧.Single().メソッド名);
        Assert.Equal(要求.ケース一覧.Single().引数一覧.Single(),復元.ケース一覧.Single().引数一覧.Single());
    }

    private static AOTワーカー要求 要求を作成() => new(
        global::Benchmark.Aot.Protocol.AOT契約.現在バージョン,
        "run-1",
        "Benchmarks",
        "G:/benchmarks/Benchmarks.dll",
        "Benchmarks.Type",
        [new("左","Method","Count=4096",[new("Count","System.Int32","4096")],4096,0)],
        0,
        "G:/artifacts/aot",
        new(),
        "toolchain",
        "request");
}
