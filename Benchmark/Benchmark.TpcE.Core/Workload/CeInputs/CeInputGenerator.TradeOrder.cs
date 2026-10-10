namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Trade-Order の入力を作る(仮。取引の実装で置き換える)。</summary>
    public TradeOrderInput TradeOrder(ref Rng rng){
        throw new NotImplementedException("Trade-Order input generation is not implemented yet.");
    }
}
