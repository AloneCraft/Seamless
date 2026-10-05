using Benchmark.Dynamic.Core;
namespace Benchmark.Dynamic.Tests;
/// <summary>動的実行の源の種類ごとの能力(件数の費用など)の仕様。</summary>
public sealed class DynamicSourceModelTests{
    [Fact,Trait("Category","Unit")]public void GetCountCost(){
        //Tsavorite はシングル用(Create())が件数をデータ本体で管理するので、配列・List・Persisted と同じ O(1)。件数を持たないマルチ用は動的実行の源に使わない
        foreach(var target in new[]{SourceTarget.SystemArray,SourceTarget.SystemList,SourceTarget.SystemPersisted,SourceTarget.SeamlessPersisted,SourceTarget.SystemTsavorite,SourceTarget.SeamlessTsavorite}){
            Assert.Equal(CountCost.Constant,target.GetCountCost());
        }
        //件数を知る方法が無い源は不明のまま
        Assert.Equal(CountCost.Unknown,SourceTarget.SystemIEnumerable.GetCountCost());
        Assert.Equal(CountCost.Unknown,SourceTarget.SystemRange.GetCountCost());
    }
}
