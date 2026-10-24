namespace Benchmark.Tpc.Core;
/// <summary>
/// Persisted の表の生存件数を保存と一緒に記録する子資源。Persisted の Count は削除済みのキーも数えるので、表が自分で数えた値を保存・復元する。
/// 1 つの Store に複数の DB(区画)を置くときは、<paramref name="name"/> を分けて名前の重複を避ける。
/// </summary>
public sealed class LiveCountComponent(List<ILiveCounter> counters,string name="~persisted-live-counts"):Seamless.Execution.IStoreComponent{
    public string Name=>name;
    public string Save(){
        var values=new System.Collections.Generic.Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var counter in counters){
            values.Add(counter.CounterName,counter.LiveCount);
        }
        return System.Text.Json.JsonSerializer.Serialize(values);
    }
    public void Flush(){
    }
    public void Load(string state){
        var values=System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string,long>>(state)??[];
        foreach(var counter in counters){
            if(values.TryGetValue(counter.CounterName,out var count)){
                counter.LiveCount=count;
            }else{
                counter.LiveCount=0;
            }
        }
    }
    public void Dispose(){
    }
}
