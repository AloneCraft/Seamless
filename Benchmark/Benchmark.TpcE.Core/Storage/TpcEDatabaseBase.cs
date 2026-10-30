using Store=Seamless.Execution.Store;
namespace Benchmark.TpcE.Core;
/// <summary>
/// TPC-E の DB。取り消し記録・表と索引の一覧・索引の作り方は <see cref="TpcDatabaseBase"/>、表と索引の宣言は TpcEDatabaseBase.Tables.cs。
/// </summary>
public abstract partial class TpcEDatabaseBase:TpcDatabaseBase{
    protected TpcEDatabaseBase(Store store,TpcEScale scale):base(store){
        this.Scale=scale;
    }
    public TpcEScale Scale{get;}
}
/// <summary>時刻順に並ぶ ID(取引・保有の索引の要素)。同時刻は ID 順。</summary>
public readonly record struct TimedId(long Dts,long Id):IComparable<TimedId>{
    public int CompareTo(TimedId other){
        var dts=this.Dts.CompareTo(other.Dts);
        if(dts!=0){
            return dts;
        }
        return this.Id.CompareTo(other.Id);
    }
}
/// <summary>会社→競合の索引の要素。</summary>
public readonly record struct CompetitorEntry(long CompetitorId,Code8 IndustryId):IComparable<CompetitorEntry>{
    public int CompareTo(CompetitorEntry other){
        var competitor=this.CompetitorId.CompareTo(other.CompetitorId);
        if(competitor!=0){
            return competitor;
        }
        return this.IndustryId.CompareTo(other.IndustryId);
    }
}
/// <summary>会社名の 128 ビットのハッシュ(会社名→会社の索引の持ち主)。衝突に備え、引いた行の名前を必ず照合する。</summary>
public readonly record struct NameHash(ulong High,ulong Low){
    public static NameHash Of(string name){
        var hash=System.IO.Hashing.XxHash128.HashToUInt128(System.Text.Encoding.UTF8.GetBytes(name));
        return new((ulong)(hash>>64),(ulong)hash);
    }
}
