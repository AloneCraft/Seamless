namespace Shared.TpcE.Transactions;
/// <summary>Broker-Volume(3.3.1)のフレーム 1。手順と問い合わせは既存の <see cref="BrokerVolumeFrames"/> を使い、ここは構造体との変換だけ。</summary>
internal sealed class BrokerVolumeHarness{
    /// <summary>EGen の max_broker_list_len。</summary>
    private const int MaximumBrokers=40;
    private readonly BrokerVolumeFrames frames=new();
    public void Frame1(TpcEDatabase db,BrokerVolumeTxnInput input,BrokerVolumeFrame1Output output){
        var brokers=new List<string>();
        for(var index=0;index<MaximumBrokers;index++){
            var name=input.GetBrokerList(index);
            if(name.Length==0){
                break;
            }
            brokers.Add(name);
        }
        var result=this.frames.Run(db,new BrokerVolumeInput{BrokerList=brokers.ToArray(),SectorName=input.SectorName});
        var count=Math.Min(result.Entries.Length,MaximumBrokers);
        output.ListLen=result.ListLength;
        for(var index=0;index<count;index++){
            output.SetBrokerName(index,result.Entries[index].BrokerName);
            output.SetVolume(index,(double)result.Entries[index].Volume);
        }
    }
}
