namespace Sample.NetShop.Core;

/// <summary>決まった種から大量の注文を作る。全件をメモリに持たず、列挙するたびに同じ列を作り直す。</summary>
public sealed class 注文の生成器(long 件数,int 顧客数,int 種):IEnumerable<注文イベント>{
    public long 件数{get;}=件数;
    public int 顧客数{get;}=顧客数;

    public IEnumerator<注文イベント>GetEnumerator(){
        var 乱数=new Random(種);
        var 商品一覧=Shop.初期データ.商品一覧;
        for(long 注文ID=1;注文ID<=this.件数;注文ID++){
            var 商品=商品一覧[乱数.Next(商品一覧.Count)];
            var 数量=乱数.Next(1,6);
            // 顧客の買い物量に偏りを付け、上位の顧客がはっきり出るようにする。
            var 顧客ID=(int)(Math.Pow(乱数.NextDouble(),2)*this.顧客数)+1;
            yield return new 注文イベント(注文ID,顧客ID,商品.商品ID,数量,(long)商品.単価*数量,乱数.Next(1,13));
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
