using Seamless.Collections.Tsavorite;

namespace Seamless.TestKit;

public static class TsavoriteBenchmarkBudget{
    public static long MemoryBytes(long count,long index,long page,long logMemory){
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        using var device=new global::Tsavorite.core.NullDevice();
        // 設定したログ全体に加え、overflow pool・管理配列・alignmentを実ストアと同じ会計で覆う。
        return TsavoriteMemoryAccounting.Required(index,TsavoriteMemoryAccounting.OverflowBytes(count,index),
            logMemory,page,device.SectorSize,logMemory/page);
    }
}
