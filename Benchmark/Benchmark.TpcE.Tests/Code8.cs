using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Tests;
public sealed class Code8{
    [Fact,Trait("Category","Unit")]public void From(){
        {
            //文字列へ戻すと元に戻る
            Assert.Equal("CMPT",Core.Code8.From("CMPT").ToString());
            Assert.Equal("",Core.Code8.From("").ToString());
            Assert.Equal("NASDAQ",Core.Code8.From("NASDAQ").ToString());
        }
        {
            //9 文字以上と非 ASCII は受け付けない
            Assert.Throws<ArgumentException>(()=>Core.Code8.From("123456789"));
            Assert.Throws<ArgumentException>(()=>Core.Code8.From("あ"));
        }
    }
    [Fact,Trait("Category","Unit")]public void CompareTo(){
        {
            //数値の大小が文字列の Ordinal 比較と一致する(短いほうが前)
            string[] texts=["A","AB","ABC","B","US1","US2","CN1","a"];
            foreach(var left in texts){
                foreach(var right in texts){
                    Assert.Equal(Math.Sign(string.CompareOrdinal(left,right)),Math.Sign(Core.Code8.From(left).CompareTo(Core.Code8.From(right))));
                }
            }
        }
        {
            //Code16 と Code24 も同じ
            Assert.True(Code16.From("AAAA-PA").CompareTo(Code16.From("AAAB"))<0);
            Assert.True(Code24.From("C000000000001").CompareTo(Code24.From("C000000000002"))<0);
        }
    }
}
