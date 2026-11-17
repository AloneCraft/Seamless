namespace Benchmark.TpcC.Tests;
public sealed class InterceptionCoverage{
    private const string Transactions="Shared.TpcC.Transactions";
    [Fact,Trait("Category","Unit")]public void FindMissing(){
        {
            //FLinq の組み合わせでは、取引の問い合わせ(*Queries の public メソッド)が全部インターセプタに置き換わっている
            foreach(var variant in TestDatabases.Variants){
                if(!variant.Name.EndsWith("-flinq",StringComparison.Ordinal)){
                    continue;
                }
                var missing=global::Benchmark.Tpc.Core.InterceptionCoverage.FindMissing(variant.GetType().Assembly,Transactions,out _);
                Assert.True(missing.Count==0,$"{variant.Name}: not intercepted: {string.Join(", ",missing)}");
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void CountInterceptors(){
        {
            //標準 LINQ の組み合わせにはインターセプタが無く(FLinq の設定が漏れていないことの確認)、FLinq の組み合わせには有る
            foreach(var variant in TestDatabases.Variants){
                var count=global::Benchmark.Tpc.Core.InterceptionCoverage.CountInterceptors(variant.GetType().Assembly);
                if(variant.Name.EndsWith("-linq",StringComparison.Ordinal)){
                    Assert.Equal(0,count);
                }else{
                    Assert.True(count>0,$"{variant.Name}: no interceptors");
                }
            }
        }
    }
}
