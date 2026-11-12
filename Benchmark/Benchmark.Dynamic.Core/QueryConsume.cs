namespace Benchmark.Dynamic.Core;

/// <summary>生成コードの <see cref="QueryEmissionStyle.Consume"/> が使う、要素を溜めるだけの受け手。</summary>
public sealed class QueryConsumer<T>{
    public List<T> Items{get;}=[];

    public void Consume(T item){
        this.Items.Add(item);
    }
}

/// <summary>
/// <c>列.Consume(consumer)</c> の通常実装。FLinq の生成器はこの文の形を見つけると consumer.Consume(item) のループへ展開する。
/// 展開されない経路(System.Linq の経路)では、ここの実装がそのまま走る。
/// </summary>
public static class QueryConsumeExtensions{
    public static void Consume<T>(this IEnumerable<T> source,QueryConsumer<T> consumer){
        foreach(var item in source){
            consumer.Consume(item);
        }
    }
}
