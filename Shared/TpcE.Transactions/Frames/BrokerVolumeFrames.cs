namespace Shared.TpcE.Transactions;
/// <summary>Broker-Volume(TPC-E 3.3.1)の手順。LINQ は書かない(問い合わせは BrokerVolumeQueries に書く)。仮の実装。</summary>
internal sealed class BrokerVolumeFrames{
    public BrokerVolumeOutput Run(TpcEDatabase db,BrokerVolumeInput input){
        throw new NotImplementedException("Broker-Volume is not implemented yet.");
    }
}
