using Seamless.Collections;

namespace Seamless.Execution;

/// <summary>明示した入出力条件を区別し、別の方式で測ったプロファイルを適用しない。</summary>
public static class ProfileIO{
    public static string Describe(StorageTuning? tuning){
        if(tuning is not StorageTuning value){return "BackendDefault";}
        return $"storage-v1:{value.OptimizationMode}:{value.AccessPattern}:{value.BufferingMode}:{value.BufferSizeStrategy}:{value.BufferSize}";
    }
}
