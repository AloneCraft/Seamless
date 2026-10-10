namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Trade-Lookup の入力を作る(仮。取引の実装で置き換える)。</summary>
    public TradeLookupInput TradeLookup(ref Rng rng){
        throw new NotImplementedException("Trade-Lookup input generation is not implemented yet.");
    }
}
