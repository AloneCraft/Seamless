using Sample.NetShop.Core;
using Sample.NetShop.Core.Shop;
using 対象=Sample.NetShop.Core.Shop.商店DB;

namespace Sample.NetShop.Tests.Shop;

/// <summary>商店DBの仕様。すべてメモリ最小のストレージ方式で確かめる。</summary>
public sealed class 商店DB{
    private static 対象 最小で開く(テスト用フォルダ フォルダ)=>対象.開く(フォルダ.下("shop"),資源設定.最小);

    [Fact,Trait("Category","Unit")]public void 開く(){
        using var フォルダ=new テスト用フォルダ();
        long 最初の注文ID;
        {
            //閉じて開き直すと、確定した注文と在庫が残る
            using(var 商店=最小で開く(フォルダ)){
                Assert.True(商店.初期データを登録する());
                var 結果=商店.注文確定(1,[new 注文行(1,2)],new DateTime(2026,1,1)).Result;
                Assert.True(結果.成功か);
                最初の注文ID=結果.注文ID;
            }
            using(var 商店=最小で開く(フォルダ)){
                Assert.Equal(1,商店.注文.LongCount);
                Assert.Equal(初期データ.商品一覧[0].在庫数-2,商店.商品[new(1)].在庫数);
                Assert.Equal(初期データ.顧客一覧.Count,商店.顧客.LongCount);
            }
        }
        {
            //開き直した後の注文IDは、保存済みの続きになる
            using var 商店=最小で開く(フォルダ);
            var 結果=商店.注文確定(2,[new 注文行(2,1)],new DateTime(2026,1,2)).Result;
            Assert.True(結果.成功か);
            Assert.Equal(最初の注文ID+1,結果.注文ID);
        }
    }

    [Fact,Trait("Category","Unit")]public void 在庫切れ商品(){
        using var フォルダ=new テスト用フォルダ();
        using var 商店=最小で開く(フォルダ);
        商店.初期データを登録する();
        {
            //在庫がある間は空
            Assert.Empty(商店.在庫切れ商品());
        }
        {
            //在庫を使い切った商品だけが商品ID順に出る
            var 抹茶=初期データ.商品一覧[2];
            var 急須=初期データ.商品一覧[5];
            Assert.True(商店.注文確定(1,[new 注文行(急須.商品ID,急須.在庫数),new 注文行(抹茶.商品ID,抹茶.在庫数)],DateTime.Now).Result.成功か);
            Assert.Equal([抹茶.商品ID,急須.商品ID],商店.在庫切れ商品().Select(行=>行.商品ID).ToArray());
            Assert.Equal("在庫切れ商品",商店.在庫切れ商品().Name);
        }
    }

    [Fact,Trait("Category","Unit")]public void 顧客の注文履歴(){
        using var フォルダ=new テスト用フォルダ();
        using var 商店=最小で開く(フォルダ);
        商店.初期データを登録する();
        var 一件目=商店.注文確定(3,[new 注文行(1,1)],DateTime.Now).Result;
        商店.注文確定(4,[new 注文行(1,1)],DateTime.Now);
        var 二件目=商店.注文確定(3,[new 注文行(2,1)],DateTime.Now).Result;
        {
            //指定した顧客の注文だけが注文ID順に出る
            Assert.Equal([一件目.注文ID,二件目.注文ID],商店.顧客の注文履歴(3).Select(行=>行.注文ID).ToArray());
        }
        {
            //注文の無い顧客は空
            Assert.Empty(商店.顧客の注文履歴(9));
        }
    }

    [Fact,Trait("Category","Unit")]public void 売上合計(){
        using var フォルダ=new テスト用フォルダ();
        using var 商店=最小で開く(フォルダ);
        商店.初期データを登録する();
        {
            //注文が無ければ0
            Assert.Equal(0,商店.売上合計().Value);
        }
        {
            //確定した注文の合計金額の和
            var 緑茶=初期データ.商品一覧[0];
            var 羊羹=初期データ.商品一覧[4];
            商店.注文確定(1,[new 注文行(緑茶.商品ID,2),new 注文行(羊羹.商品ID,1)],DateTime.Now);
            Assert.Equal(緑茶.単価*2+羊羹.単価,商店.売上合計().Value);
        }
    }

    [Fact,Trait("Category","Unit")]public void 注文確定(){
        using var フォルダ=new テスト用フォルダ();
        using var 商店=最小で開く(フォルダ);
        商店.初期データを登録する();
        var 緑茶=初期データ.商品一覧[0];
        var 抹茶=初期データ.商品一覧[2];
        {
            //成功すると在庫が減り、注文と明細が登録される
            var 結果=商店.注文確定(1,[new 注文行(緑茶.商品ID,3),new 注文行(抹茶.商品ID,1)],DateTime.Now).Result;
            Assert.True(結果.成功か);
            Assert.Equal(緑茶.単価*3+抹茶.単価,結果.合計金額);
            Assert.Equal(緑茶.在庫数-3,商店.商品[new(緑茶.商品ID)].在庫数);
            Assert.Equal(抹茶.在庫数-1,商店.商品[new(抹茶.商品ID)].在庫数);
            Assert.Equal(1,商店.注文.LongCount);
            Assert.Equal(2,商店.注文明細.LongCount);
        }
        {
            //2行目で在庫が足りないと、1行目で減らした在庫も含めて全部取り消される
            var 結果=商店.注文確定(2,[new 注文行(緑茶.商品ID,1),new 注文行(抹茶.商品ID,999)],DateTime.Now).Result;
            Assert.False(結果.成功か);
            Assert.Contains("在庫が足りません",結果.理由);
            Assert.Equal(緑茶.在庫数-3,商店.商品[new(緑茶.商品ID)].在庫数);
            Assert.Equal(1,商店.注文.LongCount);
            Assert.Equal(2,商店.注文明細.LongCount);
        }
        {
            //登録されていない顧客・商品は失敗し、何も変わらない
            Assert.False(商店.注文確定(99,[new 注文行(緑茶.商品ID,1)],DateTime.Now).Result.成功か);
            Assert.False(商店.注文確定(1,[new 注文行(99,1)],DateTime.Now).Result.成功か);
            Assert.Equal(1,商店.注文.LongCount);
            Assert.Equal(緑茶.在庫数-3,商店.商品[new(緑茶.商品ID)].在庫数);
        }
    }

    [Fact,Trait("Category","Unit")]public void 入荷(){
        using var フォルダ=new テスト用フォルダ();
        using var 商店=最小で開く(フォルダ);
        商店.初期データを登録する();
        {
            //在庫が指定数だけ増える
            var 煎餅=初期データ.商品一覧[3];
            商店.入荷(煎餅.商品ID,5);
            Assert.Equal(煎餅.在庫数+5,商店.商品[new(煎餅.商品ID)].在庫数);
        }
    }

    [Fact,Trait("Category","Unit")]public void 初期データを登録する(){
        using var フォルダ=new テスト用フォルダ();
        using var 商店=最小で開く(フォルダ);
        {
            //空なら登録して true
            Assert.True(商店.初期データを登録する());
            Assert.Equal(初期データ.商品一覧.Count,商店.商品.LongCount);
            Assert.Equal(初期データ.顧客一覧.Count,商店.顧客.LongCount);
        }
        {
            //登録済みなら何もせず false
            Assert.False(商店.初期データを登録する());
            Assert.Equal(初期データ.商品一覧.Count,商店.商品.LongCount);
        }
    }
}
