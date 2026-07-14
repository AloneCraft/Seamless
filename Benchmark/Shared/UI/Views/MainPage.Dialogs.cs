namespace UI;

public sealed partial class MainPage {
    private async Task<ベンチマーク終了操作> 終了確認ダイアログを表示Async(bool ベンチマーク実行中か,bool ベンチマーク一時停止中か,bool 未保存結果ありか) {
        var 選択肢一覧 = this.終了確認選択肢を作成(ベンチマーク実行中か,ベンチマーク一時停止中か,未保存結果ありか);
        var 結果 = await this.確認ダイアログを表示Async(
            "終了確認",
            終了確認メッセージを作成(ベンチマーク実行中か,ベンチマーク一時停止中か,未保存結果ありか),
            選択肢一覧);
        return Enum.TryParse<ベンチマーク終了操作>(結果,out var 操作)
            ? 操作
            : ベンチマーク終了操作.キャンセル;
    }

    private IReadOnlyList<確認ダイアログ選択肢> 終了確認選択肢を作成(bool ベンチマーク実行中か,bool ベンチマーク一時停止中か,bool 未保存結果ありか) {
        var 選択肢一覧 = ベンチマーク終了ダイアログロジック.選択肢を作成(ベンチマーク実行中か,ベンチマーク一時停止中か,未保存結果ありか);
        return 選択肢一覧
            .Select(選択肢 => new 確認ダイアログ選択肢(選択肢.表示文,選択肢.操作.ToString(),選択肢.役割==ベンチマークツールバーボタン役割.停止
                ? 確認ダイアログボタン種別.危険
                : 確認ダイアログボタン種別.中立))
            .ToArray();
    }

    private static string 終了確認メッセージを作成(bool ベンチマーク実行中か,bool ベンチマーク一時停止中か,bool 未保存結果ありか) {
        if(ベンチマーク一時停止中か) {
            return "一時停止中のベンチマークを停止して終了しますか？";
        }

        if(ベンチマーク実行中か) {
            if(未保存結果ありか) {
                return "実行中のベンチマークを中止し、未保存の結果をどう扱うか選んでください。";
            }

            return "実行中のベンチマークを中止して終了しますか？";
        }

        return 未保存結果ありか
            ? "未保存のベンチマーク結果があります。どう扱って終了しますか？"
            : "終了確認";
    }

    private Task<string> 確認ダイアログを表示Async(string タイトル,string メッセージ,IReadOnlyList<確認ダイアログ選択肢> 選択肢一覧) {
        var ルート = this.ルートレイアウト;
        var フォールバック値 = ダイアログフォールバック値を解決(選択肢一覧);
        if(ルート is null||選択肢一覧.Count==0) {
            return Task.FromResult(フォールバック値);
        }

        this.有効ダイアログを完了(ルート,this.有効ダイアログフォールバック値);

        var 完了ソース = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var オーバーレイ = this.確認ダイアログオーバーレイを作成(タイトル,メッセージ,選択肢一覧,値 => {
            this.有効ダイアログを完了(ルート,値);
        });
        this.有効ダイアログオーバーレイ=オーバーレイ;
        this.有効ダイアログ完了ソース=完了ソース;
        this.有効ダイアログフォールバック値=フォールバック値;
        ルート.Add(オーバーレイ,0,0);
        Grid.SetRowSpan(オーバーレイ,2);
        return 完了ソース.Task;
    }

    private void 有効ダイアログを完了(Grid ルート,string 値) {
        if(this.有効ダイアログオーバーレイ is not null) {
            ルート.Children.Remove(this.有効ダイアログオーバーレイ);
            this.有効ダイアログオーバーレイ=null;
        }

        var 完了ソース = this.有効ダイアログ完了ソース;
        this.有効ダイアログ完了ソース=null;
        this.有効ダイアログフォールバック値=string.Empty;
        完了ソース?.TrySetResult(値);
    }

    private static string ダイアログフォールバック値を解決(IReadOnlyList<確認ダイアログ選択肢> 選択肢一覧) {
        var キャンセル選択肢 = 選択肢一覧.FirstOrDefault(static 選択肢 =>
            string.Equals(選択肢.値,"キャンセル",StringComparison.Ordinal)
            ||string.Equals(選択肢.文言,"キャンセル",StringComparison.Ordinal));
        return キャンセル選択肢?.値??選択肢一覧.LastOrDefault()?.値??string.Empty;
    }

    private View 確認ダイアログオーバーレイを作成(
        string タイトル,
        string メッセージ,
        IReadOnlyList<確認ダイアログ選択肢> 選択肢一覧,
        Action<string> 完了時) {
        var オーバーレイ = new Grid {
            BackgroundColor=Color.FromRgba(15,23,42,110),
            Padding=new Thickness(24),
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=false,
        };
        var カード = new Border {
            BackgroundColor=Colors.White,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=14,
            },
            Shadow=new Shadow {
                Brush=Brush.Black,
                Opacity=0.18f,
                Radius=18,
                Offset=new Point(0,8),
            },
            Padding=new Thickness(24,22),
            MaximumWidthRequest=860,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
        };
        var 内容 = new VerticalStackLayout {
            Spacing=18,
        };
        内容.Children.Add(new Label {
            Text=タイトル,
            FontSize=18,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.WordWrap,
        });
        if(!string.IsNullOrWhiteSpace(メッセージ)) {
            内容.Children.Add(new Label {
                Text=メッセージ,
                FontSize=14,
                TextColor=Color.FromArgb("#334155"),
                HorizontalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.WordWrap,
            });
        }

        内容.Children.Add(this.確認ダイアログボタン一覧を作成(選択肢一覧,完了時));
        カード.Content=内容;
        オーバーレイ.Children.Add(カード);
        return オーバーレイ;
    }

    private View 確認ダイアログボタン一覧を作成(IReadOnlyList<確認ダイアログ選択肢> 選択肢一覧,Action<string> 完了時) {
        var レイアウト = new HorizontalStackLayout {
            Spacing=10,
            HorizontalOptions=LayoutOptions.Center,
        };

        for(var 選択肢番号 = 0;選択肢番号<選択肢一覧.Count;選択肢番号++) {
            var 選択肢 = 選択肢一覧[選択肢番号];
            var ボタン = this.確認ダイアログボタンを作成(選択肢);
            ボタン.Clicked+=(_,_) => 完了時(選択肢.値);
            レイアウト.Children.Add(ボタン);
        }

        return レイアウト;
    }

    private Button 確認ダイアログボタンを作成(確認ダイアログ選択肢 選択肢) {
        var ボタン = new Button {
            Text=選択肢.文言,
            HeightRequest=42,
            MinimumWidthRequest=128,
            WidthRequest=168,
            CornerRadius=8,
            BorderWidth=1,
            FontSize=14,
            FontAttributes=FontAttributes.Bold,
            Padding=new Thickness(12,0),
        };
        switch(選択肢.種別) {
            case 確認ダイアログボタン種別.主要:
                ボタン.BackgroundColor=Color.FromArgb("#0F766E");
                ボタン.BorderColor=Color.FromArgb("#0F766E");
                ボタン.TextColor=Colors.White;
                break;
            case 確認ダイアログボタン種別.危険:
                ボタン.BackgroundColor=Color.FromArgb("#FEF2F2");
                ボタン.BorderColor=Color.FromArgb("#FCA5A5");
                ボタン.TextColor=Color.FromArgb("#991B1B");
                break;
            default:
                ボタン.BackgroundColor=Colors.White;
                ボタン.BorderColor=Color.FromArgb("#CBD5E1");
                ボタン.TextColor=Color.FromArgb("#334155");
                break;
        }

        押下フィードバックを設定(ボタン);
        return ボタン;
    }

    private enum 確認ダイアログボタン種別 {
        主要,
        中立,
        危険,
    }

    private sealed record 確認ダイアログ選択肢(string 文言,string 値,確認ダイアログボタン種別 種別);

}
