namespace Benchmark.Shared;

// 繝吶Φ繝√・繝ｼ繧ｯ繧偵ユ繧ｹ繝医→縺励※邁｡譏楢ｨ域ｸｬ縺吶ｋ髫帙・繝代Λ繝｡繝ｼ繧ｿ繝ｼ縲・// 繝・せ繝亥・縺ｮ譌｢螳壹・1蝗槫ｮ溯｡後ら腸蠅・､画焚縺ｧ荳頑嶌縺阪〒縺阪ｋ縲・static class BenchmarkParameters{
    // 蜷・・繝ｳ繝√・繝ｼ繧ｯ繝｡繧ｽ繝・ラ縺ｮ險域ｸｬ螳溯｡悟屓謨ｰ縲よ里螳・蝗槭・
    public static int RunCount{get;}=Read("SEAMLESS_BENCHMARK_RUN_COUNT",1,1);
    // 險域ｸｬ蜑阪・繧ｦ繧ｩ繝ｼ繝繧｢繝・・蝗樊焚縲よ里螳・蝗槭・
    public static int WarmupCount{get;}=Read("SEAMLESS_BENCHMARK_WARMUP_COUNT",0,0);

    static int Read(string name,int fallback,int min){
        var raw=Environment.GetEnvironmentVariable(name);
        if(int.TryParse(raw,out var value)&&value>=min){
            return value;
        }
        return fallback;
    }
}
