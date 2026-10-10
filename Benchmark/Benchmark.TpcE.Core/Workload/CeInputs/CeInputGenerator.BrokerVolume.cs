namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Broker-Volume の入力を作る(仮。取引の実装で置き換える)。</summary>
    public BrokerVolumeInput BrokerVolume(ref Rng rng){
        throw new NotImplementedException("Broker-Volume input generation is not implemented yet.");
    }
}
