namespace Benchmark.Dynamic.Tests;

public sealed class 動的UI更新ChannelTests{
    [Fact,Trait("Category","Unit")]
    public async Task 結果を欠落させず進捗は最新値へ集約する(){
        var rows=new List<global::Benchmark.Dynamic.UI.動的結果行>();
        var progress=new List<global::Benchmark.Dynamic.UI.動的実行進捗>();
        await using var channel=new global::Benchmark.Dynamic.UI.動的UI更新Channel(
            rows.Add,
            progress.Add,
            TimeSpan.FromMilliseconds(5),
            TimeSpan.FromSeconds(1),
            action=>{
                action();
                return Task.CompletedTask;
            });
        for(var index=0;index<3;index++){
            channel.結果を追加(new global::Benchmark.Dynamic.UI.動的結果行{
                実行ID="run",
                QueryID=$"q{index}",
                対象=Benchmark.Dynamic.Core.SourceTarget.SystemArray,
                QueryIndex=index,
                短縮式="Count",
                N=1,
                深度=1,
            });
            channel.進捗を更新(new global::Benchmark.Dynamic.UI.動的実行進捗(
                index,
                3,
                Benchmark.Dynamic.Core.SourceTarget.SystemArray,
                "実行中",
                index+1,
                0,
                0,
                0,
                0));
        }

        await channel.完了Async();

        Assert.Equal([0,1,2],rows.Select(static row=>row.QueryIndex));
        var latest=Assert.Single(progress);
        Assert.Equal(3,latest.完了数);
        Assert.Equal(2,latest.QueryIndex);
    }
}
