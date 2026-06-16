using global::Tsavorite.Linq;

namespace Benchmark.Tsavorite.Linq.Enumerable.Intercept;

public class SingleOrDefault_predicate():TsavoriteBase(4_690_000){
    [BenchFact,Benchmark(Baseline=true)]public void Tsavorite()=>/* 隕∫ｴ謨ｰ縺ｫ萓晏ｭ倥＠縺ｪ縺・・ｽE・ｽ・ｽE・ｽ謨ｰ(蜈磯ｭ隕∫ｴ=1)縺ｧ荳諢丈ｸ閾ｴ縺輔○繧九Ｄount 縺ｯ instance 縺ｪ縺ｮ縺ｧ繝ｩ繝繝蜀・・ｽE・ｽ・ｽE・ｽ縺ｯ菴ｿ縺医↑縺・・ｽE・ｽ・ｽE・ｽE */ this.consumer.Consume(this.src(1).SingleOrDefault(x=>x==1));

    [BenchFact,Benchmark]public void FLinq()=>this.Tsavorite();


    [Fact,Trait("Category","Unit"),TestPriority(100)]
    public void Results_match()=>this.AssertResults(this.Tsavorite,this.FLinq,this.FLinqNoOptimized);

    [global::Tsavorite.SourceGenerator.LinqIntercept(false,global::Tsavorite.SourceGenerator.LinqInstrumentation.Counter)]
    private void FLinqNoOptimized()=>this.Tsavorite();
}
