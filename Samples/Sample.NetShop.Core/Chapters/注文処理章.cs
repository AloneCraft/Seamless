using Sample.NetShop.Core.Shop;

namespace Sample.NetShop.Core;

/// <summary>4章: ストアドプロシージャと取引。在庫が足りない注文は、途中まで減らした在庫ごと取り消される。</summary>
public sealed class 注文処理章:物語の章{
    public override string 題名=>"注文を受ける";
    public override string 使う機能=>"StoredProcedure / BeginTransaction / Commit";
    public override string 説明=>
        "お客さんから次々に注文が届きます。注文確定は1つの取引(トランザクション)の中で在庫を減らし、注文を登録します。\n"+
        "2行目で在庫が足りなければ Commit しないので、1行目で減らした在庫も含めて全部なかったことになります。\n"+
        "在庫が切れた商品は、ときどき入荷します。";
    public override string コード=>
        """
        var 結果=商店.注文確定(顧客ID,[new(商品ID,数量),...],日時).Result;

        // 注文確定の中身
        using var 取引=this.BeginTransaction();
        foreach(var 行 in 行一覧){
            if(商品.在庫数<行.数量) return 失敗;          // Commit せずに抜ける → 全部取り消し
            this.商品[キー]=在庫を減らした商品;
            this.注文明細.Add(明細);
        }
        this.注文.Add(注文);
        取引.Commit();
        """;

    public override async Task 実行Async(章の文脈 文脈){
        var 商店=文脈.商店;
        // 3章を飛ばして始めても動くよう、空の店なら先に商品と顧客を登録する(登録済みなら何もしない)。
        if(商店.初期データを登録する()){
            文脈.ログ("この章から始めたので、先に商品と顧客を登録しました。");
        }
        商店DB章.在庫を描く(文脈,商店);
        文脈.指標("売上合計",表示.金額(商店.売上合計().Value));
        await 文脈.間を置くAsync();

        // 最初の1件は、わざと2行目で在庫が足りない注文にして取り消しを見せる。
        文脈.コードを指す("商店.注文確定");
        var 先頭=初期データ.商品一覧[0];
        var 在庫の前=商店.商品[new(先頭.商品ID)].在庫数;
        var 取り消し=商店.注文確定(1,[new(先頭.商品ID,1),new(初期データ.商品一覧[1].商品ID,999)],DateTime.Now).Result;
        文脈.コードを指す("return 失敗");
        文脈.ログ($"✖ 取り消し: {取り消し.理由}",ログ種別.失敗);
        var 在庫の後=商店.商品[new(先頭.商品ID)].在庫数;
        文脈.ログ($"1行目の{先頭.名前}の在庫は {在庫の前} → {在庫の後}。減らした分も元に戻っています。");
        await 文脈.間を置くAsync(2);

        var 注文数=25;
        var 成功数=0;
        for(var 回=0;回<注文数;回++){
            文脈.取消.ThrowIfCancellationRequested();
            var 顧客=初期データ.顧客一覧[文脈.乱数.Next(初期データ.顧客一覧.Count)];
            var 行一覧=this.注文を考える(文脈.乱数);
            文脈.コードを指す("商店.注文確定");
            var 結果=商店.注文確定(顧客.顧客ID,行一覧,DateTime.Now).Result;
            if(結果.成功か){
                成功数++;
                文脈.コードを指す("取引.Commit()");
                文脈.ログ($"✔ 注文#{結果.注文ID} {顧客.名前}さん {表示.金額(結果.合計金額)}",ログ種別.成功);
            }else{
                文脈.コードを指す("return 失敗");
                文脈.ログ($"✖ {顧客.名前}さんの注文を取り消し: {結果.理由}",ログ種別.失敗);
            }
            this.ときどき入荷する(文脈,商店);
            商店DB章.在庫を描く(文脈,商店);
            文脈.指標("売上合計",表示.金額(商店.売上合計().Value));
            文脈.指標("この章の注文",$"{成功数} 件成功 / {回+1} 件");
            await 文脈.間を置くAsync(0.6);
        }
    }

    private List<注文行>注文を考える(Random 乱数){
        var 行一覧=new List<注文行>();
        var 行数=乱数.Next(1,4);
        var 使った商品=new HashSet<int>();
        while(行一覧.Count<行数){
            var 商品=初期データ.商品一覧[乱数.Next(初期データ.商品一覧.Count)];
            if(!使った商品.Add(商品.商品ID)){
                continue;
            }
            行一覧.Add(new 注文行(商品.商品ID,乱数.Next(1,4)));
        }
        return 行一覧;
    }

    private void ときどき入荷する(章の文脈 文脈,商店DB 商店){
        foreach(var 登録 in 初期データ.商品一覧){
            var 商品=商店.商品[new(登録.商品ID)];
            if(商品.在庫数<=1&&文脈.乱数.Next(3)==0){
                商店.入荷(商品.商品ID,登録.在庫数);
                文脈.ログ($"📦 {商品.名前}を{登録.在庫数}個入荷しました。");
            }
        }
    }
}
