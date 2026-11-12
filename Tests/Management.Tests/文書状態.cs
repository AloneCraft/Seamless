using Seamless.Management;
using 設計器=Seamless.Management.DatabaseDesigner;
using モデル=Seamless.Management.Components.文書状態;

namespace Management.Tests;

public sealed class 文書状態{
    [Fact]public void DraftTablesBecomeReadableOnlyAfterPublication(){
        var published=設計器.Empty("db","sample");
        published=設計器.Apply(published,new("Existing","",[new("Id","int",true)],[]));
        var pending=設計器.Apply(published,new("Items","",[new("Id","int",true)],[]));
        var database=new CatalogDatabase("db","sample","db",1,"",false);
        var version=new DefinitionVersion(published,1,"owner",DateTimeOffset.UtcNow);
        var draft=new Draft(pending,"owner",1,1,DateTimeOffset.UtcNow);
        var document=new モデル(database,version,draft);
        Assert.Contains("Items",document.テーブル);
        Assert.True(document.公開済みか("Table","Existing"));
        Assert.False(document.公開済みか("Table","Items"));
        Assert.False(document.公開済みか("Table","dbo.Items"));
        var reopened=new モデル(database,version,draft);
        Assert.False(reopened.公開済みか("Table","Items"));
        document.公開完了(2);
        Assert.True(document.公開済みか("Table","Items"));
        Assert.True(document.公開済みか("Table","Existing"));
    }
    [Fact]public void 成果物の下書き束縛とプレビューを編集と公開に追従させる(){
        var 定義=設計器.Empty("db","sample");
        var 状態=new モデル(new("db","sample","db",1,"",false),new(定義,1,"owner",DateTimeOffset.UtcNow),new(定義,"owner",3,1,DateTimeOffset.UtcNow));
        var 成果物=new SourceProjectBuild("project","db",3,1,状態.定義.Hash(),状態.定義.Source.Hash(),"db.dll","pe","pdb",Guid.NewGuid(),[]){BundleHash="bundle"};
        Assert.False(状態.成果物を選択(new(成果物 with{DraftRevision=2},DateTimeOffset.UtcNow)));
        Assert.Null(状態.選択成果物);
        Assert.True(状態.成果物を選択(new(成果物,DateTimeOffset.UtcNow)));
        Assert.Equal("bundle",状態.検証キー.BundleHash);
        var 確認=new ChangePreview("token","db",3,1,[],[],true,[]){BundleHash="bundle"};
        Assert.False(状態.プレビューを設定(状態.定義,確認 with{BundleHash="other"}));
        Assert.True(状態.プレビューを設定(状態.定義,確認));
        var 元=状態.定義;
        状態.定義=元 with{Source=元.Source with{Revision="changed"}};
        Assert.Null(状態.選択成果物);Assert.Null(状態.検証キー.BundleHash);
        状態.定義=元;
        Assert.Equal("bundle",状態.検証キー.BundleHash);
        状態.公開完了(2);
        Assert.Null(状態.選択成果物);Assert.Null(状態.検証キー.BundleHash);Assert.Null(状態.プレビュー);
    }
    [Fact]public void 公開済みの移行設定を次の編集へ持ち越さない(){
        var 定義=設計器.Empty("db","sample");
        定義=設計器.Apply(定義,new("Items","",[new("Id","int",true),new("Name","string",false)],[]));
        var DB=new CatalogDatabase("db","sample","db",1,"",false);
        var 公開=new DefinitionVersion(定義,1,"owner",DateTimeOffset.UtcNow);
        var 下書き=new Draft(定義,"owner",1,0,DateTimeOffset.UtcNow);
        var 状態=new モデル(DB,公開,下書き);
        Assert.Null(状態.定義.Migration);
        Assert.False(状態.未適用か);
        Assert.False(状態.未保管か);
        Assert.Equal(1,状態.基準版);
        var 設計=設計器.Read(状態.定義,"Items");
        var 列=設計.Columns.Select(列=>{if(列.Name=="Name"){return 列 with{Name="DisplayName"};}return 列;}).ToArray();
        状態.定義=設計器.Apply(状態.定義,設計 with{Columns=列});
        var 移行=Assert.Single(状態.定義.Migration!.Tables);
        Assert.Equal("Name",Assert.Single(移行.Columns,列=>列.Target=="DisplayName").Source);
        Assert.True(状態.未適用か);
        状態.公開完了(2);
        Assert.Null(状態.定義.Migration);
        Assert.False(状態.未適用か);
        Assert.False(状態.未保管か);
        Assert.Equal(2,状態.基準版);
    }
    [Fact]public void 保管中に追加された編集を保存応答で上書きしない(){
        var 定義=設計器.Empty("db","sample");
        var 公開=new DefinitionVersion(定義,1,"owner",DateTimeOffset.UtcNow);
        var 状態=new モデル(new("db","sample","db",1,"",false),公開,null);
        var 保存対象=状態.定義;
        var 編集=保存対象 with{Source=保存対象.Source with{Files=[new("Database.cs",保存対象.Source.Files[0].Text+"\n//追加の編集")]}};
        状態.定義=編集;
        状態.保管完了(保存対象,new(保存対象,"owner",2,1,DateTimeOffset.UtcNow));
        Assert.Same(編集,状態.定義);
        Assert.True(状態.未保管か);
        Assert.Equal(2,状態.下書き版);
        var 確認=new ChangePreview("token","db",2,1,[],[],true,[]);
        Assert.False(状態.プレビューを設定(保存対象,確認));
        Assert.Null(状態.プレビュー);
        Assert.True(状態.プレビューを設定(編集,確認));
    }
    [Fact]public void 未適用の下書きは移行設定と基準版を保持する(){
        var 定義=設計器.Empty("db","sample");
        var 編集=設計器.Apply(定義,new("Items","",[new("Id","int",true)],[]));
        var 状態=new モデル(new("db","sample","db",3,"",false),new(定義,3,"owner",DateTimeOffset.UtcNow),new(編集,"owner",4,2,DateTimeOffset.UtcNow));
        Assert.NotNull(状態.定義.Migration);
        Assert.True(状態.未適用か);
        Assert.False(状態.未保管か);
        Assert.Equal(2,状態.基準版);
    }
}
