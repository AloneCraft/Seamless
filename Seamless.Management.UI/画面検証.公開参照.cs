using System.IO;
using System.Text.Json;
using System.Windows;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private async Task ソース閲覧制限を検証Async(画面検証設定 設定,string 画像ファイル){
        var ページ=await this.必須接続().CallAsync<Page<DefinitionSummary>>("definitions","",new{cursor=0});var 概要=ページ.Items.Single(項目=>項目.Definition.Id==設定.定義ID);await this.定義を取得して開くAsync(概要);
        if(this.結果.Visibility!=Visibility.Collapsed||this.編集定義 is not null||this.コード.Text.Length!=0||this.公開情報パネル.Visibility!=Visibility.Visible||!this.公開参照実行.IsEnabled||!this.公開情報.Text.Contains("Model.Query.Run",StringComparison.Ordinal)){throw new InvalidOperationException("本文を取得しない公開情報の表示が不正です。");}
        if(this.公開定義一覧.Items.OfType<System.Windows.Controls.TreeViewItem>().SelectMany(項目=>項目.Items.OfType<System.Windows.Controls.TreeViewItem>()).First(項目=>項目.Tag is DefinitionSummary 定義&&定義.Definition.Id==設定.定義ID).ContextMenu?.Items.Count!=1){throw new InvalidOperationException("問い合わせに実行のコンテキストメニューがありません。");}
        起動.画像保存(this,画像ファイル+".public-info.png");await this.実行結果Async(new(設定.定義ID,null,ManagementJson.Element(new{})),概要.Definition.Database);if(this.結果.Visibility!=Visibility.Visible||this.結果.Items.Count!=1){throw new InvalidOperationException("ソース閲覧権限のない利用者が公開問い合わせを実行できませんでした。");}
        起動.画像保存(this,画像ファイル+".execution.png");var DB=this.表示DB.Single(項目=>項目.Name==概要.Definition.Database);var 一覧=await this.必須接続().CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",new CatalogRequest(DB.Id));await this.カタログ選択Async(new(DB.Id,ObjectId:一覧.Items.First(項目=>項目.Kind=="Table").Id));
        if(this.ソース選択領域.Visibility!=Visibility.Collapsed||this.編集定義 is not null||this.コード.Text.Length!=0||!this.オブジェクト詳細.Text.Contains("Value",StringComparison.Ordinal)||!this.ソース案内.Text.Contains("権限",StringComparison.Ordinal)){throw new InvalidOperationException("構造の参照とソース閲覧権限の案内を両立できませんでした。");}
        起動.画像保存(this,画像ファイル);File.WriteAllText(画像ファイル+".result.json",JsonSerializer.Serialize(new{公開情報=true,公開実行=true,構造参照=true,本文非表示=true,実行コンテキストメニュー=true},ManagementJson.Options));
    }
}
