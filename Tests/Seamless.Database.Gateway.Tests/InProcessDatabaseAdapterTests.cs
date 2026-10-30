using Sample.NetShop.Core;
using Sample.NetShop.Core.Shop;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Tests;

public sealed class InProcessDatabaseAdapterTests{
    [Fact,Trait("Category","Unit")]
    public void CrudOperations(){
        var dbFolder=Path.Combine(Path.GetTempPath(),"SeamlessTests_Adapter_"+Guid.NewGuid().ToString("N"));
        using var db=商店DB.開く(dbFolder,資源設定.標準);
        db.初期データを登録する();
        var adapter=new InProcessDatabaseAdapter(db);
        using var session=new SessionContext(1,"商店DB");

        {
            // テーブル一覧の取得
            var tables=adapter.GetTables();
            Assert.Contains(tables,t=>t.TableName=="商品");
            Assert.Contains(tables,t=>t.TableName=="顧客");
        }

        {
            // SELECT 全件
            var result=adapter.ExecuteSelect("商品",null,null,session);
            Assert.True(result.Rows.Count>0);
            Assert.Equal(4,result.Columns.Count);
        }

        {
            // INSERT
            var newId=9999;
            var values=new Dictionary<string,object?>{
                {"商品ID",newId},
                {"名前","単体テスト商品"},
                {"単価",300},
                {"在庫数",15}
            };
            var affected=adapter.ExecuteInsert("商品",values,session);
            Assert.Equal(1,affected);

            // 追加確認
            var selectResult=adapter.ExecuteSelect("商品",null,(rec)=>{
                var idProp=rec.GetType().GetProperty("商品ID");
                if(idProp is not null){
                    var v=idProp.GetValue(rec);
                    return v is int iv&&iv==newId;
                }else{
                    return false;
                }
            },session);
            Assert.Single(selectResult.Rows);
        }

        {
            // UPDATE
            var newId=9999;
            var updateValues=new Dictionary<string,object?>{
                {"単価",450},
                {"在庫数",30}
            };
            var affected=adapter.ExecuteUpdate("商品",updateValues,(rec)=>{
                var idProp=rec.GetType().GetProperty("商品ID");
                if(idProp is not null){
                    var v=idProp.GetValue(rec);
                    return v is int iv&&iv==newId;
                }else{
                    return false;
                }
            },session);
            Assert.Equal(1,affected);

            // 更新確認
            var selectResult=adapter.ExecuteSelect("商品",null,(rec)=>{
                var idProp=rec.GetType().GetProperty("商品ID");
                if(idProp is not null){
                    var v=idProp.GetValue(rec);
                    return v is int iv&&iv==newId;
                }else{
                    return false;
                }
            },session);
            Assert.Single(selectResult.Rows);
            var priceIndex=selectResult.Columns.ToList().FindIndex(c=>c.Name=="単価");
            Assert.Equal(450,selectResult.Rows[0][priceIndex]);
        }

        {
            // DELETE
            var newId=9999;
            var affected=adapter.ExecuteDelete("商品",(rec)=>{
                var idProp=rec.GetType().GetProperty("商品ID");
                if(idProp is not null){
                    var v=idProp.GetValue(rec);
                    return v is int iv&&iv==newId;
                }else{
                    return false;
                }
            },session);
            Assert.Equal(1,affected);

            // 削除確認
            var selectResult=adapter.ExecuteSelect("商品",null,(rec)=>{
                var idProp=rec.GetType().GetProperty("商品ID");
                if(idProp is not null){
                    var v=idProp.GetValue(rec);
                    return v is int iv&&iv==newId;
                }else{
                    return false;
                }
            },session);
            Assert.Empty(selectResult.Rows);
        }
    }
}
