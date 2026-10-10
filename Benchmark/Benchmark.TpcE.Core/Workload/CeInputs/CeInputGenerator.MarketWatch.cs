namespace Benchmark.TpcE.Core;
public sealed partial class CeInputGenerator{
    /// <summary>Market-Watch の入力を作る(仮。取引の実装で置き換える)。</summary>
    public MarketWatchInput MarketWatch(ref Rng rng){
        throw new NotImplementedException("Market-Watch input generation is not implemented yet.");
    }
}
