namespace Seamless.Database.Gateway.Native;

/// <summary>退役した実行中DLLも容量に含めるキャッシュの観測値。</summary>
public sealed record NativeLambdaCacheStatistics(int PreparedQueries,int RetiredQueries,int ExecutionLeases,int Compilations,int Evictions){
    public int LiveQueries=>this.PreparedQueries+this.RetiredQueries;
}
