using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
public sealed class InterceptionCoverage{
    [Fact,Trait("Category","Unit")]public void FindMissing(){
        {
            //FLinq の組み合わせでは、全部の問い合わせが置き換わっている(止めたものは許可リスト)
            foreach(var variant in VariantCatalog.All){
                if(!variant.Name.EndsWith("-flinq",StringComparison.Ordinal)){
                    continue;
                }
                var missing=Core.InterceptionCoverage.FindMissing(variant.GetType().Assembly,out _);
                Assert.True(missing.Count==0,$"{variant.Name}: not intercepted: {string.Join(", ",missing)}");
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void CountInterceptors(){
        {
            //標準 LINQ の組み合わせにはインターセプタが無い(FLinq の設定が漏れていないことの確認)
            foreach(var variant in VariantCatalog.All){
                if(variant.Name.EndsWith("-linq",StringComparison.Ordinal)){
                    Assert.Equal(0,Core.InterceptionCoverage.CountInterceptors(variant.GetType().Assembly));
                }
            }
        }
    }
}
