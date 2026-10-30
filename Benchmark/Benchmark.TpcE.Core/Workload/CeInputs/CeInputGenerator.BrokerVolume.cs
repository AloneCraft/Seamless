namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>
    /// Broker-Volume の入力を作る(3.3.1.1)。互いに異なるブローカー名を 20〜40 件(ブローカーがそれより少なければ全員)と、部門名を 1 つ、どちらも一様に選ぶ。
    /// </summary>
    public BrokerVolumeInput BrokerVolume(ref Rng rng){
        var brokers=this.Context.Scale.Brokers;
        var count=(int)Math.Min(rng.Range(TpcESpec.MinBrokerListLength,TpcESpec.MaxBrokerListLength),brokers);
        // ブローカー ID(1..Brokers)の並びの先頭 count 個を部分的な Fisher-Yates で選ぶ(重複なし・一様)。
        var ids=new long[brokers];
        for(var index=0;index<ids.Length;index++){
            ids[index]=index+1;
        }
        var names=new string[count];
        for(var index=0;index<count;index++){
            var pick=rng.Range(index,ids.Length-1);
            (ids[index],ids[pick])=(ids[pick],ids[index]);
            names[index]=this.Model.BrokerName(ids[index]);
        }
        var sectorName=TpcEModel.SectorName(rng.Range(0,TpcEModel.SectorCount-1));
        return new BrokerVolumeInput{BrokerList=names,SectorName=sectorName};
    }
}
