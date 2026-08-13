using Benchmark.Aot.Protocol;

namespace Test.BenchmarkWorker;

public sealed class AOT応答検証テスト {
    [Fact,Trait("Category","Unit")]
    public void 完全なNativeAOT応答だけを受理する() {
        var (応答,ケース一覧)=正常系を作成する();
        AOT応答検証.検証(応答,"request","toolchain",3,ケース一覧);

        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答 with { 契約バージョン=999 },"request","toolchain",3,ケース一覧));
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答 with { NativeAOTか=false },"request","toolchain",3,ケース一覧));
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答 with { アフィニティ指定成功か=false },"request","toolchain",3,ケース一覧));
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答 with { 論理CPU=4 },"request","toolchain",3,ケース一覧));
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答,"different","toolchain",3,ケース一覧));
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答,"request","different",3,ケース一覧));
    }

    [Fact,Trait("Category","Unit")]
    public void 欠落重複未要求ケースを拒否する() {
        var (応答,ケース一覧)=正常系を作成する();
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答 with { 結果一覧=[] },"request","toolchain",3,ケース一覧));

        var ケース2=new AOTケース要求("Right","Other","",[],4096,1);
        var 二件要求=new[] { ケース一覧[0],ケース2 };
        var 重複応答=応答 with { 結果一覧=[応答.結果一覧[0],応答.結果一覧[0]] };
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(重複応答,"request","toolchain",3,二件要求));

        var 未要求結果=応答.結果一覧[0] with { メソッド名="Other" };
        Assert.Throws<InvalidDataException>(()=>AOT応答検証.検証(応答 with { 結果一覧=[未要求結果] },"request","toolchain",3,ケース一覧));
    }

    [Fact,Trait("Category","Unit")]
    public void null統計非有限値不正割当空ダイジェストを拒否する() {
        var (応答,ケース一覧)=正常系を作成する();
        var 結果=応答.結果一覧[0];
        Assert.Throws<InvalidDataException>(()=>検証する(応答,ケース一覧,結果 with { 平均ナノ秒=null }));
        Assert.Throws<InvalidDataException>(()=>検証する(応答,ケース一覧,結果 with { 誤差ナノ秒=double.NaN }));
        Assert.Throws<InvalidDataException>(()=>検証する(応答,ケース一覧,結果 with { 標準偏差ナノ秒=double.PositiveInfinity }));
        Assert.Throws<InvalidDataException>(()=>検証する(応答,ケース一覧,結果 with { 割り当てバイト数=-1 }));
        Assert.Throws<InvalidDataException>(()=>検証する(応答,ケース一覧,結果 with { 結果ダイジェスト="" }));
    }

    private static void 検証する(AOTワーカー応答 応答,IReadOnlyList<AOTケース要求> ケース一覧,AOTケース結果 結果) =>
        AOT応答検証.検証(応答 with { 結果一覧=[結果] },"request","toolchain",3,ケース一覧);

    private static (AOTワーカー応答 応答,IReadOnlyList<AOTケース要求> ケース一覧) 正常系を作成する() {
        var ケース一覧=new[] { new AOTケース要求("Left","Sum","",[],4096,0) };
        var 結果一覧=new[] { new AOTケース結果("Sum","","Completed",10,1,0.5,32,"digest") };
        var 応答=new AOTワーカー応答(
            global::Benchmark.Aot.Protocol.AOT契約.現在バージョン,
            "Completed",
            true,
            3,
            true,
            "toolchain",
            "request",
            結果一覧,
            "G:/artifacts",
            "G:/artifacts/aot.log");
        return (応答,ケース一覧);
    }
}
