using Seamless.Collections.Persisted;
using Seamless.Collections.Tsavorite;
using Seamless.Execution;
// 旧 API にも同じ名前の Store があるため、共通ストアを明示する。
using Store=Seamless.Execution.Store;

namespace Sample.NetShop.Core;

/// <summary>入荷の記録。参照を含まない構造体なので、Persisted のリストへそのまま書ける。</summary>
public readonly record struct 入荷記録(int 商品ID,int 数量);

/// <summary>1章: 共通ストアから、メモリに収まらなくてもよいコレクションを作る。</summary>
public sealed class 開店準備章:物語の章{
    public override string 題名=>"開店準備";
    public override string 使う機能=>"Store / Tsavorite / Persisted";
    public override string 説明=>
        "開店の前に、商品名の辞書・来店客の名簿・入荷の記録を用意します。\n"+
        "どれも共通ストア(Store)から作るコレクションで、メモリの上限を超えた分は自動でディスクへ退避します。\n"+
        "来店客は同じ人が何度も来ますが、HashSet なので重複は1人として数えます。";
    public override string コード=>
        """
        using var ストア=Store.CreateTemporary(new(){MemoryBytes=上限},作業フォルダ);
        using var 商品名=ストア.CreateTsavorite<int,string>("商品名");
        using(var 辞書=商品名.Create()){ 辞書.Add(1,"緑茶"); }
        using var 来店客=ストア.CreateTsavoriteHashSet<int>("来店客",小さいログ);
        using(var 名簿=来店客.Create()){ 名簿.Add(顧客ID); }   // 重複は入らない
        var 統計=来店客.GetStatistics();                     // メモリ上とディスク上のログ
        var 入荷=ストア.CreatePersistedList<入荷記録>("入荷");
        入荷.Add(new 入荷記録(商品ID,数量));
        """;

    public override async Task 実行Async(章の文脈 文脈){
        文脈.コードを指す("Store.CreateTemporary");
        var 上限=文脈.資源.ストアメモリ上限バイト数;
        using var ストア=Store.CreateTemporary(new StoreLimits{MemoryBytes=上限,DiskBytes=文脈.資源.ディスク上限バイト数},文脈.作業フォルダを作る("store"));
        文脈.ログ($"一時ストアを作りました(メモリ上限 {表示.バイト(上限)})。破棄するとフォルダごと消えます。");
        文脈.メーター("ストアのメモリ",ストア.Budget.MemoryBytes,上限);
        await 文脈.間を置くAsync();

        文脈.コードを指す("CreateTsavorite<int,string>");
        using var 商品名=ストア.CreateTsavorite<int,string>("商品名");
        await 文脈.間を置くAsync(0.5);
        文脈.コードを指す("辞書.Add");
        foreach(var 商品 in Shop.初期データ.商品一覧){
            using(var 辞書=商品名.Create()){
                辞書.Add(商品.商品ID,商品.名前);
            }
            文脈.ログ($"商品名を登録: {商品.商品ID} → {商品.名前}");
            文脈.指標("商品名の件数",商品名.GetLongCount().ToString("N0"));
            await 文脈.間を置くAsync(0.2);
        }
        文脈.メーター("ストアのメモリ",ストア.Budget.MemoryBytes,上限);

        文脈.コードを指す("CreateTsavoriteHashSet");
        // メモリ上のログをわざと小さくし、少ない件数でもディスクへの退避が起きるようにする。
        var 小さいログ=new TsavoriteTuning{PageSize=1L<<12,LogMemorySize=1L<<15};
        using var 来店客=ストア.CreateTsavoriteHashSet<int>("来店客",小さいログ);
        await 文脈.間を置くAsync(0.5);
        文脈.コードを指す("名簿.Add");
        var 来店回数=100_000;
        if(文脈.設定.低メモリか){
            来店回数=20_000;
        }
        var 顧客数=来店回数/2;
        var 区切り=来店回数/10;
        for(var 回=0;回<来店回数;回+=区切り){
            // 操作用オブジェクトはまとめて使い、待つ前に閉じる。
            using(var 名簿=来店客.Create()){
                for(var 件=0;件<区切り;件++){
                    名簿.Add(文脈.乱数.Next(1,顧客数+1));
                }
            }
            var 途中の統計=来店客.GetStatistics();
            文脈.棒グラフ("来店の記録","件",[
                new("来店回数",回+区切り,棒の色.比較),
                new("来店客(重複なし)",来店客.GetLongCount(),棒の色.強調),
            ]);
            文脈.メーター("来店客: メモリ上のログ",途中の統計.MemoryLogBytes,小さいログ.LogMemorySize!.Value);
            文脈.指標("来店客: ディスク上のログ",表示.バイト(途中の統計.DiskLogBytes));
            await 文脈.間を置くAsync(0.3);
        }
        文脈.コードを指す("GetStatistics");
        var 統計=来店客.GetStatistics();
        文脈.ログ($"来店{来店回数:N0}回のうち、重複を除くと{来店客.GetLongCount():N0}人でした。",ログ種別.成功);
        文脈.ログ($"メモリ上のログ {表示.バイト(統計.MemoryLogBytes)}、ディスク上のログ {表示.バイト(統計.DiskLogBytes)}。上限を超えた分はディスクにあります。");
        文脈.指標("索引のバケット数",統計.IndexSize.ToString("N0"));
        await 文脈.間を置くAsync();

        文脈.コードを指す("CreatePersistedList");
        var 入荷=ストア.CreatePersistedList<入荷記録>("入荷");
        文脈.コードを指す("入荷.Add");
        foreach(var 商品 in Shop.初期データ.商品一覧){
            入荷.Add(new 入荷記録(商品.商品ID,商品.在庫数));
        }
        long 入荷数=0;
        foreach(var 記録 in 入荷){
            入荷数+=記録.数量;
        }
        文脈.ログ($"入荷を{入荷.Count}件記録しました(合計{入荷数}個)。Persisted のリストはファイルに書かれます。",ログ種別.成功);
        文脈.メーター("ストアのメモリ",ストア.Budget.MemoryBytes,上限);
        await 文脈.間を置くAsync();
    }
}
