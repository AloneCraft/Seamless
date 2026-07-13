using UI;

namespace Test.Benchmark;

public sealed class BenchmarkResultDisplaySelectionLogic{
    private static readonly BenchmarkSourceTabDefinition SourceTab=BenchmarkSourceTabs.Get(BenchmarkSourceTab.SystemArray);
    private const string TypeFullName="Benchmark.System.Linq.Enumerable.FLinq.Sample";

    [Fact]
    public void IsResultSelected(){
        var result = Result(SourceTab.LeftProject,"Aggregate",TypeFullName,"Count=1");
        {
            //選択中の一致セルだけ表示対象にする
            Assert.True(global::UI.BenchmarkResultDisplaySelectionLogic.IsResultSelected(
                result,
                [
                    new BenchmarkResultSelectionCell(SourceTab.LeftProject,"Aggregate","Sample","Count=1",true,true),
                    new BenchmarkResultSelectionCell(SourceTab.RightProject,"Aggregate",TypeFullName,"Count=1",true,false),
                ]));
        }
        {
            //完了済み結果でも現在未選択なら表示対象にしない
            Assert.False(global::UI.BenchmarkResultDisplaySelectionLogic.IsResultSelected(
                result,
                [
                    new BenchmarkResultSelectionCell(SourceTab.LeftProject,"Aggregate","Sample","Count=1",true,false),
                ]));
        }
        {
            //別引数の結果を混ぜない
            Assert.False(global::UI.BenchmarkResultDisplaySelectionLogic.IsResultSelected(
                result,
                [
                    new BenchmarkResultSelectionCell(SourceTab.LeftProject,"Aggregate","Sample","Count=2",true,true),
                ]));
        }
    }

    [Fact]
    public void IsResultSelected_FollowsCurrentCellSelection(){
        var result = Result(SourceTab.LeftProject,"Aggregate",TypeFullName,"Count=1");
        BenchmarkResultSelectionCell[] selected = [
            new(SourceTab.LeftProject,"Aggregate","Sample","Count=1",true,true),
        ];
        BenchmarkResultSelectionCell[] unselected = [
            new(SourceTab.LeftProject,"Aggregate","Sample","Count=1",true,false),
        ];

        Assert.True(global::UI.BenchmarkResultDisplaySelectionLogic.IsResultSelected(result,selected));
        Assert.False(global::UI.BenchmarkResultDisplaySelectionLogic.IsResultSelected(result,unselected));
        Assert.True(global::UI.BenchmarkResultDisplaySelectionLogic.IsResultSelected(result,selected));
    }

    private static BenchmarkResultItem Result(string project,string method,string typeFullName,string argumentKey) =>
        new(new BenchmarkJob{
            Project=project,
            AssemblyName=SourceTab.GetAssemblyName(project),
            MethodName=method,
            TypeFullName=typeFullName,
            ArgumentKey=argumentKey,
            SourceTab=BenchmarkSourceTab.SystemArray,
            ElementCount=4096,
            LoopCount=1,
            Status=JobStatus.Completed,
            MeanNanoseconds=100,
            AllocatedBytes=10,
            ExitCode=0,
        });
}
