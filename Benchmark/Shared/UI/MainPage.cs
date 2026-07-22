using Syncfusion.Maui.Buttons;
using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Toolkit.TabView;

using System.Collections.ObjectModel;
using System.ComponentModel;

using Seamless.UI.Storage;
using UI.ViewModels;

namespace UI;

public sealed partial class MainPage:ContentPage {
    // ベンチマーク選択列の幅
    private const double ベンチマーク選択列幅 = 52;
    // ベンチマーク集計チェックボックスのサイズ
    private const double ベンチマーク集約チェックボックス寸法 = 32;
    // ベンチマーク上部の全選択領域の幅
    private const double ベンチマーク上部全選択領域幅 = ベンチマーク集約チェックボックス寸法+4;
    // ベンチマーク進捗概要バッジの幅
    private const double ベンチマーク進行概要バッジ幅 = 280;
    private const double ベンチマーク_ベンチマーク = 320;
    private const double ベンチマークCPU = 58;
    private const double ベンチマーク_ループ = 64;
    // ベンチマーク引数列の幅
    private const double ベンチマーク引数列幅 = 72;
    // ベンチマーク回数引数列の幅
    private const double ベンチマークCount引数列幅 = 78;
    // ベンチマークメモリ引数列の幅
    private const double ベンチマークメモリ引数列幅 = 96;
    // ベンチマーク簡易引数列の幅
    private const double ベンチマークコンパクト引数列幅 = 64;
    private const double ベンチマーク_日時 = 132;
    private const double 時間 = 78;
    private const double メモリ = 78;
    // ベンチマークのペア停止回数列の幅
    private const double 時間シェア = 72;
    private const double メモリシェア = 72;
    // ベンチマークスクロールバーのサイズ
    private const double ベンチマークスクロールバー寸法 = 32;
    // CPUチェックボックスの幅
    private const double CPUチェックボックス幅 = 34;
    // CPUチェックボックスの高さ
    private const double CPUチェックボックス高さ = 28;
    // CPU論理コア列の幅
    private const double CPU論理列幅 = 340;
    // CPU論理コア確認列の幅
    private const double CPU論理チェック列幅 = 38;
    // CPU論理コア番号の幅
    private const double CPU論理番号幅 = 26;
    // 下部タブバーの高さ
    private const double 下部タブバー高さ = 34;
    // CPUセクションヘッダーの高さ
    private const double CPU区分見出し高さ = 34;
    // ベンチマークグリッド行の高さ
    private const double ベンチマークグリッド行高さ = 24;
    // ベンチマークグリッドヘッダー行の高さ
    private const double ベンチマークグリッド見出し行高さ = 30;
    // ベンチマークグリッドセルのフォントサイズ
    private const double ベンチマークグリッドセル文字寸法 = 10;
    // ベンチマークグリッド詳細のフォントサイズ
    private const double ベンチマークグリッド詳細文字寸法 = 9;
    // ベンチマークホバーセルの拡張余白
    private const double ベンチマークホバーセル拡張余白 = 14;
    // CPUグリッド行の高さ
    private const double CPUグリッド行高さ = 24;
    // CPUグリッドヘッダー行の高さ
    private const double CPUグリッド見出し行高さ = 30;
    // CPUグリッドセルのフォントサイズ
    private const double CPUグリッドセル文字寸法 = 10;
    // CPUセクショングリッドヘッダーの高さ
    private const double CPU区分グリッド見出し高さ = CPUグリッド見出し行高さ;
    // CPUセクション行の高さ
    private const double CPU区分行高さ = CPUグリッド行高さ;
    // CPUセクショングリッド追加の高さ
    private const double CPU区分グリッド追加高さ = 2;
    // CPUセクション間の間隔
    private const double CPU区分間隔 = 3;
    // CPUリストの間隔
    private const double CPU一覧間隔 = 10;
    // CPUコンテンツ行の間隔
    private const double CPU内容行間隔 = 10;
    private readonly 実行ビューモデル? ビューモデル;
    private readonly Grid? ルートレイアウト;
    private View? 有効ダイアログオーバーレイ;
    private TaskCompletionSource<string>? 有効ダイアログ完了ソース;
    private string 有効ダイアログフォールバック値 = string.Empty;
    private readonly Dictionary<ベンチマークツールバーボタン役割,VisualElement> ツールバーボタン一覧 = [];
    private readonly List<SfDataGrid> データグリッド更新バッチ対象一覧 = [];
    private readonly HashSet<SfDataGrid> データグリッド更新バッチ開始済み対象一覧 = [];
    private readonly HashSet<SfDataGrid> 選択抑止グリッド一覧 = [];
    private readonly Dictionary<SfDataGrid,チェック選択操作状態> チェック選択操作状態一覧=[];
    private int チェック選択操作番号;

    private sealed class チェック選択操作状態(int 操作番号,IReadOnlyList<object> 選択スナップショット) {
        public int 操作番号 { get; }=操作番号;
        public IReadOnlyList<object> 選択スナップショット { get; }=選択スナップショット;
    }
    private readonly Dictionary<ベンチマークソースタブ,SfDataGrid> ソースタブ別ベンチマークグリッド = [];
    private SfDataGrid? ベンチマークグリッド;
    private SfTabView? ソースタブビュー;
    private int データグリッド更新バッチ深度;
    private IDispatcherTimer? 一時停止中ツールバー点滅タイマー;
    private bool 一時停止中ツールバー点灯中か;
    private bool ソースタブビュー選択適用中か;
    private bool データグリッド更新バッチ購読済みか;
    private bool 読込警告購読済みか;
    private ベンチマークソート規則? ドラッグ中ベンチマークソート規則;

    public MainPage(実行ビューモデル ビューモデル) {
        try {
            起動診断.書き込む("MainPageコンストラクターを開始しました。");
            this.Title=ベンチマークアプリプロファイル.タイトル;
            this.BackgroundColor=Color.FromArgb("#F8FAFC");
            this.ビューモデル=ビューモデル;
            this.BindingContext=this.ビューモデル;
            this.データグリッド更新バッチを購読();
            this.読込警告を購読();

            var ツールバー = this.ツールバーを作成();
            var 内容 = this.ベンチマークパネル内容を作成();

            this.ルートレイアウト=new Grid {
                RowDefinitions= {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star },
                },
                Children= {
                    ツールバー,
                    内容,
                },
            };
            this.Content=this.ルートレイアウト;
            Grid.SetRow(内容,1);
            this.Loaded+=this.読み込み時Async;
            this.Unloaded+=this.アンロード時;
            起動診断.書き込む("MainPageコンストラクターが完了しました。");
        } catch(Exception 例外) {
            起動診断.書き込む("MainPageコンストラクターに失敗しました。",例外);
            this.Content=起動診断.エラーページを作成("ベンチマーク画面の作成に失敗しました",例外).Content;
        }
    }

    private void アンロード時(object? 送信元,EventArgs イベント引数) {
        this.データグリッド更新バッチ購読を解除();
        this.読込警告購読を解除();
        this.全データグリッドビュー更新バッチを終了();
    }

    private async void 読み込み時Async(object? 送信元,EventArgs イベント引数) {
        if(this.ビューモデル is null) {
            return;
        }

        try {
            this.データグリッド更新バッチを購読();
            this.読込警告を購読();
            起動診断.書き込む("MainPage初期化Asyncを開始しました。");
            await this.ビューモデル.初期化Async();
            起動診断.書き込む("MainPage初期化Asyncが完了しました。");
        } catch(Exception 例外) {
            起動診断.書き込む("MainPage初期化Asyncに失敗しました。",例外);
            this.ビューモデル.状態文字列="起動時読み込み失敗: "+例外.Message;
        }
    }

    private void データグリッド更新バッチを購読() {
        if(this.ビューモデル is null||this.データグリッド更新バッチ購読済みか) {
            return;
        }

        this.ビューモデル.データグリッド更新バッチ変更時+=this.データグリッド更新バッチ変更時;
        this.データグリッド更新バッチ購読済みか=true;
    }

    private void データグリッド更新バッチ購読を解除() {
        if(this.ビューモデル is null||!this.データグリッド更新バッチ購読済みか) {
            return;
        }

        this.ビューモデル.データグリッド更新バッチ変更時-=this.データグリッド更新バッチ変更時;
        this.データグリッド更新バッチ購読済みか=false;
    }

    private void 読込警告を購読() {
        if(this.ビューモデル is null||this.読込警告購読済みか) {
            return;
        }

        this.ビューモデル.読込警告が発生した+=this.読込警告発生時;
        this.読込警告購読済みか=true;
    }

    private void 読込警告購読を解除() {
        if(this.ビューモデル is null||!this.読込警告購読済みか) {
            return;
        }

        this.ビューモデル.読込警告が発生した-=this.読込警告発生時;
        this.読込警告購読済みか=false;
    }

    private async void 読込警告発生時(object? 送信元,string メッセージ) {
        try {
            await this.DisplayAlertAsync("読み込み警告",メッセージ,"OK");
        } catch(Exception 例外) {
            起動診断.書き込む("読込警告ポップアップの表示に失敗しました。",例外);
        }
    }

    private void データグリッド更新バッチ対象を登録(SfDataGrid グリッド) {
        if(this.データグリッド更新バッチ対象一覧.Contains(グリッド)) {
            return;
        }

        this.データグリッド更新バッチ対象一覧.Add(グリッド);
        グリッド.HandlerChanged+=(_,_) => {
            if(this.データグリッド更新バッチ深度>0) {
                this.データグリッドビュー更新を開始(グリッド);
            }
        };
        if(this.データグリッド更新バッチ深度>0) {
            this.データグリッドビュー更新を開始(グリッド);
        }
    }

    private void データグリッド更新バッチ変更時(object? 送信元,ベンチマークデータグリッド更新バッチイベント引数 イベント引数) {
        起動診断.メインスレッドで呼び出し開始(
            イベント引数.開始時か ? "データグリッド更新バッチ開始" : "データグリッド更新バッチ終了",
            () => {
                if(イベント引数.開始時か) {
                    this.データグリッドビュー更新バッチを開始();
                    return;
                }

                this.データグリッドビュー更新バッチを終了();
            });
    }

    private void データグリッドビュー更新バッチを開始() {
        if(this.データグリッド更新バッチ深度++>0) {
            return;
        }

        foreach(var グリッド in this.データグリッド更新バッチ対象一覧.ToArray()) {
            this.データグリッドビュー更新を開始(グリッド);
        }
    }

    private void データグリッドビュー更新バッチを終了() {
        if(this.データグリッド更新バッチ深度<=0) {
            return;
        }

        this.データグリッド更新バッチ深度--;
        if(this.データグリッド更新バッチ深度>0) {
            return;
        }

        this.開始済みデータグリッドビュー更新を終了();
    }

    private void 全データグリッドビュー更新バッチを終了() {
        this.データグリッド更新バッチ深度=0;
        this.開始済みデータグリッドビュー更新を終了();
    }

    private void データグリッドビュー更新を開始(SfDataGrid グリッド) {
        if(this.データグリッド更新バッチ開始済み対象一覧.Contains(グリッド)) {
            return;
        }

        try {
            var ビュー = グリッド.View;
            if(ビュー is null) {
                return;
            }

            ビュー.BeginInit();
            this.データグリッド更新バッチ開始済み対象一覧.Add(グリッド);
        } catch(Exception 例外) {
            起動診断.書き込む("SfDataGrid View.BeginInitに失敗しました。",例外);
        }
    }

    private void 開始済みデータグリッドビュー更新を終了() {
        var 開始済みグリッド一覧 = this.データグリッド更新バッチ開始済み対象一覧.ToArray();
        Array.Reverse(開始済みグリッド一覧);
        foreach(var グリッド in 開始済みグリッド一覧) {
            try {
                グリッド.View?.EndInit();
            } catch(Exception 例外) {
                起動診断.書き込む("SfDataGrid View.EndInitに失敗しました。",例外);
            }
        }
        this.データグリッド更新バッチ開始済み対象一覧.Clear();
    }

    public async Task<bool> 終了を要求Async() {
        if(this.ビューモデル is null) {
            return true;
        }

        var ベンチマーク有効か = this.ビューモデル.ベンチマーク実行有効か;
        var ベンチマーク一時停止中か = this.ビューモデル.ベンチマーク一時停止中か;
        var 未保存結果ありか = this.ビューモデル.未保存ベンチマーク結果ありか;
        if(!ベンチマーク終了ダイアログロジック.ダイアログが必要か(ベンチマーク有効か,ベンチマーク一時停止中か,未保存結果ありか)) {
            await this.ビューモデル.終了準備Async();
            return true;
        }

        var 操作 = await this.終了確認ダイアログを表示Async(ベンチマーク有効か,ベンチマーク一時停止中か,未保存結果ありか);
        if(操作==ベンチマーク終了操作.キャンセル) {
            return false;
        }

        if(ベンチマーク終了ダイアログロジック.閉じる前に未保存結果を破棄するか(ベンチマーク有効か,ベンチマーク一時停止中か,未保存結果ありか,操作)) {
            this.ビューモデル.未保存ベンチマーク結果を破棄();
        }

        if(ベンチマーク有効か||ベンチマーク一時停止中か) {
            this.ビューモデル.状態文字列="終了前に停止しています...";
            var 停止タスク = this.ビューモデル.停止Async();
            if(!await this.終了前のベンチマーク停止を待機Async(停止タスク)) {
                return false;
            }
        }

        await this.ビューモデル.終了準備Async();
        return true;
    }

    private async Task<bool> 終了前のベンチマーク停止を待機Async(Task 停止タスク) {
        var 短時間待機 = await Task.WhenAny(停止タスク,Task.Delay(350));
        if(ReferenceEquals(短時間待機,停止タスク)) {
            await 停止タスク;
            return true;
        }

        var ダイアログタスク = this.確認ダイアログを表示Async(
            "停止待ち",
            "実行中のベンチマークを停止しています。停止しない場合は強制終了できます。",
            [
                new 確認ダイアログ選択肢("強制終了","強制終了",確認ダイアログボタン種別.危険),
                new 確認ダイアログ選択肢("キャンセル","キャンセル",確認ダイアログボタン種別.中立),
            ]);
        var 完了したか = await Task.WhenAny(停止タスク,ダイアログタスク);
        if(ReferenceEquals(完了したか,停止タスク)) {
            if(this.ルートレイアウト is { } ルート) {
                this.有効ダイアログを完了(ルート,"停止済み");
            }
            await 停止タスク;
            return true;
        }

        var 操作 = await ダイアログタスク;
        if(string.Equals(操作,"強制終了",StringComparison.Ordinal)) {
            return true;
        }

        return false;
    }

    private void ベンチマークソートダイアログを表示() {
        var ルート = this.ルートレイアウト;
        if(ルート is null||this.ビューモデル is null) {
            return;
        }

        this.有効ダイアログを完了(ルート,string.Empty);
        var オーバーレイ = this.ベンチマークソートダイアログオーバーレイを作成(() => this.有効ダイアログを完了(ルート,string.Empty));
        this.有効ダイアログオーバーレイ=オーバーレイ;
        this.有効ダイアログ完了ソース=null;
        this.有効ダイアログフォールバック値=string.Empty;
        ルート.Add(オーバーレイ,0,0);
        Grid.SetRowSpan(オーバーレイ,2);
    }

    private View ベンチマークソートダイアログオーバーレイを作成(Action 閉じる) {
        var オーバーレイ = new Grid {
            BackgroundColor=Color.FromRgba(15,23,42,110),
            Padding=new Thickness(24),
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=false,
        };
        オーバーレイ.BindingContext=this.ビューモデル;
        var カード = new Border {
            BackgroundColor=Colors.White,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=14,
            },
            Padding=new Thickness(20,18),
            MaximumWidthRequest=820,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
        };
        var 内容 = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(300) },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=12,
        };
        内容.Add(new Label {
            Text="ソート設定",
            FontSize=18,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            HorizontalTextAlignment=TextAlignment.Center,
        },0,0);
        var 概要 = new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#334155"),
            LineBreakMode=LineBreakMode.TailTruncation,
            HorizontalTextAlignment=TextAlignment.Center,
        };
        概要.SetBinding(Label.TextProperty,nameof(実行ビューモデル.ベンチマークソート概要文字列));
        内容.Add(概要,0,1);
        内容.Add(this.ベンチマークソート規則一覧を作成(),0,2);
        内容.Add(this.ベンチマークソートダイアログボタンを作成(閉じる),0,3);
        カード.Content=内容;
        オーバーレイ.Children.Add(カード);
        return オーバーレイ;
    }

    private View ベンチマークソート規則一覧を作成() {
        var 一覧 = new CollectionView {
            SelectionMode=SelectionMode.None,
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=3,
            },
            ItemTemplate=new DataTemplate(() => this.ベンチマークソート規則行を作成()),
        };
        一覧.SetBinding(ItemsView.ItemsSourceProperty,nameof(実行ビューモデル.ベンチマークソート規則一覧));
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=一覧,
        };
    }

    private View ベンチマークソート規則行を作成() {
        var 行 = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(220) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
            },
            ColumnSpacing=5,
            Padding=new Thickness(6,3),
            BackgroundColor=Colors.White,
            MinimumHeightRequest=34,
        };
        var ハンドル = new Image {
            Source=ImageSource.FromFile("drag_handle.png"),
            WidthRequest=18,
            HeightRequest=18,
            Aspect=Aspect.AspectFit,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        ハンドル.SetValue(ToolTipProperties.TextProperty,"ドラッグで並び替え");
        var ピッカー = new Picker {
            HeightRequest=28,
            ItemDisplayBinding=new Binding(nameof(ベンチマークソート列選択肢.表示名)),
            Margin=new Thickness(0),
        };
        ピッカー.ItemsSource=this.ビューモデル?.ベンチマークソート列選択肢一覧;
        var 方向 = this.ソート規則アイコンボタンを作成("sort_asc.png","昇順/降順を切り替え");
        var 上 = this.ソート規則アイコンボタンを作成("arrow_up.png","上へ移動");
        var 下 = this.ソート規則アイコンボタンを作成("arrow_down.png","下へ移動");
        var 削除 = this.ソート規則アイコンボタンを作成("delete.png","削除");
        foreach(var 編集ボタン in new[] { ピッカー as VisualElement,方向,上,下,削除 }) {
            編集ボタン.SetBinding(VisualElement.IsEnabledProperty,new Binding(nameof(実行ビューモデル.ソート規則編集可能か),source: this.ビューモデル));
        }

        void ピッカーを更新() {
            if(行.BindingContext is not ベンチマークソート規則 規則||this.ビューモデル is null) {
                return;
            }
            ピッカー.ItemsSource=this.ビューモデル.ベンチマークソート列選択肢一覧;
            ピッカー.SelectedItem=this.ビューモデル.ベンチマークソート列選択肢一覧.FirstOrDefault(選択肢 => 選択肢.列==規則.列);
            方向.Source=ImageSource.FromFile(規則.降順か ? "sort_desc.png" : "sort_asc.png");
            方向.SetValue(ToolTipProperties.TextProperty,規則.降順か ? "降順" : "昇順");
        }

        行.BindingContextChanged+=(_,_) => ピッカーを更新();
        方向.BindingContextChanged+=(_,_) => ピッカーを更新();
        ピッカー.SelectedIndexChanged+=(_,_) => {
            if(行.BindingContext is ベンチマークソート規則 規則
                &&ピッカー.SelectedItem is ベンチマークソート列選択肢 選択肢) {
                this.ビューモデル?.ベンチマークソート規則列を設定(規則,選択肢.列);
            }
        };
        方向.Clicked+=(_,_) => {
            if(行.BindingContext is ベンチマークソート規則 規則) {
                this.ビューモデル?.ベンチマークソート規則方向を切り替え(規則);
                ピッカーを更新();
            }
        };
        上.Clicked+=(_,_) => {
            if(行.BindingContext is ベンチマークソート規則 規則) {
                this.ビューモデル?.ベンチマークソート規則を上へ移動(規則);
            }
        };
        下.Clicked+=(_,_) => {
            if(行.BindingContext is ベンチマークソート規則 規則) {
                this.ビューモデル?.ベンチマークソート規則を下へ移動(規則);
            }
        };
        削除.Clicked+=(_,_) => {
            if(行.BindingContext is ベンチマークソート規則 規則) {
                this.ビューモデル?.ベンチマークソート規則を削除(規則);
            }
        };

        var ドラッグ = new DragGestureRecognizer();
        ドラッグ.DragStarting+=(_,イベント引数) => {
            if(this.ビューモデル?.ソート規則編集可能か!=true) {
                イベント引数.Cancel=true;
                return;
            }
            this.ドラッグ中ベンチマークソート規則=行.BindingContext as ベンチマークソート規則;
        };
        var ドロップ = new DropGestureRecognizer();
        ドロップ.Drop+=(_,_) => {
            if(this.ビューモデル?.ソート規則編集可能か==true
                &&this.ドラッグ中ベンチマークソート規則 is not null
                &&行.BindingContext is ベンチマークソート規則 対象) {
                this.ビューモデル?.ベンチマークソート規則を移動(this.ドラッグ中ベンチマークソート規則,対象);
            }
            this.ドラッグ中ベンチマークソート規則=null;
        };
        行.GestureRecognizers.Add(ドラッグ);
        行.GestureRecognizers.Add(ドロップ);

        行.Add(ハンドル,0,0);
        行.Add(ピッカー,1,0);
        行.Add(方向,2,0);
        行.Add(上,3,0);
        行.Add(下,4,0);
        行.Add(削除,5,0);
        return 行;
    }

    private ImageButton ソート規則アイコンボタンを作成(string 画像ソース,string ツールチップ文言) {
        var ボタン = new ImageButton {
            Source=ImageSource.FromFile(画像ソース),
            WidthRequest=28,
            HeightRequest=28,
            Padding=new Thickness(5),
            CornerRadius=5,
            BackgroundColor=Colors.White,
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            Aspect=Aspect.AspectFit,
            Margin=new Thickness(0),
        };
        ボタン.SetValue(ToolTipProperties.TextProperty,ツールチップ文言);
        押下フィードバックを設定(ボタン);
        return ボタン;
    }

    private View ベンチマークソートダイアログボタンを作成(Action 閉じる) {
        var 追加 = this.確認ダイアログボタンを作成(new 確認ダイアログ選択肢("追加","追加",確認ダイアログボタン種別.中立));
        var リセット = this.確認ダイアログボタンを作成(new 確認ダイアログ選択肢("既定順","リセット",確認ダイアログボタン種別.中立));
        var 閉じるボタン = this.確認ダイアログボタンを作成(new 確認ダイアログ選択肢("閉じる","閉じる",確認ダイアログボタン種別.主要));
        追加.SetBinding(VisualElement.IsEnabledProperty,new Binding(nameof(実行ビューモデル.ソート規則編集可能か),source: this.ビューモデル));
        リセット.SetBinding(VisualElement.IsEnabledProperty,new Binding(nameof(実行ビューモデル.ソート規則編集可能か),source: this.ビューモデル));
        追加.Clicked+=(_,_) => this.ビューモデル?.ベンチマークソート規則を追加();
        リセット.Clicked+=(_,_) => this.ビューモデル?.ベンチマークソート規則をリセット();
        閉じるボタン.Clicked+=(_,_) => 閉じる();
        return new HorizontalStackLayout {
            Spacing=10,
            HorizontalOptions=LayoutOptions.Center,
            Children= {
                追加,
                リセット,
                閉じるボタン,
            },
        };
    }

    private View ツールバーを作成() {
        var 実行ボタン = this.ツールバーテキストボタンを作成("計測");
        this.ツールバーボタン状態をバインド(実行ボタン,ベンチマークツールバーボタン役割.実行);
        var 停止ボタン = this.ツールバーアイコンボタンを作成(
            "stop.png",
            "停止");
        this.ツールバーボタン状態をバインド(停止ボタン,ベンチマークツールバーボタン役割.停止);
        var DB保存ボタン = this.ツールバーテキストボタンを作成("DB保存");
        this.ツールバーボタン状態をバインド(DB保存ボタン,ベンチマークツールバーボタン役割.DB保存);
        var 新規グリッドボタン = this.ツールバーテキストボタンを作成("新規");
        this.ツールバーボタン状態をバインド(新規グリッドボタン,ベンチマークツールバーボタン役割.新規グリッド);
#if DEBUG
        var 無限実行ボタン = new Button {
            Text="永久実行",
            HeightRequest=30,
            MinimumWidthRequest=92,
            Padding=new Thickness(10,0),
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            TextColor=Color.FromArgb("#334155"),
            CornerRadius=6,
        };
        押下フィードバックを設定(無限実行ボタン);
        this.ツールバーボタン状態をバインド(無限実行ボタン,ベンチマークツールバーボタン役割.無限実行);
#endif
        var しきい値ピッカー = new Picker {
            WidthRequest=112,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(閾値選択肢.表示名)),
            Margin=new Thickness(0),
        };
        しきい値ピッカー.SetBinding(Picker.ItemsSourceProperty,nameof(実行ビューモデル.閾値選択肢一覧));
        しきい値ピッカー.SetBinding(Picker.SelectedItemProperty,nameof(実行ビューモデル.選択閾値));
        しきい値ピッカー.SetBinding(IsEnabledProperty,nameof(実行ビューモデル.ベンチマーク条件を変更可能か));

        var 実行プロファイルピッカー = new Picker {
            WidthRequest=96,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(実行プロファイル選択肢.表示名)),
            Margin=new Thickness(0),
        };
        実行プロファイルピッカー.SetBinding(Picker.ItemsSourceProperty,nameof(実行ビューモデル.実行プロファイル一覧));
        実行プロファイルピッカー.SetBinding(Picker.SelectedItemProperty,nameof(実行ビューモデル.選択実行プロファイル));
        実行プロファイルピッカー.SetBinding(IsEnabledProperty,nameof(実行ビューモデル.ベンチマーク条件を変更可能か));

        var 実行履歴ピッカー = new Picker {
            WidthRequest=220,
            HeightRequest=30,
            ItemDisplayBinding=new Binding(nameof(ベンチマーク実行選択肢.表示名)),
            Margin=new Thickness(0),
        };
        実行履歴ピッカー.SetBinding(Picker.ItemsSourceProperty,nameof(実行ビューモデル.実行選択肢一覧));
        実行履歴ピッカー.SetBinding(Picker.SelectedItemProperty,nameof(実行ビューモデル.選択実行選択肢));
        実行履歴ピッカー.SetBinding(IsEnabledProperty,nameof(実行ビューモデル.実行履歴を選択可能か));
        //DB削除アイコン
        var 実行履歴消去ボタン = this.ツールバーアイコンボタンを作成("delete.png","履歴消去");
        実行履歴消去ボタン.SetBinding(IsEnabledProperty,nameof(実行ビューモデル.実行履歴をクリア可能か));
        実行履歴消去ボタン.SetBinding(
            OpacityProperty,
            new Binding(nameof(実行ビューモデル.実行履歴をクリア可能か),converter: new 有効時不透明度コンバーター()));
        実行履歴消去ボタン.SetBinding(
            ToolTipProperties.TextProperty,
            nameof(実行ビューモデル.実行履歴クリアツールチップ文字列));
        実行履歴消去ボタン.Clicked+=async (_,_) => await this.実行履歴消去確認ダイアログを表示Async();
        var 状態ラベル = new Label {
            FontSize=12,
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(12,0,0,0),
        };
        状態ラベル.SetBinding(Label.TextProperty,nameof(実行ビューモデル.状態文字列));
        var グリッド = new Grid {
            Padding=new Thickness(8,4,8,3),
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=6,
        };
        グリッド.Add(this.ツールバーラベルを作成("測定時間"),0,0);
        グリッド.Add(しきい値ピッカー,1,0);
        グリッド.Add(this.ツールバーラベルを作成("モード"),2,0);
        グリッド.Add(実行プロファイルピッカー,3,0);
        グリッド.Add(実行ボタン,4,0);
        グリッド.Add(停止ボタン,5,0);
        グリッド.Add(DB保存ボタン,6,0);
        グリッド.Add(新規グリッドボタン,7,0);
#if DEBUG
        グリッド.Add(無限実行ボタン,8,0);
        グリッド.Add(this.ツールバーラベルを作成("結果"),9,0);
        グリッド.Add(実行履歴ピッカー,10,0);
        グリッド.Add(実行履歴消去ボタン,11,0);
#else
        グリッド.Add(this.ツールバーラベルを作成("結果"),8,0);
        グリッド.Add(実行履歴ピッカー,9,0);
        グリッド.Add(実行履歴消去ボタン,10,0);
#endif
        グリッド.Add(状態ラベル,12,0);
        return グリッド;
    }

    private View 状態アイコンプレビューを作成() {
        var レイアウト = new HorizontalStackLayout {
            Spacing=10,
            Padding=new Thickness(2,0,0,0),
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Center,
        };
        foreach(var (文言,画像ソース) in new[] {
            ("待機",ベンチマーク状態画像名.待機中),
            ("完了",ベンチマーク状態画像名.完了),
            ("失敗",ベンチマーク状態画像名.失敗),
            ("測定値なし",ベンチマーク状態画像名.測定なし),
            ("CPU 高性能",ベンチマークCPUコア画像名.高性能),
            ("CPU 高効率",ベンチマークCPUコア画像名.高効率),
        }) {
            レイアウト.Add(this.状態アイコンプレビュー項目を作成(文言,画像ソース));
        }

        return レイアウト;
    }

    private View 状態アイコンプレビュー項目を作成(string テキスト,string 画像ソース) {
        var グリッド = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(18) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=3,
            VerticalOptions=LayoutOptions.Center,
        };
        グリッド.Add(new Image {
            Source=ImageSource.FromFile(画像ソース),
            WidthRequest=16,
            HeightRequest=16,
            Aspect=Aspect.AspectFit,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        },0,0);
        グリッド.Add(new Label {
            Text=テキスト,
            FontSize=9,
            TextColor=Color.FromArgb("#334155"),
            VerticalTextAlignment=TextAlignment.Center,
        },1,0);
        return グリッド;
    }

    private ImageButton ツールバーアイコンボタンを作成(string 画像ソース,string ツールチップ文言) {
        var ボタン = new ImageButton {
            Source=ImageSource.FromFile(画像ソース),
            WidthRequest=30,
            HeightRequest=30,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            CornerRadius=6,
            Padding=new Thickness(5),
            Margin=new Thickness(0),
            Aspect=Aspect.AspectFit,
        };
        if(ツールチップ文言==nameof(実行ビューモデル.実行ボタンツールチップ文字列)) {
            ボタン.SetBinding(ToolTipProperties.TextProperty,nameof(実行ビューモデル.実行ボタンツールチップ文字列));
        } else {
            ボタン.SetValue(ToolTipProperties.TextProperty,ツールチップ文言);
        }
        押下フィードバックを設定(ボタン);
        return ボタン;
    }

    private Button ツールバーテキストボタンを作成(string テキスト) {
        var ボタン = new Button {
            Text=テキスト,
            HeightRequest=30,
            MinimumWidthRequest=72,
            Padding=new Thickness(10,0),
            FontSize=12,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            BorderColor=Color.FromArgb("#CBD5E1"),
            BorderWidth=1,
            TextColor=Color.FromArgb("#334155"),
            CornerRadius=6,
        };
        押下フィードバックを設定(ボタン);
        return ボタン;
    }

    private static void 押下フィードバックを設定(Button ボタン) =>
        押下フィードバック本体を設定(
            ボタン,
            ハンドラー => ボタン.Pressed+=ハンドラー,
            ハンドラー => ボタン.Released+=ハンドラー);

    private static void 押下フィードバックを設定(ImageButton ボタン) =>
        押下フィードバック本体を設定(
            ボタン,
            ハンドラー => ボタン.Pressed+=ハンドラー,
            ハンドラー => ボタン.Released+=ハンドラー);

    private static void 押下フィードバック本体を設定(
        VisualElement ボタン,
        Action<EventHandler> 押下時を購読,
        Action<EventHandler> 解放時を購読) {
        var 押下中か = false;
        var 解放時拡大率 = 1d;
        var 解放時垂直移動量 = 0d;

        void 解放後表示を復元() {
            if(!押下中か) {
                return;
            }

            押下中か=false;
            ボタン.Scale=解放時拡大率;
            ボタン.TranslationY=解放時垂直移動量;
        }

        押下時を購読((_,_) => {
            if(!ボタン.IsEnabled||押下中か) {
                return;
            }

            押下中か=true;
            解放時拡大率=ボタン.Scale;
            解放時垂直移動量=ボタン.TranslationY;
            ボタン.Scale=解放時拡大率*0.95;
            ボタン.TranslationY=解放時垂直移動量+1;
        });
        解放時を購読((_,_) => 解放後表示を復元());
        ボタン.PropertyChanged+=(_,イベント引数) => {
            if(イベント引数.PropertyName==nameof(VisualElement.IsEnabled)&&!ボタン.IsEnabled) {
                解放後表示を復元();
            }
        };
    }

    private void ツールバーボタン状態をバインド(ImageButton ボタン,ベンチマークツールバーボタン役割 役割) {
        this.ツールバーボタン一覧[役割]=ボタン;
        ボタン.Clicked+=async (_,_) => await this.ツールバーボタンクリック時Async(役割);
        this.ツールバーボタン状態バインド本体(ボタン,役割);
    }

    private void ツールバーボタン状態をバインド(Button ボタン,ベンチマークツールバーボタン役割 役割) {
        this.ツールバーボタン一覧[役割]=ボタン;
        ボタン.Clicked+=async (_,_) => await this.ツールバーボタンクリック時Async(役割);
        this.ツールバーボタン状態バインド本体(ボタン,役割);
    }

    private void ツールバーボタン状態バインド本体(VisualElement ボタン,ベンチマークツールバーボタン役割 役割) {
        void 適用() {
            if(this.ビューモデル is null) {
                return;
            }

            this.ツールバーボタン状態を適用(ボタン,this.ビューモデル.ツールバーボタン状態を取得(役割));
        }

        適用();
        if(this.ビューモデル is not null) {
            this.ビューモデル.PropertyChanged+=(_,イベント引数) => {
                if(イベント引数.PropertyName==nameof(実行ビューモデル.ベンチマーク実行有効か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.ベンチマーク一時停止中か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.更新中か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.ベンチマークを実行可能か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.ベンチマークを再開可能か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.実行ツールバーボタン使用可能か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.DB保存可能か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.新規グリッド作成可能か)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.選択ベンチマークセルありか)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.未保存ベンチマーク結果ありか)
                    ||イベント引数.PropertyName==nameof(実行ビューモデル.ツールバー状態入力)) {
                    起動診断.メインスレッドで呼び出し開始("ツールバー状態適用 "+役割,適用);
                }
            };
        }
    }

    private void ツールバーボタン状態を適用(VisualElement ボタン,ベンチマークツールバーボタン状態 状態) {
        ボタン.IsEnabled=状態.有効か;
        ボタン.InputTransparent=false;
        ボタン.Opacity=状態.無効表示をするか ? 0.45 : 1;
        var 境界色 = 状態.点滅するか&&this.一時停止中ツールバー点灯中か
            ? Color.FromArgb("#EA580C")
            : Color.FromArgb("#CBD5E1");
        var 背景色 = 状態.点滅するか&&this.一時停止中ツールバー点灯中か
            ? Color.FromArgb("#FFF7ED")
            : Color.FromArgb("#F8FAFC");
        if(ボタン is ImageButton 画像ボタン) {
            画像ボタン.BorderColor=境界色;
            画像ボタン.BackgroundColor=背景色;
        } else if(ボタン is Button テキストボタン) {
            テキストボタン.BorderColor=境界色;
            テキストボタン.BackgroundColor=背景色;
        }
        ボタン.Scale=状態.点滅するか&&this.一時停止中ツールバー点灯中か ? 1.08 : 1;
        this.一時停止中ツールバー点滅を更新();
    }

    private async Task ツールバーボタンクリック時Async(ベンチマークツールバーボタン役割 役割) {
        if(this.ビューモデル is null) {
            return;
        }

        var 状態 = this.ビューモデル.ツールバーボタン状態を取得(役割);
        if(!状態.有効か) {
            return;
        }

        if(ベンチマークツールバー一時停止ガードロジック.判断ダイアログが必要か(役割,this.ビューモデル.ベンチマーク一時停止中か)) {
            await this.一時停止中ツールバー判断ダイアログを表示Async(役割);
            return;
        }

        if(!状態.主要操作を呼ぶか) {
            return;
        }

        await this.ツールバーボタンコマンドを実行Async(役割);
    }

    private async Task ツールバーボタンコマンドを実行Async(ベンチマークツールバーボタン役割 役割) {
        if(this.ビューモデル is null) {
            return;
        }

        switch(役割) {
            case ベンチマークツールバーボタン役割.実行:
                if(this.ビューモデル.実行コマンド.CanExecute(null)) {
                    await this.ビューモデル.実行コマンド.ExecuteAsync(null);
                }
                break;
#if DEBUG
            case ベンチマークツールバーボタン役割.無限実行:
                if(this.ビューモデル.無限実行コマンド.CanExecute(null)) {
                    await this.ビューモデル.無限実行コマンド.ExecuteAsync(null);
                }
                break;
#endif
            case ベンチマークツールバーボタン役割.停止:
                if(this.ビューモデル.停止コマンド.CanExecute(null)) {
                    await this.ビューモデル.停止コマンド.ExecuteAsync(null);
                }
                break;
            case ベンチマークツールバーボタン役割.一時停止:
                if(this.ビューモデル.一時停止コマンド.CanExecute(null)) {
                    await this.ビューモデル.一時停止コマンド.ExecuteAsync(null);
                }
                break;
            case ベンチマークツールバーボタン役割.DB保存:
                if(this.ビューモデル.DB保存コマンド.CanExecute(null)) {
                    await this.ビューモデル.DB保存コマンド.ExecuteAsync(null);
                }
                break;
            case ベンチマークツールバーボタン役割.新規グリッド:
                if(this.ビューモデル.新規グリッドコマンド.CanExecute(null)) {
                    this.ビューモデル.新規グリッドコマンド.Execute(null);
                }
                break;
        }
    }

    private async Task 実行履歴消去確認ダイアログを表示Async() {
        if(this.ビューモデル is null||!this.ビューモデル.実行履歴クリアコマンド.CanExecute(null)) {
            return;
        }

        var 結果 = await this.確認ダイアログを表示Async(
            "履歴消去",
            "DB内の実行履歴をすべて消去します。よろしいですか？",
            [
                new 確認ダイアログ選択肢("全履歴を消去","消去",確認ダイアログボタン種別.危険),
                new 確認ダイアログ選択肢("キャンセル","キャンセル",確認ダイアログボタン種別.中立),
            ]);
        if(結果!="消去"||this.ビューモデル is null||!this.ビューモデル.実行履歴クリアコマンド.CanExecute(null)) {
            return;
        }

        await this.ビューモデル.実行履歴クリアコマンド.ExecuteAsync(null);
    }

    private async Task 一時停止中ツールバー判断ダイアログを表示Async(ベンチマークツールバーボタン役割 役割) {
        var 結果 = await this.確認ダイアログを表示Async(
            "一時停止中",
            一時停止中ツールバー判断メッセージを作成(役割),
            [
                new 確認ダイアログ選択肢("再開","再開",確認ダイアログボタン種別.主要),
                new 確認ダイアログ選択肢("停止","停止",確認ダイアログボタン種別.危険),
                new 確認ダイアログ選択肢("キャンセル","キャンセル",確認ダイアログボタン種別.中立),
            ]);
        switch(結果) {
            case "再開":
                await this.ツールバーボタンコマンドを実行Async(ベンチマークツールバーボタン役割.実行);
                break;
            case "停止":
                await this.ツールバーボタンコマンドを実行Async(ベンチマークツールバーボタン役割.停止);
                break;
        }
    }

    private static string 一時停止中ツールバー判断メッセージを作成(ベンチマークツールバーボタン役割 役割) =>
        "一時停止中です。続けるなら再開、終了するなら停止を選んでください。";

    private void 一時停止中ツールバー点滅を更新() {
        if(this.ビューモデル is null) {
            this.一時停止中ツールバー点滅を停止();
            return;
        }

        var 点滅するか = this.ツールバーボタン一覧.Keys
            .Any(役割 => this.ビューモデル.ツールバーボタン状態を取得(役割).点滅するか);
        if(!点滅するか) {
            this.一時停止中ツールバー点滅を停止();
            return;
        }

        if(this.一時停止中ツールバー点滅タイマー is not null) {
            return;
        }

        this.一時停止中ツールバー点滅タイマー=this.Dispatcher.CreateTimer();
        this.一時停止中ツールバー点滅タイマー.Interval=TimeSpan.FromMilliseconds(420);
        this.一時停止中ツールバー点滅タイマー.Tick+=(_,_) => {
            this.一時停止中ツールバー点灯中か=!this.一時停止中ツールバー点灯中か;
            this.ツールバーボタン表示状態を適用();
        };
        this.一時停止中ツールバー点滅タイマー.Start();
    }

    private void 一時停止中ツールバー点滅を停止() {
        this.一時停止中ツールバー点滅タイマー?.Stop();
        this.一時停止中ツールバー点滅タイマー=null;
        if(!this.一時停止中ツールバー点灯中か) {
            return;
        }

        this.一時停止中ツールバー点灯中か=false;
        this.ツールバーボタン表示状態を適用();
    }

    private void ツールバーボタン表示状態を適用() {
        if(this.ビューモデル is null) {
            return;
        }

        foreach(var 組 in this.ツールバーボタン一覧) {
            this.ツールバーボタン状態を適用(組.Value,this.ビューモデル.ツールバーボタン状態を取得(組.Key));
        }
    }

    private Label ツールバーラベルを作成(string テキスト) {
        return new Label {
            Text=テキスト,
            FontSize=12,
            VerticalTextAlignment=TextAlignment.Center,
            TextColor=Color.FromArgb("#334155"),
            Margin=new Thickness(0),
        };
    }

    private View ベンチマークパネル内容を作成() {
        var タブビュー = new SfTabView {
            AutomationId="ベンチマーク.ソースタブ表示",
            TabBarHeight=30,
            TabBarBackground=Color.FromArgb("#F8FAFC"),
            IndicatorBackground=Colors.Transparent,
            HeaderItemSpacing=1,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Colors.White,
            VerticalOptions=LayoutOptions.Fill,
        };
        this.コンパクトタブビューを構成(タブビュー,false);
        AutomationProperties.SetIsInAccessibleTree(タブビュー,true);
        var タブ項目一覧 = new TabItemCollection();
        var 読み込み済みソースタブ内容 = new HashSet<int>();
        var ソースタブインデックス = 0;
        foreach(var 選択肢 in this.ビューモデル?.ソースタブ一覧?? []) {
            var 取得済みインデックス = ソースタブインデックス;
            var 見出し = this.ソースタブ見出しを作成(選択肢,タブビュー,取得済みインデックス);
            var 状態 = this.ビューモデル?.ソースタブ表示状態を取得(選択肢.定義.タブ);
            var タブ項目 = new SfTabItem {
                AutomationId="ベンチマーク.ソースタブ内容."+選択肢.定義.タブ,
                Header=選択肢.見出し文字列,
                HeaderContent=見出し,
                FontSize=12,
                Content=取得済みインデックス==0
                    ? this.ソースタブ内容を作成(状態)
                    : this.ソースタブプレースホルダーを作成(選択肢),
            };
            AutomationProperties.SetIsInAccessibleTree(タブ項目,true);
            if(取得済みインデックス==0) {
                読み込み済みソースタブ内容.Add(取得済みインデックス);
            }
            選択肢.PropertyChanged+=(_,イベント引数) => {
                if(イベント引数.PropertyName==nameof(ソースタブ選択肢.見出し文字列)) {
                    起動診断.メインスレッドで呼び出し開始("ソースタブ見出し文 "+選択肢.定義.ID,() => タブ項目.Header=選択肢.見出し文字列);
                }
            };
            タブ項目一覧.Add(タブ項目);
            ソースタブインデックス++;
        }

        タブビュー.Items=タブ項目一覧;
        タブビュー.SelectedIndex=0;
        this.ソースタブビュー=タブビュー;
        void 選択中ソースタブ内容を保証() {
            if(this.ビューモデル is null) {
                return;
            }

            var インデックス = タブビュー.SelectedIndex;
            if(インデックス<0||インデックス>=this.ビューモデル.ソースタブ一覧.Count||読み込み済みソースタブ内容.Contains(インデックス)) {
                起動診断.パンくずを追加("ソースタブ内容を省略 インデックス="+インデックス.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            var 選択肢 = this.ビューモデル.ソースタブ一覧[インデックス];
            var 状態 = this.ビューモデル.ソースタブ表示状態を取得(選択肢.定義.タブ);
            起動診断.パンくずを追加("ソースタブ内容を作成 "+選択肢.定義.ID+" インデックス="+インデックス.ToString(System.Globalization.CultureInfo.InvariantCulture));
            タブ項目一覧[インデックス].Content=this.ソースタブ内容を作成(状態);
            読み込み済みソースタブ内容.Add(インデックス);
            起動診断.パンくずを追加("ソースタブ内容を作成済み "+選択肢.定義.ID+" 行数="+状態.行一覧.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        タブビュー.PropertyChanged+=(_,イベント引数) => {
            if(イベント引数.PropertyName!=nameof(SfTabView.SelectedIndex)||this.ビューモデル is null) {
                return;
            }

            起動診断.パンくずを追加(
                "ソースタブビュー選択インデックス変更 インデックス="
                +タブビュー.SelectedIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 実行中="+this.ビューモデル.ベンチマーク実行有効か.ToString(System.Globalization.CultureInfo.InvariantCulture)
                +" 選択="+this.ビューモデル.選択ソースタブ.ID);
            選択中ソースタブ内容を保証();
            if(this.ソースタブビュー選択適用中か) {
                this.有効ソース用ベンチマークグリッドを選択();
                return;
            }

            var インデックス = タブビュー.SelectedIndex;
            if(インデックス>=0&&インデックス<this.ビューモデル.ソースタブ一覧.Count) {
                this.ビューモデル.選択ソースタブ=this.ビューモデル.ソースタブ一覧[インデックス];
            }
            this.有効ソース用ベンチマークグリッドを選択();
        };
        if(this.ビューモデル is not null) {
            this.ビューモデル.PropertyChanged+=(_,イベント引数) => {
                if(イベント引数.PropertyName==nameof(実行ビューモデル.選択ソースタブ)) {
                    起動診断.メインスレッドで呼び出し開始("ソースタブのビューモデルプロパティ "+イベント引数.PropertyName,() => {
                        this.ソースタブビュー選択を同期();
                        選択中ソースタブ内容を保証();
                        this.有効ソース用ベンチマークグリッドを選択();
                    });
                } else if(イベント引数.PropertyName==nameof(実行ビューモデル.最新ベンチマーク自動スクロール要求)) {
                    var 要求 = this.ビューモデル.最新ベンチマーク自動スクロール要求;
                    if(要求 is not null) {
                        起動診断.メインスレッドで呼び出し開始("最新ベンチマーク自動スクロール "+要求.ソースタブ,() => this.ベンチマークグリッドを最新実行へスクロールAsync(要求));
                    }
                }
            };
        }
        var 下部内容 = this.ベンチマークタブ内容を作成();
        var 全体進行 = this.全ソース進行ビューを作成();
        Grid.SetColumn(タブビュー,1);
        var グリッド = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
            },
            RowSpacing=0,
            BackgroundColor=Colors.White,
            Padding=new Thickness(4,0,4,1),
            Children= {
                全体進行,
                タブビュー,
                下部内容,
            },
        };
        Grid.SetRow(タブビュー,1);
        Grid.SetRow(下部内容,2);
        return グリッド;
    }

    private View 全ソース進行ビューを作成() {
        var バッジ = this.進行概要バッジを作成(out var テキスト);
        if(this.ビューモデル is not null) {
            テキスト.SetBinding(Label.TextProperty,nameof(実行ビューモデル.全体ソース進行概要文字列));
        } else {
            テキスト.Text="確定 0/0";
        }
        var チェックホスト = this.上部全ベンチマーク選択チェックホストを作成();
        var プレビュー = this.状態アイコンプレビューを作成();
        var レイアウト = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(ベンチマーク上部全選択領域幅) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=4,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Center,
            Children= {
                チェックホスト,
                バッジ,
                プレビュー,
            },
        };
        Grid.SetColumn(バッジ,1);
        Grid.SetColumn(プレビュー,2);
        return レイアウト;
    }

    private View 上部全ベンチマーク選択チェックホストを作成() {
        var チェック = this.ベンチマーク集約チェックボックスを作成();
        チェック.AutomationId="ベンチマーク.全タブ選択チェック";
        チェック.SetValue(ToolTipProperties.TextProperty,"全タブのチェックを切り替え");
        if(this.ビューモデル is not null) {
            this.集約チェックボックスをバインド(チェック,this.ビューモデル,nameof(実行ビューモデル.全ベンチマーク選択状態),() => this.ビューモデル.全ベンチマーク選択状態);
        }
        var ホスト = new Grid {
            WidthRequest=ベンチマーク上部全選択領域幅,
            MinimumWidthRequest=ベンチマーク上部全選択領域幅,
            BackgroundColor=Colors.Transparent,
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Center,
            AutomationId="ベンチマーク.全タブ選択チェックホスト",
            Children= {
                チェック,
            },
        };
        ホスト.SetValue(ToolTipProperties.TextProperty,"全タブのチェックを切り替え");
        var タップ = new TapGestureRecognizer();
        タップ.Tapped+=(_,_) => {
            this.ビューモデル?.全ベンチマーク選択を切り替える();
        };
        ホスト.GestureRecognizers.Add(タップ);
        return ホスト;
    }

    private void コンパクトタブビューを構成(SfTabView タブビュー,bool スクロールボタンを有効にするか) {
        タブビュー.TabWidthMode=TabWidthMode.SizeToContent;
        タブビュー.TabHeaderAlignment=TabHeaderAlignment.Start;
        タブビュー.TabHeaderPadding=new Thickness(0);
        タブビュー.IsScrollButtonEnabled=スクロールボタンを有効にするか;
    }

    private Border 進行概要バッジを作成(out Label テキスト) {
        テキスト=new Label {
            FontSize=12,
            TextColor=Color.FromArgb("#334155"),
            VerticalTextAlignment=TextAlignment.Center,
            HorizontalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.NoWrap,
            Margin=new Thickness(6,1),
        };
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=new CornerRadius(6),
            },
            Content=テキスト,
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Center,
            WidthRequest=ベンチマーク進行概要バッジ幅,
            MinimumWidthRequest=ベンチマーク進行概要バッジ幅,
            HeightRequest=18,
        };
    }

    private View タブ見出しを作成(タブ見出し選択肢基底 選択肢,Action 選択時,IReadOnlyList<View>? 後続ビュー一覧 = null) {
        var ラベル = new Label {
            FontSize=12,
            HorizontalTextAlignment=TextAlignment.Center,
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
            Margin=new Thickness(6,1,3,1),
        };
        ラベル.SetBinding(Label.TextProperty,new Binding(nameof(タブ見出し選択肢基底.見出し文字列),source: 選択肢));
        ラベル.SetBinding(Label.FontAttributesProperty,new Binding(nameof(タブ見出し選択肢基底.見出しフォント属性),source: 選択肢));
        ラベル.SetBinding(Label.TextColorProperty,new Binding(nameof(タブ見出し選択肢基底.見出し文字色),source: 選択肢));

        var 内容 = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing=1,
            HorizontalOptions=LayoutOptions.Start,
            Children= {
                ラベル,
            },
        };
        if(後続ビュー一覧 is { Count:>0 }) {
            foreach(var 後続 in 後続ビュー一覧) {
                内容.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                内容.Add(後続,内容.ColumnDefinitions.Count-1,0);
            }
        }

        var レイアウト = new Border {
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=new CornerRadius(6),
            },
            Content=内容,
            Margin=new Thickness(1,0),
            HorizontalOptions=LayoutOptions.Start,
        };
        レイアウト.SetBinding(BackgroundColorProperty,new Binding(nameof(タブ見出し選択肢基底.見出し背景色),source: 選択肢));
        レイアウト.SetBinding(Border.StrokeProperty,new Binding(nameof(タブ見出し選択肢基底.見出し枠線),source: 選択肢));
        var タップ = new TapGestureRecognizer();
        タップ.Tapped+=(_,_) => 選択時();
        レイアウト.GestureRecognizers.Add(タップ);
        return レイアウト;
    }

    private View ソースタブ見出しを作成(ソースタブ選択肢 選択肢,SfTabView タブビュー,int タブインデックス) {
        var 進行 = new ベンチマーク円形進行ビュー {
            WidthRequest=18,
            HeightRequest=18,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
        };
        進行.SetBinding(ベンチマーク円形進行ビュー.進行プロパティ,new Binding(nameof(ソースタブ選択肢.進行割合),source: 選択肢));
        進行.SetBinding(ベンチマーク円形進行ビュー.輪郭色プロパティ,new Binding(nameof(ソースタブ選択肢.進行外枠色),source: 選択肢));
        進行.SetBinding(IsVisibleProperty,new Binding(nameof(ソースタブ選択肢.進行リング表示中か),source: 選択肢));
        進行.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(ソースタブ選択肢.進行ツールチップ文字列),source: 選択肢));
        進行.AutomationId="ベンチマーク.ソースタブ進捗リング."+選択肢.定義.タブ;
        AutomationProperties.SetIsInAccessibleTree(進行,true);
        var 完了アイコン = new Image {
            WidthRequest=14,
            HeightRequest=14,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            InputTransparent=true,
        };
        完了アイコン.SetBinding(Image.SourceProperty,new Binding(nameof(ソースタブ選択肢.完了アイコンソース),source: 選択肢,converter: new 画像ファイルソースコンバーター()));
        完了アイコン.SetBinding(IsVisibleProperty,new Binding(nameof(ソースタブ選択肢.完了アイコン表示中か),source: 選択肢));
        完了アイコン.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(ソースタブ選択肢.進行ツールチップ文字列),source: 選択肢));
        完了アイコン.AutomationId="ベンチマーク.ソースタブ進捗完了."+選択肢.定義.タブ;
        AutomationProperties.SetIsInAccessibleTree(完了アイコン,true);
        var 進行ホスト = new Grid {
            WidthRequest=18,
            HeightRequest=18,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            Margin=new Thickness(0,0,1,0),
            Children= {
                進行,
                完了アイコン,
            },
        };
        進行ホスト.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(ソースタブ選択肢.進行ツールチップ文字列),source: 選択肢));
        進行ホスト.SetBinding(IsVisibleProperty,new Binding(nameof(ソースタブ選択肢.進行ありか),source: 選択肢));
        進行ホスト.SetBinding(SemanticProperties.DescriptionProperty,new Binding(nameof(ソースタブ選択肢.進行自動化状態),source: 選択肢));
        進行ホスト.AutomationId="ベンチマーク.ソースタブ進捗."+選択肢.定義.タブ;
        AutomationProperties.SetIsInAccessibleTree(進行ホスト,true);
        var スピナー = new ActivityIndicator {
            WidthRequest=14,
            HeightRequest=14,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            Color=Color.FromArgb("#00FF00"),
            InputTransparent=true,
        };
        スピナー.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(nameof(ソースタブ選択肢.実行スピナー表示中か),source: 選択肢));
        スピナー.SetBinding(IsVisibleProperty,new Binding(nameof(ソースタブ選択肢.実行スピナー表示中か),source: 選択肢));
        スピナー.AutomationId="ベンチマーク.ソースタブ実行スピナー."+選択肢.定義.タブ;
        AutomationProperties.SetIsInAccessibleTree(スピナー,true);
        var スピナーホスト自動化要素 = new Button {
            WidthRequest=16,
            HeightRequest=18,
            MinimumWidthRequest=0,
            MinimumHeightRequest=0,
            Padding=new Thickness(0),
            BorderWidth=0,
            BackgroundColor=Colors.Transparent,
            InputTransparent=true,
        };
        スピナーホスト自動化要素.AutomationId="ベンチマーク.ソースタブ実行スピナーホスト."+選択肢.定義.タブ;
        AutomationProperties.SetIsInAccessibleTree(スピナーホスト自動化要素,true);
        var スピナーホスト = new Grid {
            WidthRequest=16,
            HeightRequest=18,
            VerticalOptions=LayoutOptions.Center,
            HorizontalOptions=LayoutOptions.Center,
            Margin=new Thickness(0,0,4,0),
            InputTransparent=true,
            Children= {
                スピナーホスト自動化要素,
                スピナー,
            },
        };
        var 見出し = this.タブ見出しを作成(
            選択肢,
            () => {
                if(タブビュー.SelectedIndex!=タブインデックス) {
                    タブビュー.SelectedIndex=タブインデックス;
                }
                if(this.ビューモデル is not null&&this.ビューモデル.選択ソースタブ!=選択肢) {
                    this.ビューモデル.選択ソースタブ=選択肢;
                }
            },
            [進行ホスト,スピナーホスト]);
        見出し.AutomationId="ベンチマーク.ソースタブ."+選択肢.定義.タブ;
        return 見出し;
    }

    private void ソースタブビュー選択を同期() {
        if(this.ビューモデル is null||this.ソースタブビュー is null) {
            return;
        }

        var 選択インデックス = -1;
        for(var 候補インデックス = 0;候補インデックス<this.ビューモデル.ソースタブ一覧.Count;候補インデックス++) {
            if(this.ビューモデル.ソースタブ一覧[候補インデックス]==this.ビューモデル.選択ソースタブ) {
                選択インデックス=候補インデックス;
                break;
            }
        }
        if(選択インデックス<0||this.ソースタブビュー.SelectedIndex==選択インデックス) {
            return;
        }

        this.ソースタブビュー選択適用中か=true;
        try {
            this.ソースタブビュー.SelectedIndex=選択インデックス;
        } finally {
            this.ソースタブビュー選択適用中か=false;
        }
    }

    private async void ベンチマークグリッドを最新実行へスクロールAsync(ベンチマーク自動スクロール要求 要求) {
        this.ソースタブビュー選択を同期();
        this.ベンチマークグリッドをメソッドへスクロール(要求.メソッド名,要求.行キー);
        await Task.Delay(150);
        起動診断.メインスレッドで呼び出し開始("ベンチマークグリッド遅延自動スクロール "+要求.ソースタブ,() => this.ベンチマークグリッドをメソッドへスクロール(要求.メソッド名,要求.行キー));
    }

    private void ベンチマークグリッドをメソッドへスクロール(string メソッド名,string 行キー) {
        if(this.ビューモデル is null||this.ベンチマークグリッド is null) {
            return;
        }

        var 行インデックス = -1;
        for(var インデックス = 0;インデックス<this.ビューモデル.ベンチマーク行一覧.Count;インデックス++) {
            var 行 = this.ビューモデル.ベンチマーク行一覧[インデックス];
            if(!string.IsNullOrWhiteSpace(行キー)
                &&string.Equals(行.行キー,行キー,StringComparison.OrdinalIgnoreCase)) {
                行インデックス=インデックス;
                break;
            }

            if(string.IsNullOrWhiteSpace(行キー)
                &&string.Equals(行.メソッド名,メソッド名,StringComparison.OrdinalIgnoreCase)) {
                行インデックス=インデックス;
                break;
            }
        }

        if(行インデックス<0) {
            return;
        }

        this.ベンチマークグリッド行へのスクロールを試行(行インデックス);
    }

    private void ベンチマークグリッド行へのスクロールを試行(int 行インデックス) {
        if(this.ベンチマークグリッド is null) {
            return;
        }

        try {
            var 見出しオフセット = this.ベンチマークグリッド.StackedHeaderRows.Count+1;
            this.メンバー呼び出しを試行(this.ベンチマークグリッド,"ScrollToRowIndex",行インデックス+見出しオフセット);
            this.メンバー呼び出しを試行(this.ベンチマークグリッド,"ScrollToRowIndex",行インデックス+1);
            this.メンバー呼び出しを試行(this.ベンチマークグリッド,"ScrollToRowIndex",行インデックス);
            var 表示コンテナー = this.ベンチマークグリッド.GetVisualContainer();
            if(表示コンテナー is not null) {
                this.メンバー呼び出しを試行(表示コンテナー,"ScrollToRowIndex",行インデックス+見出しオフセット);
                this.メンバー呼び出しを試行(表示コンテナー,"ScrollToRowIndex",行インデックス+1);
                this.メンバー呼び出しを試行(表示コンテナー,"ScrollToRowIndex",行インデックス);
            }
        } catch(Exception 例外) {
            起動診断.書き込む("ベンチマークグリッドの最新実行へのスクロールに失敗しました。",例外);
        }
    }

    private View ソースタブ内容を作成(ベンチマークソースタブ表示状態? 状態) {
        var グリッド = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Star },
            },
            RowSpacing=1,
            ColumnSpacing=6,
        };
        var 概要 = this.進行概要バッジを作成(out var 概要文);
        var ソースタブ選択肢 = 状態 is not null ? this.ビューモデル?.ソースタブ一覧.FirstOrDefault(選択肢 => 選択肢.定義.タブ==状態.定義.タブ) : this.ビューモデル?.選択ソースタブ;
        if(ソースタブ選択肢 is not null) {
            概要文.SetBinding(Label.TextProperty,new Binding(nameof(ソースタブ選択肢.進行概要文字列),source: ソースタブ選択肢));
            概要.SetBinding(ToolTipProperties.TextProperty,new Binding(nameof(ソースタブ選択肢.進行ツールチップ文字列),source: ソースタブ選択肢));
        }
        var 概要ホスト = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(ベンチマーク上部全選択領域幅) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=4,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Center,
            Children= {
                概要,
            },
        };
        Grid.SetColumn(概要,1);
        グリッド.Add(概要ホスト,0,0);

        var ベンチマークグリッド = this.ベンチマークグリッドを作成(状態);
        ベンチマークグリッド.VerticalOptions=LayoutOptions.Fill;
        グリッド.Add(ベンチマークグリッド,0,1);
        return グリッド;
    }

    private View ソースタブプレースホルダーを作成(ソースタブ選択肢 選択肢) =>
        new Grid {
            AutomationId="ベンチマーク.ソースタブプレースホルダー."+選択肢.定義.タブ,
            BackgroundColor=Colors.White,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };

    private View ベンチマークタブ内容を作成() {
        var グリッド = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
            },
        };
        var 下部タブビュー = new SfTabView {
            TabBarHeight=下部タブバー高さ,
            TabBarBackground=Color.FromArgb("#E2E8F0"),
            IndicatorBackground=Colors.Transparent,
            HeaderItemSpacing=1,
            EnableSwiping=false,
            ContentTransitionDuration=0,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
        };
        this.コンパクトタブビューを構成(下部タブビュー,false);
        var 下部項目一覧 = new TabItemCollection();
        var 下部タブ選択肢一覧 = new[] {
            new タブ見出し選択肢("CPU"),
            new タブ見出し選択肢("ログ"),
            new タブ見出し選択肢("改善プロンプト"),
        };
        var 下部内容一覧 = new View[] { this.CPUビューを作成(),this.ログビューを作成(),this.改善プロンプトビューを作成() };
        for(var インデックス = 0;インデックス<下部タブ選択肢一覧.Length;インデックス++) {
            var 取得済みインデックス = インデックス;
            下部項目一覧.Add(new SfTabItem {
                Header=下部タブ選択肢一覧[インデックス].見出し文字列,
                HeaderContent=this.タブ見出しを作成(下部タブ選択肢一覧[インデックス],() => 下部タブビュー.SelectedIndex=取得済みインデックス),
                FontSize=12,
                Content=下部内容一覧[インデックス],
            });
        }
        下部タブ選択肢一覧[0].選択状態を設定(true);
        下部タブビュー.Items=下部項目一覧;
        void 下部タブ高さを適用() {
            var 高さ = 下部タブバー高さ+this.CPUタブ内容高さを計算();
            if(Math.Abs(下部タブビュー.HeightRequest-高さ)>0.1) {
                下部タブビュー.HeightRequest=高さ;
            }
        }

        下部タブ高さを適用();
        下部タブビュー.Loaded+=(_,_) => 下部タブ高さを適用();
        下部タブビュー.SizeChanged+=(_,_) => 下部タブ高さを適用();
        下部タブビュー.SelectedIndex=(int)(this.ビューモデル?.選択下段タブ??ベンチマーク下段タブ.CPU);
        下部タブビュー.PropertyChanged+=(_,イベント引数) => {
            if(イベント引数.PropertyName!=nameof(SfTabView.SelectedIndex)) {
                return;
            }
            for(var インデックス = 0;インデックス<下部タブ選択肢一覧.Length;インデックス++) {
                下部タブ選択肢一覧[インデックス].選択状態を設定(インデックス==下部タブビュー.SelectedIndex);
            }
            if(this.ビューモデル is null) {
                return;
            }

            this.ビューモデル.選択下段タブ=下部タブビュー.SelectedIndex switch {
                1 => ベンチマーク下段タブ.ログ,
                2 => ベンチマーク下段タブ.改善プロンプト,
                _ => ベンチマーク下段タブ.CPU,
            };
        };
        var 下部タブ枠 = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle {
                CornerRadius=6,
            },
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=下部タブビュー,
        };
        グリッド.Add(下部タブ枠,0,0);

        if(this.ビューモデル is not null) {
            this.ビューモデル.PropertyChanged+=(_,イベント引数) => {
                if(イベント引数.PropertyName==nameof(実行ビューモデル.選択下段タブ)) {
                    起動診断.メインスレッドで呼び出し開始("下部タブ同期",() => 下部タブビュー.SelectedIndex=(int)this.ビューモデル.選択下段タブ);
                }
            };
        }
        return グリッド;
    }

    private double CPUタブ内容高さを計算() {
        if(this.ビューモデル is null) {
            return 0;
        }

        var 高性能高さ = this.CPU区分一覧高さを計算(this.ビューモデル.高性能CPU区分一覧);
        var 高効率高さ = this.CPU区分一覧高さを計算(this.ビューモデル.高効率CPU区分一覧);
        var 不明高さ = this.CPU区分一覧高さを計算(this.ビューモデル.不明CPU区分一覧);
        var 上部行高さ = Math.Max(高性能高さ,高効率高さ);
        if(不明高さ<=0) {
            return 上部行高さ;
        }

        return 上部行高さ<=0
            ? 不明高さ
            : 上部行高さ+CPU内容行間隔+不明高さ;
    }

    private double CPU区分一覧高さを計算(IEnumerable<CPU表区分> 区分一覧) {
        var 高さ一覧 = 区分一覧.Select(this.CPU区分高さを計算).ToArray();
        if(高さ一覧.Length==0) {
            return 0;
        }

        return 高さ一覧.Sum()+((高さ一覧.Length-1)*CPU一覧間隔);
    }

    private double CPU区分高さを計算(CPU表区分 区分) =>
        CPU区分見出し高さ
        +CPU区分間隔
        +CPU区分グリッド見出し高さ
        +(区分.行一覧.Count*CPU区分行高さ)
        +CPU区分グリッド追加高さ;

    private View ベンチマークグリッドを作成() =>
        this.ベンチマークグリッドを作成(this.ビューモデル?.選択ソースタブ表示状態);

    private SfDataGrid ベンチマークグリッドを作成(ベンチマークソースタブ表示状態? 状態) {
        起動診断.Syncfusionアセンブリを書き込む("ベンチマークグリッド作成前のSfDataGrid");
        var データグリッド = new SfDataGrid {
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=ベンチマークグリッド行高さ,
            HeaderRowHeight=ベンチマークグリッド見出し行高さ,
            AllowTriStateSorting=false,
            AllowResizingColumns=true,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.Extended,
            SelectionUnit=DataGridSelectionUnit.Row,
            NavigationMode=DataGridNavigationMode.Row,
            AllowKeyboardNavigation=true,
            AllowSelectionOnPress=true,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Always,
            VerticalScrollBarVisibility=ScrollBarVisibility.Always,
            GridLinesVisibility=GridLinesVisibility.Both,
            DefaultColumnWidth=92,
        };
        this.データグリッド更新バッチ対象を登録(データグリッド);
        if(状態 is not null) {
            データグリッド.AutomationId="ベンチマーク.グリッド."+状態.定義.タブ;
            this.ソースタブ別ベンチマークグリッド[状態.定義.タブ]=データグリッド;
            if(this.ビューモデル?.選択ソース定義.タブ==状態.定義.タブ) {
                this.ベンチマークグリッド=データグリッド;
            }
        } else {
            データグリッド.AutomationId="ベンチマーク.グリッド";
            this.ベンチマークグリッド=データグリッド;
        }
        var 選択コントローラー = new ベンチマークチェック選択コントローラー(
            データグリッド,
            行一覧 => this.ビューモデル?.ベンチマーク行一覧を切り替え(行一覧),
            (追加行一覧,削除行一覧) => this.ベンチマークグリッド選択を同期(状態,追加行一覧,削除行一覧));
        データグリッド.SelectionController=選択コントローラー;
        グリッドキーボードフォーカス動作.取り付ける(データグリッド);
        データグリッド.SelectionChanging+=(_,イベント引数) => {
            if(this.選択抑止グリッド一覧.Contains(データグリッド)) {
                イベント引数.Cancel=true;
            }
        };
        データグリッド.SelectionChanged+=(_,イベント引数) => {
            選択コントローラー.選択変更を処理(イベント引数);
        };
        AutomationProperties.SetIsInAccessibleTree(データグリッド,true);
        データグリッド.QueryRowHeight+=(_,イベント引数) => {
            if(イベント引数.RowIndex<2) {
                イベント引数.Height=ベンチマークグリッド見出し行高さ;
            }
        };
        データグリッド.CellDoubleTapped+=this.ベンチマークグリッドセルダブルタップ時;
        if(状態 is not null) {
            データグリッド.ItemsSource=状態.行一覧;
        } else {
            データグリッド.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(実行ビューモデル.ベンチマーク行一覧));
        }
        this.データグリッド線を構成(データグリッド,行フォーカスを表示するか: true);
        this.データグリッドスクロールバーを構成(データグリッド);

        var ソースタブ = 状態?.定義??this.ビューモデル?.選択ソース定義
            ??throw new InvalidDataException("ベンチマークグリッドのソースタブを解決できません。");
        this.ベンチマークグリッド列を再構築(データグリッド,ソースタブ);
        return データグリッド;
    }

    private void 有効ソース用ベンチマークグリッドを選択() {
        if(this.ビューモデル is null) {
            return;
        }

        if(this.ソースタブ別ベンチマークグリッド.TryGetValue(this.ビューモデル.選択ソース定義.タブ,out var グリッド)) {
            this.ベンチマークグリッド=グリッド;
        }
    }

    private void ベンチマークグリッド選択を同期(
        ベンチマークソースタブ表示状態? 状態,
        IReadOnlyList<ベンチマーク表行> 追加行一覧,
        IReadOnlyList<ベンチマーク表行> 削除行一覧) {
        if(this.ビューモデル is null||状態 is null) {
            return;
        }

        this.ビューモデル.ベンチマーク行UI選択を同期(状態,追加行一覧,削除行一覧);
    }

    private void 全ベンチマークグリッド行を選択(ベンチマークソースタブ定義 ソースタブ) {
        if(!this.ソースタブ別ベンチマークグリッド.TryGetValue(ソースタブ.タブ,out var グリッド)
            ||this.ビューモデル?.ソースタブ表示状態を取得(ソースタブ.タブ) is not { } 状態) {
            return;
        }

        グリッド.SelectAll();
    }

    private void ベンチマークグリッド列を再構築(SfDataGrid グリッド,ベンチマークソースタブ定義 ソースタブ) {
        グリッド.Columns.Clear();
        グリッド.StackedHeaderRows.Clear();
        var プロジェクト見出し行 = new DataGridStackedHeaderRow();
        グリッド.StackedHeaderRows.Add(プロジェクト見出し行);

        グリッド.Columns.Add(this.チェック列を作成(グリッド,ソースタブ));
        foreach(var 列 in ベンチマーク固定列UI一覧を取得()) {
            グリッド.Columns.Add(this.テキスト列を作成(列.見出し,列.プロパティ,列.ソート列,列.幅,列.配置));
        }
        var CPUマッピング = nameof(ベンチマーク表行.論理プロセッサ文字列);
        グリッド.Columns.Add(this.CPU列を作成(CPUマッピング));
        var ループマッピング = nameof(ベンチマーク表行.ループ回数文字列);
        グリッド.Columns.Add(this.テキスト列を作成("ループ回数",ループマッピング,ベンチマークグリッドソート列.ループ回数,ベンチマーク_ループ,TextAlignment.End));
        var 引数マッピング一覧 = this.引数列を追加(グリッド,ソースタブ);
        this.ベンチマーク基本積層見出しを追加(プロジェクト見出し行,CPUマッピング,ループマッピング,引数マッピング一覧);
        var 三列か=!string.IsNullOrWhiteSpace(ソースタブ.中央プロジェクト);
        if(三列か) {
            var 左時間シェア指定=new ベンチマークシェア列指定(
                nameof(ベンチマーク表行.左時間シェア表示),
                nameof(ベンチマーク表行.左時間シェアゲージ),
                static 行=>行.左時間シェアゲージ,
                ベンチマークグリッドソート列.左時間シェア,
                "時間シェア（"+ソースタブ.比較ペア一覧.First(ペア=>ペア.最適化列キー=="左").表示名+"）");
            var 左メモリシェア指定=new ベンチマークシェア列指定(
                nameof(ベンチマーク表行.左メモリシェア表示),
                nameof(ベンチマーク表行.左メモリシェアゲージ),
                static 行=>行.左メモリシェアゲージ,
                ベンチマークグリッドソート列.左メモリシェア,
                "メモリシェア（"+ソースタブ.比較ペア一覧.First(ペア=>ペア.最適化列キー=="左").表示名+"）");
            var 左マッピング一覧=this.プロジェクト列とシェアを追加(
                グリッド,nameof(ベンチマーク表行.左セル),nameof(ベンチマーク表行.左時間文字色),nameof(ベンチマーク表行.左メモリ文字色),
                ベンチマークグリッドソート列.左日時,ベンチマークグリッドソート列.左時間,左時間シェア指定,
                ベンチマークグリッドソート列.左割り当て,左メモリシェア指定);

            var 中央時間シェア指定=new ベンチマークシェア列指定(
                nameof(ベンチマーク表行.中央時間シェア表示),
                nameof(ベンチマーク表行.中央時間シェアゲージ),
                static 行=>行.中央時間シェアゲージ,
                ベンチマークグリッドソート列.中央時間シェア,
                "時間シェア（中央:右）");
            var 中央メモリシェア指定=new ベンチマークシェア列指定(
                nameof(ベンチマーク表行.中央メモリシェア表示),
                nameof(ベンチマーク表行.中央メモリシェアゲージ),
                static 行=>行.中央メモリシェアゲージ,
                ベンチマークグリッドソート列.中央メモリシェア,
                "メモリシェア（中央:右）");
            IReadOnlyList<string> 中央マッピング一覧;
            if(ソースタブ.比較ペア一覧.Any(ペア=>ペア.最適化列キー=="中央")){
                中央マッピング一覧=this.プロジェクト列とシェアを追加(
                    グリッド,nameof(ベンチマーク表行.中央セル),nameof(ベンチマーク表行.中央時間文字色),nameof(ベンチマーク表行.中央メモリ文字色),
                    ベンチマークグリッドソート列.中央日時,ベンチマークグリッドソート列.中央時間,中央時間シェア指定,
                    ベンチマークグリッドソート列.中央割り当て,中央メモリシェア指定);
            }else{
                中央マッピング一覧=this.プロジェクト列を追加(
                    グリッド,nameof(ベンチマーク表行.中央セル),nameof(ベンチマーク表行.中央時間文字色),nameof(ベンチマーク表行.中央メモリ文字色),
                    ベンチマークグリッドソート列.中央日時,ベンチマークグリッドソート列.中央時間,ベンチマークグリッドソート列.中央割り当て);
            }

            IReadOnlyList<string> 追加マッピング一覧=[];
            if(!string.IsNullOrWhiteSpace(ソースタブ.追加プロジェクト)){
                var 追加時間シェア指定=new ベンチマークシェア列指定(
                    nameof(ベンチマーク表行.追加時間シェア表示),
                    nameof(ベンチマーク表行.追加時間シェアゲージ),
                    static 行=>行.追加時間シェアゲージ,
                    ベンチマークグリッドソート列.追加時間シェア,
                    "時間シェア（"+ソースタブ.比較ペア一覧.First(ペア=>ペア.最適化列キー=="追加").表示名+"）");
                var 追加メモリシェア指定=new ベンチマークシェア列指定(
                    nameof(ベンチマーク表行.追加メモリシェア表示),
                    nameof(ベンチマーク表行.追加メモリシェアゲージ),
                    static 行=>行.追加メモリシェアゲージ,
                    ベンチマークグリッドソート列.追加メモリシェア,
                    "メモリシェア（"+ソースタブ.比較ペア一覧.First(ペア=>ペア.最適化列キー=="追加").表示名+"）");
                追加マッピング一覧=this.プロジェクト列とシェアを追加(
                    グリッド,nameof(ベンチマーク表行.追加セル),nameof(ベンチマーク表行.追加時間文字色),nameof(ベンチマーク表行.追加メモリ文字色),
                    ベンチマークグリッドソート列.追加日時,ベンチマークグリッドソート列.追加時間,追加時間シェア指定,
                    ベンチマークグリッドソート列.追加割り当て,追加メモリシェア指定);
            }
            var 右マッピング一覧=this.プロジェクト列を追加(グリッド,nameof(ベンチマーク表行.右セル),nameof(ベンチマーク表行.右時間文字色),nameof(ベンチマーク表行.右メモリ文字色),ベンチマークグリッドソート列.右日時,ベンチマークグリッドソート列.右時間,ベンチマークグリッドソート列.右割り当て);
            this.ベンチマークプロジェクト選択グループを追加(プロジェクト見出し行,this.ベンチマークプロジェクト選択グループを取得("左",ソースタブ.左見出し),左マッピング一覧,ソースタブ);
            this.ベンチマークプロジェクト選択グループを追加(プロジェクト見出し行,this.ベンチマークプロジェクト選択グループを取得("中央",ソースタブ.中央見出し),中央マッピング一覧,ソースタブ);
            if(!string.IsNullOrWhiteSpace(ソースタブ.追加プロジェクト)) {
                this.ベンチマークプロジェクト選択グループを追加(
                    プロジェクト見出し行,this.ベンチマークプロジェクト選択グループを取得("追加",ソースタブ.追加見出し),追加マッピング一覧,ソースタブ);
            }
            this.ベンチマークプロジェクト選択グループを追加(プロジェクト見出し行,this.ベンチマークプロジェクト選択グループを取得("右",ソースタブ.右見出し),右マッピング一覧,ソースタブ);
        } else {
            var スケーラブルか = ソースタブ.ID.StartsWith("Scalable",StringComparison.OrdinalIgnoreCase);
            var 左見出し=スケーラブルか ? "今回" : "左";
            var 右見出し=スケーラブルか ? "前回" : "右";
            var 時間シェア指定=new ベンチマークシェア列指定(
                nameof(ベンチマーク表行.時間シェア表示),
                nameof(ベンチマーク表行.時間シェアゲージ),
                static 行=>行.時間シェアゲージ,
                ベンチマークグリッドソート列.時間シェア);
            var メモリシェア指定=new ベンチマークシェア列指定(
                nameof(ベンチマーク表行.メモリシェア表示),
                nameof(ベンチマーク表行.メモリシェアゲージ),
                static 行=>行.メモリシェアゲージ,
                ベンチマークグリッドソート列.メモリシェア,
                "メモリシェア");
            var 左マッピング一覧=this.プロジェクト列とシェアを追加(
                グリッド,nameof(ベンチマーク表行.左セル),nameof(ベンチマーク表行.左時間文字色),nameof(ベンチマーク表行.左メモリ文字色),
                ベンチマークグリッドソート列.左日時,ベンチマークグリッドソート列.左時間,時間シェア指定,
                ベンチマークグリッドソート列.左割り当て,メモリシェア指定);
            var 右マッピング一覧 = this.プロジェクト列を追加(グリッド,nameof(ベンチマーク表行.右セル),nameof(ベンチマーク表行.右時間文字色),nameof(ベンチマーク表行.右メモリ文字色),ベンチマークグリッドソート列.右日時,ベンチマークグリッドソート列.右時間,ベンチマークグリッドソート列.右割り当て);
            this.ベンチマークプロジェクト選択グループを追加(プロジェクト見出し行,this.ベンチマークプロジェクト選択グループを取得("左",左見出し),左マッピング一覧,ソースタブ);
            this.ベンチマークプロジェクト選択グループを追加(プロジェクト見出し行,this.ベンチマークプロジェクト選択グループを取得("右",右見出し),右マッピング一覧,ソースタブ);
        }
    }

    private void ベンチマークグリッドセルダブルタップ時(object? 送信元,DataGridCellDoubleTappedEventArgs イベント引数) {
        if(送信元 is not SfDataGrid グリッド||イベント引数.Column is null||イベント引数.RowData is not null) {
            return;
        }

        var 計算済み幅 = this.ベンチマーク列自動幅を計算(イベント引数.Column)+8;
        var 最小幅 = イベント引数.Column.MinimumWidth;
        var 最大幅 = イベント引数.Column.MaximumWidth;
        var 幅 = Math.Max(計算済み幅,double.IsNaN(最小幅) ? 0 : 最小幅);
        if(!double.IsNaN(最大幅)&&最大幅>0) {
            幅=Math.Min(幅,最大幅);
        }

        イベント引数.Column.Width=幅;
    }

    private double ベンチマーク列自動幅を計算(DataGridColumn 列) {
        var 見出し幅 = ベンチマーク列テキストを測定(列.HeaderText);
        var 行一覧 = this.ビューモデル?.ベンチマーク行一覧?? [];
        var 内容幅 = 行一覧
            .Select(行 => ベンチマーク列テキストを測定(ベンチマーク列テキストを取得(行,列.MappingName)))
            .DefaultIfEmpty(0)
            .Max();
        return Math.Clamp(Math.Max(見出し幅,内容幅)+14,48,900);
    }

    private static double ベンチマーク列テキストを測定(string? テキスト) {
        if(string.IsNullOrWhiteSpace(テキスト)) {
            return 0;
        }

        var ラベル = new Label {
            Text=テキスト,
            FontSize=10,
            LineBreakMode=LineBreakMode.NoWrap,
            Padding=new Thickness(2,0),
        };
        return ラベル.Measure(double.PositiveInfinity,ベンチマークグリッド行高さ).Width;
    }

    private static string ベンチマーク列テキストを取得(ベンチマーク表行 行,string マッピング名) =>
        マッピング名 switch {
            nameof(ベンチマーク表行.ベンチマーク名) => 行.ベンチマーク名,
            nameof(ベンチマーク表行.ベンチマーク型文字列) => 行.ベンチマーク型文字列,
            nameof(ベンチマーク表行.ベンチマークメソッド文字列) => 行.ベンチマークメソッド文字列,
            nameof(ベンチマーク表行.ループ回数文字列) => 行.ループ回数文字列,
            nameof(ベンチマーク表行.Count引数) => 行.Count引数,
            nameof(ベンチマーク表行.Index引数) => 行.Index引数,
            nameof(ベンチマーク表行.Page引数) => 行.Page引数,
            nameof(ベンチマーク表行.Segment引数) => 行.Segment引数,
            nameof(ベンチマーク表行.Memory引数) => 行.Memory引数,
            nameof(ベンチマーク表行.割合引数) => 行.割合引数,
            nameof(ベンチマーク表行.Initial引数) => 行.Initial引数,
            nameof(ベンチマーク表行.Data引数) => 行.Data引数,
            nameof(ベンチマーク表行.停止要素数文字列) => 行.停止要素数文字列,
            nameof(ベンチマーク表行.時間シェア表示) => 行.時間シェア表示,
            nameof(ベンチマーク表行.メモリシェア表示) => 行.メモリシェア表示,
            nameof(ベンチマーク表行.左時間文字列) => 行.左時間文字列,
            nameof(ベンチマーク表行.左割り当て文字列) => 行.左割り当て文字列,
            nameof(ベンチマーク表行.左時間シェア表示) => 行.左時間シェア表示,
            nameof(ベンチマーク表行.左メモリシェア表示) => 行.左メモリシェア表示,
            nameof(ベンチマーク表行.中央時間文字列) => 行.中央時間文字列,
            nameof(ベンチマーク表行.中央割り当て文字列) => 行.中央割り当て文字列,
            nameof(ベンチマーク表行.中央時間シェア表示) => 行.中央時間シェア表示,
            nameof(ベンチマーク表行.中央メモリシェア表示) => 行.中央メモリシェア表示,
            nameof(ベンチマーク表行.右時間文字列) => 行.右時間文字列,
            nameof(ベンチマーク表行.右割り当て文字列) => 行.右割り当て文字列,
            "左セル確認" => 行.左セル.実行文字列,
            "右セル確認" => 行.右セル.実行文字列,
            _ => string.Empty,
        };

    private DataGridTemplateColumn チェック列を作成(SfDataGrid グリッド,ベンチマークソースタブ定義 ソースタブ) {
        return new DataGridTemplateColumn {
            MappingName=nameof(ベンチマーク表行.選択チェック状態),
            HeaderText="",
            Width=ベンチマーク選択列幅,
            AllowSorting=false,
            HeaderTemplate=this.全ベンチマーク選択列見出しテンプレートを作成(ソースタブ),
            CellTemplate=new DataTemplate(() => {
                var レイアウト = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(ベンチマーク集約チェックボックス寸法+4) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    WidthRequest=ベンチマーク選択列幅,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var チェック = this.Syncfusionチェックボックスを作成(ベンチマーク集約チェックボックス寸法,ベンチマーク集約チェックボックス寸法);
                チェック.AutomationId="ベンチマーク.行チェック";
                チェック.InputTransparent=true;
                チェック.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(ベンチマーク表行.選択チェック状態),BindingMode.OneWay));
                var チェックホスト = new Grid {
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                    AutomationId="ベンチマーク.行チェックホスト",
                };
                チェックホスト.Add(チェック,0,0);
                var チェックタップ = new TapGestureRecognizer();
                チェックタップ.Tapped+=(_,_) => {
                    try {
                        if(レイアウト.BindingContext is ベンチマーク表行 行) {
                            if(グリッド.SelectionController is ベンチマークチェック選択コントローラー 選択コントローラー) {
                                選択コントローラー.チェッククリックを処理(行,this.チェック開始時選択を取得<ベンチマーク表行>(グリッド));
                            } else {
                                this.ビューモデル?.ベンチマーク行を切り替え(行);
                            }
                        }
                    } finally {
                        this.チェック操作を完了(グリッド);
                    }
                };
                チェックホスト.GestureRecognizers.Add(チェックタップ);
                this.チェック中の選択を抑止(チェックホスト,グリッド);
                レイアウト.Add(チェックホスト,0,0);
                return レイアウト;
            }),
        };
    }

    private void ベンチマーク基本積層見出しを追加(
        DataGridStackedHeaderRow プロジェクト見出し行,
        string CPUマッピング,
        string ループマッピング,
        IReadOnlyList<string> 引数マッピング一覧) {
        var 基本マッピング一覧 = new List<string> {
            nameof(ベンチマーク表行.選択チェック状態),
            nameof(ベンチマーク表行.ベンチマークメソッド文字列),
            CPUマッピング,
            ループマッピング,
        };
        基本マッピング一覧.AddRange(引数マッピング一覧);
        プロジェクト見出し行.Columns.Add(new DataGridStackedColumn {
            MappingName="ベンチマーク基本見出し余白",
            ColumnMappingNames=string.Join(",",基本マッピング一覧),
            Template=this.ベンチマーク中立グループ見出しテンプレートを作成("項目"),
        });
    }

    private DataGridTemplateColumn シェアゲージ列を作成(
        string 見出し,
        string テキストパス,
        string ゲージプロパティ名,
        Func<ベンチマーク表行,ベンチマークシェアゲージ状態> ゲージ選択,
        ベンチマークグリッドソート列 ソート列,
        double 幅) =>
        new() {
            MappingName=テキストパス,
            HeaderText=見出し,
            Width=幅,
            AllowSorting=false,
            HeaderTemplate=this.見出しテンプレートを作成(見出し,ソート列),
            CellTemplate=new DataTemplate(() => シェアゲージセルを作成(テキストパス,ゲージプロパティ名,ゲージ選択)),
        };

    private static View シェアゲージセルを作成(string テキストパス,string ゲージプロパティ名,Func<ベンチマーク表行,ベンチマークシェアゲージ状態> ゲージ選択) {
        var 左塗り = new BoxView { Color=Color.FromArgb("#2563EB") };
        var 右塗り = new BoxView { Color=Color.FromArgb("#F97316") };
        var トラック = new Grid {
            HeightRequest=6,
            BackgroundColor=Colors.Transparent,
            ColumnDefinitions= {
                new ColumnDefinition { Width=0 },
                new ColumnDefinition { Width=0 },
            },
            Children={ 左塗り,右塗り },
        };
        Grid.SetColumn(右塗り,1);
        var テキスト = new Label {
            FontSize=ベンチマークグリッドセル文字寸法,
            TextColor=Color.FromArgb("#0F172A"),
            LineBreakMode=LineBreakMode.TailTruncation,
            HorizontalTextAlignment=TextAlignment.End,
            VerticalTextAlignment=TextAlignment.Center,
            Padding=new Thickness(2,0),
        };
        テキスト.SetBinding(Label.TextProperty,テキストパス);
        テキスト.SetBinding(ToolTipProperties.TextProperty,テキストパス);
        var セル = new Grid {
            RowDefinitions= {
                new RowDefinition { Height=GridLength.Star },
                new RowDefinition { Height=new GridLength(6) },
            },
            RowSpacing=1,
            Padding=new Thickness(1,0),
            // 他列と同じく透明にして、行選択時のハイライト色がシェアセルにも透けるようにする。
            BackgroundColor=Colors.Transparent,
            Children={ テキスト,トラック },
        };
        Grid.SetRow(トラック,1);

        ベンチマーク表行? 購読中行 = null;
        var 購読済みか = false;
        void 更新() {
            var ゲージ = セル.BindingContext is ベンチマーク表行 行 ? ゲージ選択(行) : default;
            var トラック幅 = Math.Max(0,トラック.Width);
                トラック.ColumnDefinitions[0].Width=new GridLength(ゲージ.色付きバーがあるか ? トラック幅*ゲージ.左割合 : 0);
                トラック.ColumnDefinitions[1].Width=new GridLength(ゲージ.色付きバーがあるか ? トラック幅*ゲージ.右割合 : 0);
        }
        void 行プロパティ変更時(object? 送信元,PropertyChangedEventArgs イベント引数) {
            if(イベント引数.PropertyName==ゲージプロパティ名||string.IsNullOrEmpty(イベント引数.PropertyName)) {
                更新();
            }
        }
        void 購読() {
            if(購読中行 is not null&&!購読済みか) {
                購読中行.PropertyChanged+=行プロパティ変更時;
                購読済みか=true;
            }
        }
        void 購読解除() {
            if(購読中行 is not null&&購読済みか) {
                購読中行.PropertyChanged-=行プロパティ変更時;
                購読済みか=false;
            }
        }
        セル.BindingContextChanged+=(_,_) => {
            購読解除();
            購読中行=セル.BindingContext as ベンチマーク表行;
            購読();
            更新();
        };
        セル.Loaded+=(_,_) => {
            購読();
            更新();
        };
        セル.Unloaded+=(_,_) => 購読解除();
        トラック.SizeChanged+=(_,_) => 更新();
        return セル;
    }

    private IReadOnlyList<string> 引数列を追加(SfDataGrid グリッド,ベンチマークソースタブ定義 ソースタブ) {
        var マッピング一覧 = new List<string>();
        foreach(var 引数列 in ベンチマーク引数列一覧.取得(ソースタブ)) {
            var 列 = 引数列UIを取得(引数列);
            グリッド.Columns.Add(this.テキスト列を作成(列.見出し,列.プロパティ,列.ソート列,列.幅,TextAlignment.End));
            マッピング一覧.Add(列.プロパティ);
        }
        return マッピング一覧;
    }

    private static ベンチマーク引数列UI 引数列UIを取得(スケーラブルベンチマーク引数列 列) =>
        列 switch {
            スケーラブルベンチマーク引数列.Count => new("Count",nameof(ベンチマーク表行.Count引数),ベンチマークグリッドソート列.引数Count,ベンチマークCount引数列幅),
            スケーラブルベンチマーク引数列.Index => new("Index",nameof(ベンチマーク表行.Index引数),ベンチマークグリッドソート列.引数インデックス,ベンチマーク引数列幅),
            スケーラブルベンチマーク引数列.Page => new("Page",nameof(ベンチマーク表行.Page引数),ベンチマークグリッドソート列.引数ページ,ベンチマークコンパクト引数列幅),
            スケーラブルベンチマーク引数列.Segment => new("Segment",nameof(ベンチマーク表行.Segment引数),ベンチマークグリッドソート列.引数セグメント,ベンチマーク引数列幅),
            スケーラブルベンチマーク引数列.Memory => new("Memory",nameof(ベンチマーク表行.Memory引数),ベンチマークグリッドソート列.引数メモリ,ベンチマークメモリ引数列幅),
            スケーラブルベンチマーク引数列.Fraction => new("Fraction",nameof(ベンチマーク表行.割合引数),ベンチマークグリッドソート列.引数割合,ベンチマークコンパクト引数列幅),
            スケーラブルベンチマーク引数列.Initial => new("Initial",nameof(ベンチマーク表行.Initial引数),ベンチマークグリッドソート列.引数初期値,ベンチマークコンパクト引数列幅),
            スケーラブルベンチマーク引数列.Data => new("Data",nameof(ベンチマーク表行.Data引数),ベンチマークグリッドソート列.引数データ,ベンチマークコンパクト引数列幅),
            _ => throw new ArgumentOutOfRangeException(nameof(列)),
        };

    private static IReadOnlyList<ベンチマーク固定列UI> ベンチマーク固定列UI一覧を取得() =>
        [new(
            "ベンチマーク",
            nameof(ベンチマーク表行.ベンチマークメソッド文字列),
            ベンチマークグリッドソート列.ベンチマークメソッド,
            ベンチマーク_ベンチマーク,
            TextAlignment.End)];

    private readonly record struct ベンチマーク固定列UI(
        string 見出し,
        string プロパティ,
        ベンチマークグリッドソート列 ソート列,
        double 幅,
        TextAlignment 配置);

    private readonly record struct ベンチマーク引数列UI(
        string 見出し,
        string プロパティ,
        ベンチマークグリッドソート列 ソート列,
        double 幅);

    private readonly record struct ベンチマークシェア列指定(
        string テキストマッピング,
        string ゲージプロパティ名,
        Func<ベンチマーク表行,ベンチマークシェアゲージ状態> ゲージ選択,
        ベンチマークグリッドソート列 ソート列,
        string 見出し="時間シェア");

    private IReadOnlyList<string> プロジェクト列とシェアを追加(
        SfDataGrid グリッド,
        string セルパス,
        string 時間文字色パス,
        string メモリ文字色パス,
        ベンチマークグリッドソート列 日時ソート列,
        ベンチマークグリッドソート列 時間ソート列,
        ベンチマークシェア列指定 時間シェア指定,
        ベンチマークグリッドソート列 割り当てソート列,
        ベンチマークシェア列指定 メモリシェア指定) {
        var 日時マッピング = セルパス+"日時";
        var 時間マッピング = セルパス+"時間";
        var 割り当てマッピング = セルパス+"割り当て";
        グリッド.Columns.Add(this.プロジェクト日時列を作成(日時マッピング,セルパス,日時ソート列));
        グリッド.Columns.Add(this.プロジェクトテキスト列を作成("時間",時間マッピング,セルパス,セルパス+"."+nameof(ベンチマークプロジェクトセル.時間文字列),時間文字色パス,時間ソート列,時間,TextAlignment.End));
        グリッド.Columns.Add(this.シェアゲージ列を作成(時間シェア指定.見出し,時間シェア指定.テキストマッピング,時間シェア指定.ゲージプロパティ名,時間シェア指定.ゲージ選択,時間シェア指定.ソート列,時間シェア));
        グリッド.Columns.Add(this.プロジェクトテキスト列を作成("メモリ",割り当てマッピング,セルパス,セルパス+"."+nameof(ベンチマークプロジェクトセル.割り当て文字列),メモリ文字色パス,割り当てソート列,メモリ,TextAlignment.End));
        グリッド.Columns.Add(this.シェアゲージ列を作成(メモリシェア指定.見出し,メモリシェア指定.テキストマッピング,メモリシェア指定.ゲージプロパティ名,メモリシェア指定.ゲージ選択,メモリシェア指定.ソート列,メモリシェア));
        return [日時マッピング,時間マッピング,時間シェア指定.テキストマッピング,割り当てマッピング,メモリシェア指定.テキストマッピング];
    }

    private IReadOnlyList<string> プロジェクト列を追加(
        SfDataGrid グリッド,
        string セルパス,
        string 時間文字色パス,
        string メモリ文字色パス,
        ベンチマークグリッドソート列 日時ソート列,
        ベンチマークグリッドソート列 時間ソート列,
        ベンチマークグリッドソート列 割り当てソート列) {
        var 日時マッピング = セルパス+"日時";
        var 時間マッピング = セルパス+"時間";
        var 割り当てマッピング = セルパス+"割り当て";
        グリッド.Columns.Add(this.プロジェクト日時列を作成(日時マッピング,セルパス,日時ソート列));
        グリッド.Columns.Add(this.プロジェクトテキスト列を作成("時間",時間マッピング,セルパス,セルパス+"."+nameof(ベンチマークプロジェクトセル.時間文字列),時間文字色パス,時間ソート列,時間,TextAlignment.End));
        グリッド.Columns.Add(this.プロジェクトテキスト列を作成("メモリ",割り当てマッピング,セルパス,セルパス+"."+nameof(ベンチマークプロジェクトセル.割り当て文字列),メモリ文字色パス,割り当てソート列,メモリ,TextAlignment.End));
        return [日時マッピング,時間マッピング,割り当てマッピング];
    }

    private DataGridTemplateColumn CPU列を作成(string マッピング名) {
        return new DataGridTemplateColumn {
            MappingName=マッピング名,
            HeaderText="CPU",
            Width=ベンチマークCPU,
            AllowSorting=false,
            HeaderTemplate=this.見出しテンプレートを作成("CPU",ベンチマークグリッドソート列.論理CPU),
            CellTemplate=new DataTemplate(() => {
                var グリッド = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(2,0),
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var バッジ = this.コアバッジを作成();
                バッジ.SetBinding(IsVisibleProperty,nameof(ベンチマーク表行.CPU表示ありか));
                バッジ.SetBinding(BackgroundColorProperty,nameof(ベンチマーク表行.バッジ背景色));
                if(バッジ.Content is Image バッジ画像) {
                    バッジ画像.SetBinding(
                        Image.SourceProperty,
                        new Binding(nameof(ベンチマーク表行.コア種別画像ソース),converter: new 画像ファイルソースコンバーター()));
                }
                var 番号 = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#0F172A"),
                    FontAttributes=FontAttributes.Bold,
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                番号.SetBinding(IsVisibleProperty,nameof(ベンチマーク表行.CPU表示ありか));
                番号.SetBinding(Label.TextProperty,nameof(ベンチマーク表行.論理プロセッサ文字列));
                グリッド.SetBinding(ToolTipProperties.TextProperty,nameof(ベンチマーク表行.CPUツールチップ文字列));
                グリッド.Add(バッジ,0,0);
                グリッド.Add(番号,1,0);
                return グリッド;
            }),
        };
    }

    private void ベンチマークプロジェクト選択グループを追加(DataGridStackedHeaderRow 見出し行,ベンチマーク列選択グループ グループ,IReadOnlyList<string> 子マッピング一覧,ベンチマークソースタブ定義 ソースタブ) {
        見出し行.Columns.Add(new DataGridStackedColumn {
            MappingName="ベンチマークプロジェクトグループ"+グループ.ID,
            ColumnMappingNames=string.Join(",",子マッピング一覧),
            Template=this.ベンチマーク列グループ見出しテンプレートを作成(グループ,ソースタブ),
        });
    }

    private ベンチマーク列選択グループ ベンチマークプロジェクト選択グループを取得(string ID,string 見出し文) {
        return this.ビューモデル?.ベンチマークプロジェクト選択グループ一覧.FirstOrDefault(グループ => グループ.ID==ID)
            ??new ベンチマーク列選択グループ(ID,見出し文,[]);
    }

    private DataTemplate 全ベンチマーク選択列見出しテンプレートを作成(ベンチマークソースタブ定義 ソースタブ) {
        return new DataTemplate(() => {
            var レイアウト = new Grid {
                ColumnDefinitions= {
                    new ColumnDefinition { Width = new GridLength(ベンチマーク集約チェックボックス寸法+4) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                WidthRequest=ベンチマーク選択列幅,
                BackgroundColor=Color.FromArgb("#F1F5F9"),
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            var チェック = this.ベンチマーク集約チェックボックスを作成();
            チェック.AutomationId="ベンチマーク.全選択チェック";
            var 状態 = this.ビューモデル?.ソースタブ表示状態を取得(ソースタブ.タブ);
            if(状態 is not null) {
                this.集約チェックボックスをバインド(チェック,状態,nameof(ベンチマークソースタブ表示状態.ベンチマーク選択状態),() => 状態.ベンチマーク選択状態);
            }
            var チェックホスト = new Grid {
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
                AutomationId="ベンチマーク.全選択チェックホスト",
            };
            チェックホスト.Add(チェック,0,0);
            var チェックタップ = new TapGestureRecognizer();
            チェックタップ.Tapped+=(_,_) => {
                this.ビューモデル?.全ベンチマーク選択を切り替える(ソースタブ);
            };
            チェックホスト.GestureRecognizers.Add(チェックタップ);
            var 選択ホスト = new Grid {
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            var 選択タップ = new TapGestureRecognizer();
            選択タップ.Tapped+=(_,_) => this.全ベンチマークグリッド行を選択(ソースタブ);
            選択ホスト.GestureRecognizers.Add(選択タップ);
            レイアウト.Add(チェックホスト,0,0);
            レイアウト.Add(選択ホスト,1,0);
            return レイアウト;
        });
    }

    private DataTemplate ベンチマーク列グループ見出しテンプレートを作成(ベンチマーク列選択グループ グループ,ベンチマークソースタブ定義 ソースタブ) {
        return new DataTemplate(() => {
            var 本体 = this.ベンチマーク見出し本体を作成(nameof(ベンチマーク列選択グループ.見出し文字列),Color.FromArgb("#EAF3FF"),Color.FromArgb("#CBD5E1"));
            本体.BindingContext=グループ;
            return 本体;
        });
    }

    private DataTemplate ベンチマーク中立グループ見出しテンプレートを作成(string テキスト) {
        return new DataTemplate(() => this.ベンチマーク見出し本体を作成(テキスト,Color.FromArgb("#F8FAFC"),Color.FromArgb("#CBD5E1")));
    }
    private Grid ベンチマーク見出し本体を作成(string テキスト,Color 背景色,Color 境界色) {
        var 本体 = new Grid {
            BackgroundColor=背景色,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        this.ベンチマーク見出し縦境界を追加(本体,境界色);
        if(!string.IsNullOrWhiteSpace(テキスト)) {
            var ラベル = new Label {
                FontSize=12,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                InputTransparent=true,
                HorizontalOptions=LayoutOptions.Fill,
                HorizontalTextAlignment=TextAlignment.Center,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
                Margin=new Thickness(4,0),
            };
            if(テキスト==nameof(ベンチマーク列選択グループ.見出し文字列)) {
                ラベル.SetBinding(Label.TextProperty,nameof(ベンチマーク列選択グループ.見出し文字列));
            } else {
                ラベル.Text=テキスト;
            }
            本体.Add(ラベル,0,0);
        }
        return 本体;
    }

    private void ベンチマーク見出し縦境界を追加(Grid 本体,Color 境界色) {
        var 左 = new BoxView {
            WidthRequest=1,
            BackgroundColor=境界色,
            HorizontalOptions=LayoutOptions.Start,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=true,
        };
        本体.Add(左,0,0);
        Grid.SetColumnSpan(左,Math.Max(1,本体.ColumnDefinitions.Count));
        var 右 = new BoxView {
            WidthRequest=1,
            BackgroundColor=境界色,
            HorizontalOptions=LayoutOptions.End,
            VerticalOptions=LayoutOptions.Fill,
            InputTransparent=true,
        };
        本体.Add(右,0,0);
        Grid.SetColumnSpan(右,Math.Max(1,本体.ColumnDefinitions.Count));
    }

    private SfCheckBox ベンチマーク集約チェックボックスを作成() {
        var チェック = this.Syncfusionチェックボックスを作成(ベンチマーク集約チェックボックス寸法,ベンチマーク集約チェックボックス寸法,true);
        チェック.InputTransparent=true;
        return チェック;
    }

    private void 集約チェックボックスをバインド(SfCheckBox チェック,INotifyPropertyChanged ソース,string プロパティ名,Func<bool?> 値を取得) {
        チェック.IsChecked=値を取得();
        チェック.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(プロパティ名,BindingMode.OneWay,source: ソース));
        PropertyChangedEventHandler ハンドラー = (_,イベント引数) => {
            if(イベント引数.PropertyName==プロパティ名) {
                起動診断.メインスレッドで呼び出し開始("ベンチマーク列見出し選択適用",() => {
                    チェック.IsChecked=値を取得();
                });
            }
        };
        ソース.PropertyChanged+=ハンドラー;
        チェック.Unloaded+=(_,_) => ソース.PropertyChanged-=ハンドラー;
    }

    private DataGridTemplateColumn プロジェクト日時列を作成(string マッピング名,string セルパス,ベンチマークグリッドソート列 日時ソート列) {
        return new DataGridTemplateColumn {
            MappingName=マッピング名,
            HeaderText="日時",
            Width=ベンチマーク_日時,
            AllowSorting=false,
            HeaderTemplate=this.見出しテンプレートを作成("日時",日時ソート列),
            CellTemplate=new DataTemplate(() => {
                var レイアウト = new Grid {
                    RowDefinitions= {
                        new RowDefinition { Height = new GridLength(2) },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = new GridLength(2) },
                    },
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(2) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(2) },
                    },
                    BackgroundColor=Colors.Transparent,
                    VerticalOptions=LayoutOptions.Fill,
                    HorizontalOptions=LayoutOptions.Fill,
                };
                var 内容 = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    Padding=new Thickness(1,0),
                    VerticalOptions=LayoutOptions.Center,
                };
                var スピナー = new ActivityIndicator {
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                スピナー.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(セルパス+"."+nameof(ベンチマークプロジェクトセル.実行中表示か)));
                スピナー.SetBinding(IsVisibleProperty,new Binding(セルパス+"."+nameof(ベンチマークプロジェクトセル.実行中表示か)));
                var 状態アイコン = new Image {
                    WidthRequest=14,
                    HeightRequest=14,
                    InputTransparent=true,
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                    Aspect=Aspect.AspectFit,
                };
                状態アイコン.SetBinding(
                    Image.SourceProperty,
                    new Binding(セルパス+"."+nameof(ベンチマークプロジェクトセル.状態画像ソース),converter: new 画像ファイルソースコンバーター()));
                状態アイコン.SetBinding(IsVisibleProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.状態アイコン表示中か));
                状態アイコン.SetBinding(ToolTipProperties.TextProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.実行ツールチップ文字列));
                var 詳細 = new Label {
                    FontSize=ベンチマークグリッド詳細文字寸法,
                    TextColor=Color.FromArgb("#334155"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                詳細.SetBinding(Label.TextProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.実行文字列));
                詳細.SetBinding(Label.TextColorProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.実行文字色));
                詳細.SetBinding(Label.FontAttributesProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.実行フォント属性));
                var 失敗詳細 = new Label {
                    FontSize=ベンチマークグリッド詳細文字寸法,
                    TextColor=Color.FromArgb("#DC2626"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    VerticalTextAlignment=TextAlignment.Center,
                };
                失敗詳細.SetBinding(Label.TextProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.失敗実行文字列));
                失敗詳細.SetBinding(Label.TextColorProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.失敗実行文字色));
                var 詳細ホスト = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Auto },
                    },
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                詳細ホスト.SetBinding(ToolTipProperties.TextProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.実行ツールチップ文字列));
                詳細ホスト.Add(詳細,0,0);
                詳細ホスト.Add(失敗詳細,1,0);
                内容.Add(状態アイコン,0,0);
                内容.Add(スピナー,0,0);
                内容.Add(詳細ホスト,1,0);
                レイアウト.Add(内容,1,1);
                return レイアウト;
            }),
        };
    }

    private DataGridTemplateColumn プロジェクトテキスト列を作成(
        string 見出し,
        string マッピング名,
        string セルパス,
        string バインドパス,
        string 文字色パス,
        ベンチマークグリッドソート列 ソート列,
        double 幅,
        TextAlignment 配置) {
        return new DataGridTemplateColumn {
            MappingName=マッピング名,
            HeaderText=見出し,
            Width=幅,
            AllowSorting=false,
            HeaderTemplate=this.見出しテンプレートを作成(見出し,ソート列),
            CellTemplate=new DataTemplate(() => {
                var グリッド = new Grid {
                    RowDefinitions= {
                        new RowDefinition { Height = new GridLength(2) },
                        new RowDefinition { Height = GridLength.Star },
                        new RowDefinition { Height = new GridLength(2) },
                    },
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(2) },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = new GridLength(2) },
                    },
                    BackgroundColor=Colors.Transparent,
                    VerticalOptions=LayoutOptions.Fill,
                    HorizontalOptions=LayoutOptions.Fill,
                };
                var 実行中背景 = new BoxView {
                    BackgroundColor=Color.FromArgb("#DBEAFE"),
                    Opacity=0.85,
                    InputTransparent=true,
                };
                実行中背景.SetBinding(IsVisibleProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.実行中表示か));
                var ラベル = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#1E293B"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=配置,
                    VerticalTextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0),
                    Padding=new Thickness(2,0),
                };
                ラベル.SetBinding(Label.TextProperty,バインドパス);
                ラベル.SetBinding(Label.TextColorProperty,文字色パス);
                ラベル.SetBinding(Label.FontAttributesProperty,セルパス+"."+nameof(ベンチマークプロジェクトセル.指標フォント属性));
                var ツールチップパス = バインドパス.EndsWith(nameof(ベンチマークプロジェクトセル.時間文字列),StringComparison.Ordinal)
                    ? セルパス+"."+nameof(ベンチマークプロジェクトセル.時間ツールチップ文字列)
                    : バインドパス;
                ラベル.SetBinding(ToolTipProperties.TextProperty,ツールチップパス);
                グリッド.SetBinding(ToolTipProperties.TextProperty,ツールチップパス);
                グリッド.Add(実行中背景,0,0);
                Grid.SetColumnSpan(実行中背景,3);
                Grid.SetRowSpan(実行中背景,3);
                グリッド.Add(ラベル,0,0);
                Grid.SetColumnSpan(ラベル,3);
                Grid.SetRowSpan(ラベル,3);
                this.ホバーセル拡張を有効化(グリッド,ラベル,配置);
                return グリッド;
            }),
        };
    }

    private void チェック中の選択を抑止(View チェックホスト,SfDataGrid グリッド) {
        var ポインター = new PointerGestureRecognizer();
        ポインター.PointerPressed+=(_,_) => {
            this.選択抑止グリッド一覧.Add(グリッド);
            var 選択スナップショット=グリッド.SelectionController is CPUチェック選択コントローラー
                ? グリッド.GetSelectedCells().Cast<object>().ToArray()
                : グリッド.SelectedRows.Cast<object>().ToArray();
            this.チェック選択操作状態一覧[グリッド]=new チェック選択操作状態(++this.チェック選択操作番号,選択スナップショット);
        };
        ポインター.PointerReleased+=(_,_) => {
            if(!this.チェック選択操作状態一覧.TryGetValue(グリッド,out var 状態)) {
                return;
            }
            var 操作番号=状態.操作番号;
            チェックホスト.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100),
                () => this.チェック操作を終了(グリッド,操作番号));
        };
        ポインター.PointerExited+=(_,_) => this.チェック操作を終了(グリッド);
        チェックホスト.GestureRecognizers.Add(ポインター);
    }

    private IReadOnlyList<T>? チェック開始時選択を取得<T>(SfDataGrid グリッド) {
        if(!this.チェック選択操作状態一覧.TryGetValue(グリッド,out var 状態)) {
            return null;
        }
        return 状態.選択スナップショット.OfType<T>().ToArray();
    }

    private void チェック操作を完了(SfDataGrid グリッド) => this.チェック操作を終了(グリッド);

    private void チェック操作を終了(SfDataGrid グリッド,int? 操作番号=null) {
        if(操作番号 is { } 対象操作番号
            &&(!this.チェック選択操作状態一覧.TryGetValue(グリッド,out var 状態)||状態.操作番号!=対象操作番号)) {
            return;
        }
        this.選択抑止グリッド一覧.Remove(グリッド);
        this.チェック選択操作状態一覧.Remove(グリッド);
    }

    private DataGridTemplateColumn テキスト列を作成(string 見出し,string プロパティ,ベンチマークグリッドソート列 ソート列,double 幅,TextAlignment 配置,Color? 背景色 = null) {
        var 解決済み背景色 = 背景色??Color.FromArgb("#F1F5F9");
        return new DataGridTemplateColumn {
            MappingName=プロパティ,
            HeaderText=見出し,
            Width=幅,
            AllowSorting=false,
            HeaderTemplate=this.見出しテンプレートを作成(見出し,ソート列,解決済み背景色),
            CellTemplate=new DataTemplate(() => {
                var グリッド = new Grid {
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var ラベル = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#1E293B"),
                    InputTransparent=true,
                    LineBreakMode=LineBreakMode.TailTruncation,
                    HorizontalTextAlignment=配置,
                    VerticalTextAlignment=TextAlignment.Center,
                    Margin=new Thickness(0),
                    Padding=new Thickness(2,0),
                };
                ラベル.SetBinding(Label.TextProperty,プロパティ);
                ラベル.SetBinding(ToolTipProperties.TextProperty,プロパティ);
                if(プロパティ==nameof(ベンチマーク表行.ベンチマーク型文字列)
                    ||プロパティ==nameof(ベンチマーク表行.ベンチマークメソッド文字列)) {
                    ラベル.SetBinding(Label.TextDecorationsProperty,nameof(ベンチマーク表行.ベンチマーク識別文字装飾));
                }
                グリッド.SetBinding(ToolTipProperties.TextProperty,プロパティ);
                if(プロパティ==nameof(ベンチマーク表行.ベンチマークメソッド文字列)) {
                    グリッド.SetBinding(ToolTipProperties.TextProperty,nameof(ベンチマーク表行.ベンチマーク引数ツールチップ文字列));
                }
                グリッド.Add(ラベル,0,0);
                this.ホバーセル拡張を有効化(グリッド,ラベル,配置);
                return グリッド;
            }),
        };
    }

    private void ホバーセル拡張を有効化(Grid ホスト,Label ラベル,TextAlignment 配置) {
        var ポインター = new PointerGestureRecognizer();
        var 元幅要求 = ラベル.WidthRequest;
        var 元最小幅要求 = ラベル.MinimumWidthRequest;
        var 元背景 = ラベル.BackgroundColor;
        var 元改行モード = ラベル.LineBreakMode;
        var 元水平配置 = ラベル.HorizontalOptions;
        var 元ホスト重なり順 = ホスト.ZIndex;
        var 元ラベル重なり順 = ラベル.ZIndex;
        var 展開済みか = false;

        ポインター.PointerEntered+=(_,_) => {
            if(展開済みか||string.IsNullOrWhiteSpace(ラベル.Text)||ホスト.Width<=0) {
                return;
            }

            var 希望幅 = ラベル.Measure(double.PositiveInfinity,ベンチマークグリッド行高さ).Width
                +ベンチマークホバーセル拡張余白;
            if(希望幅<=ホスト.Width+1) {
                return;
            }

            展開済みか=true;
            ホスト.ZIndex=1000;
            ラベル.ZIndex=1000;
            ラベル.LineBreakMode=LineBreakMode.NoWrap;
            ラベル.WidthRequest=希望幅;
            ラベル.MinimumWidthRequest=希望幅;
            ラベル.BackgroundColor=Colors.White;
            ラベル.HorizontalOptions=配置 switch {
                TextAlignment.End => LayoutOptions.End,
                TextAlignment.Center => LayoutOptions.Center,
                _ => LayoutOptions.Start,
            };
        };
        ポインター.PointerExited+=(_,_) => {
            if(!展開済みか) {
                return;
            }

            展開済みか=false;
            ラベル.WidthRequest=元幅要求;
            ラベル.MinimumWidthRequest=元最小幅要求;
            ラベル.BackgroundColor=元背景;
            ラベル.LineBreakMode=元改行モード;
            ラベル.HorizontalOptions=元水平配置;
            ホスト.ZIndex=元ホスト重なり順;
            ラベル.ZIndex=元ラベル重なり順;
        };
        ホスト.GestureRecognizers.Add(ポインター);
    }

    private DataTemplate 見出しテンプレートを作成(string テキスト,ベンチマークグリッドソート列 ソート列) =>
        this.見出しテンプレートを作成(テキスト,ソート列,Color.FromArgb("#F1F5F9"));

    private DataTemplate 見出しテンプレートを作成(string テキスト,ベンチマークグリッドソート列 ソート列,Color 背景色) {
        return new DataTemplate(() => {
            var グリッド = new Grid {
                ColumnDefinitions= {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(12) },
                },
                BackgroundColor=背景色,
                Padding=new Thickness(2,0),
            };
            var ラベル = new Label {
                Text=テキスト,
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                HorizontalOptions=LayoutOptions.Fill,
                HorizontalTextAlignment=TextAlignment.Start,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
            };
            var アイコン = new Image {
                WidthRequest=10,
                HeightRequest=10,
                Aspect=Aspect.AspectFit,
                VerticalOptions=LayoutOptions.Center,
                HorizontalOptions=LayoutOptions.Center,
            };
            void アイコンを更新() {
                if(this.ビューモデル is null||this.ビューモデル.現在ソート列!=ソート列) {
                    アイコン.IsVisible=false;
                    return;
                }

                アイコン.IsVisible=true;
                アイコン.Source=ImageSource.FromFile(this.ビューモデル.現在ソート降順か ? "sort_desc.png" : "sort_asc.png");
            }

            アイコンを更新();
            if(this.ビューモデル is not null) {
                PropertyChangedEventHandler ハンドラー = (_,イベント引数) => {
                    if(イベント引数.PropertyName==nameof(実行ビューモデル.現在ソート列)
                        ||イベント引数.PropertyName==nameof(実行ビューモデル.現在ソート降順か)) {
                        起動診断.メインスレッドで呼び出し開始("ベンチマーク状態アイコン更新",アイコンを更新);
                    }
                };
                this.ビューモデル.PropertyChanged+=ハンドラー;
                グリッド.Unloaded+=(_,_) => this.ビューモデル.PropertyChanged-=ハンドラー;
            }

            グリッド.Add(ラベル,0,0);
            グリッド.Add(アイコン,1,0);
            var 左タップ = new TapGestureRecognizer {
                Buttons=ButtonsMask.Primary,
            };
            左タップ.Tapped+=(_,_) => this.ビューモデル?.ベンチマーク行をソート(ソート列);
            グリッド.GestureRecognizers.Add(左タップ);
            var 右タップ = new TapGestureRecognizer {
                Buttons=ButtonsMask.Secondary,
            };
            右タップ.Tapped+=(_,_) => this.ベンチマークソートダイアログを表示();
            グリッド.GestureRecognizers.Add(右タップ);
            return グリッド;
        });
    }

    private View CPUビューを作成() {
        var 高性能コア一覧 = this.CPU区分一覧を作成(nameof(実行ビューモデル.高性能CPU区分一覧));
        var 高効率コア一覧 = this.CPU区分一覧を作成(nameof(実行ビューモデル.高効率CPU区分一覧));
        var 不明コア一覧 = this.CPU区分一覧を作成(nameof(実行ビューモデル.不明CPU区分一覧));

        var レイアウト = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            RowSpacing=10,
            ColumnSpacing=10,
            Children= {
                高性能コア一覧,
                高効率コア一覧,
                不明コア一覧,
            },
        };
        Grid.SetColumn(高効率コア一覧,1);
        Grid.SetRow(不明コア一覧,1);
        Grid.SetColumnSpan(不明コア一覧,2);
        return レイアウト;
    }

    private View ログビューを作成() {
        var ログ一覧 = new CollectionView {
            SelectionMode=SelectionMode.None,
            VerticalOptions=LayoutOptions.Fill,
            HorizontalOptions=LayoutOptions.Fill,
            ItemTemplate=new DataTemplate(() => {
                var ラベル = new Label {
                    FontSize=11,
                    TextColor=Color.FromArgb("#0F172A"),
                    LineBreakMode=LineBreakMode.WordWrap,
                    Margin=new Thickness(0),
                    Padding=new Thickness(0,2),
                };
                ラベル.SetBinding(Label.TextProperty,".");
                ラベル.BindingContextChanged+=(_,_) => {
                    var 強調中か = ラベル.BindingContext is string テキスト
                        &&テキスト.StartsWith(実行ビューモデル.強調ログ項目接頭辞,StringComparison.Ordinal);
                    ラベル.FontAttributes=強調中か ? FontAttributes.Bold : FontAttributes.None;
                    ラベル.TextColor=強調中か ? Color.FromArgb("#DC2626") : Color.FromArgb("#0F172A");
                };
                return ラベル;
            }),
            ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Vertical) {
                ItemSpacing=0,
            },
        };
        ログ一覧.SetBinding(ItemsView.ItemsSourceProperty,nameof(実行ビューモデル.ログ項目一覧));

        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=ログ一覧,
        };
    }

    private View 改善プロンプトビューを作成() {
        var コピーボタン = new Button {
            Text="⧉",
            WidthRequest=30,
            HeightRequest=28,
            Padding=new Thickness(0),
            FontSize=14,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Color.FromArgb("#E2E8F0"),
        };
        コピーボタン.SetBinding(Button.CommandProperty,nameof(実行ビューモデル.改善プロンプトコピーコマンド));
        コピーボタン.SetBinding(IsEnabledProperty,nameof(実行ビューモデル.改善プロンプトをコピー可能か));
        ToolTipProperties.SetText(コピーボタン,"改善プロンプトをコピー");
        押下フィードバックを設定(コピーボタン);

        var タイトル = new Label {
            Text="改善プロンプト",
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        var 見出し = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=6,
            Padding=new Thickness(0,0,0,6),
        };
        見出し.Add(コピーボタン,0,0);
        見出し.Add(タイトル,1,0);

        var プロンプト文 = new Editor {
            FontSize=11,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Colors.White,
            AutoSize=EditorAutoSizeOption.Disabled,
            IsSpellCheckEnabled=false,
            IsTextPredictionEnabled=false,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        プロンプト文.SetBinding(Editor.TextProperty,new Binding(nameof(実行ビューモデル.改善プロンプト文字列),BindingMode.TwoWay));
        var プロンプト境界 = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Colors.White,
            Padding=new Thickness(6),
            Content=プロンプト文,
        };
        var 内容 = new Grid {
            RowDefinitions= {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
            },
            RowSpacing=4,
        };
        内容.Add(見出し,0,0);
        内容.Add(プロンプト境界,0,1);
        return new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(8),
            Content=内容,
        };
    }

    private VerticalStackLayout CPU区分一覧を作成(string 項目ソースパス) {
        var レイアウト = new VerticalStackLayout {
            Spacing=10,
        };
        BindableLayout.SetItemTemplate(レイアウト,new DataTemplate(this.CPU区分ビューを作成));
        レイアウト.SetBinding(BindableLayout.ItemsSourceProperty,項目ソースパス);
        return レイアウト;
    }

    private View CPU区分ビューを作成() {
        var 見出し = new Border {
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Padding=new Thickness(6,3),
        };
        var 見出しグリッド = new Grid {
            ColumnDefinitions= {
                new ColumnDefinition { Width = new GridLength(CPU論理チェック列幅) },
                new ColumnDefinition { Width = new GridLength(24) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing=4,
            HorizontalOptions=LayoutOptions.Fill,
        };
        var 区分チェック = this.CPUチェックボックスを作成();
        区分チェック.InputTransparent=true;
        区分チェック.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CPU表区分.状態),BindingMode.OneWay,converter: new CPU選択状態真偽値コンバーター()));
        var 区分チェックホスト = new Grid {
            BackgroundColor=Colors.Transparent,
            HorizontalOptions=LayoutOptions.Fill,
            VerticalOptions=LayoutOptions.Fill,
        };
        区分チェックホスト.Add(区分チェック,0,0);
        区分チェックホスト.GestureRecognizers.Add(new TapGestureRecognizer {
            Command=new Command(() => {
                if(見出し.BindingContext is CPU表区分 区分) {
                    this.ビューモデル?.CPU区分を切り替え(区分);
                }
            }),
        });
        var バッジ = this.コアバッジを作成();
        バッジ.SetBinding(BackgroundColorProperty,nameof(CPU表区分.バッジ背景色));
        if(バッジ.Content is Image バッジ画像) {
            バッジ画像.SetBinding(
                Image.SourceProperty,
                new Binding(nameof(CPU表区分.バッジ画像ソース),converter: new 画像ファイルソースコンバーター()));
        }
        var タイトル = new Label {
            FontSize=12,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            VerticalTextAlignment=TextAlignment.Center,
            LineBreakMode=LineBreakMode.TailTruncation,
        };
        タイトル.SetBinding(Label.TextProperty,nameof(CPU表区分.タイトル));
        見出しグリッド.Add(区分チェックホスト,0,0);
        見出しグリッド.Add(バッジ,1,0);
        見出しグリッド.Add(タイトル,2,0);
        見出し.Content=見出しグリッド;

        起動診断.Syncfusionアセンブリを書き込む("CPU区分ビュー作成前のSfDataGrid");
        var CPUグリッド = new SfDataGrid {
            AutoGenerateColumnsMode=AutoGenerateColumnsMode.None,
            RowHeight=CPUグリッド行高さ,
            HeaderRowHeight=CPUグリッド見出し行高さ,
            AllowTriStateSorting=false,
            SortingMode=DataGridSortingMode.None,
            SelectionMode=DataGridSelectionMode.Extended,
            SelectionUnit=DataGridSelectionUnit.Cell,
            NavigationMode=DataGridNavigationMode.Cell,
            AllowKeyboardNavigation=true,
            AllowSelectionOnPress=true,
            GridLinesVisibility=GridLinesVisibility.Both,
            DefaultColumnWidth=56,
        };
        this.データグリッド更新バッチ対象を登録(CPUグリッド);
        this.データグリッド線を構成(CPUグリッド,セルフォーカスを表示するか: true);
        var 区分レイアウト = new VerticalStackLayout {
            Spacing=3,
            Children= {
                見出し,
                CPUグリッド,
            },
        };
        CPUグリッド.SetBinding(SfDataGrid.ItemsSourceProperty,nameof(CPU表区分.行一覧));
        void CPUグリッドバインドコンテキストを構成() {
            if(CPUグリッド.BindingContext is not CPU表区分 区分) {
                return;
            }

            CPUグリッド.AutomationId="CPU.グリッド."+区分.コア種別;
            this.CPUデータグリッド列を再構築(CPUグリッド,区分);
            var 選択コントローラー = new CPUチェック選択コントローラー(
                CPUグリッド,
                CPU一覧 => this.ビューモデル?.CPUチェックを切り替え(CPU一覧));
            CPUグリッド.SelectionController=選択コントローラー;
            グリッドキーボードフォーカス動作.取り付ける(CPUグリッド);
            CPUグリッド.HeightRequest=CPU区分グリッド見出し高さ+(区分.行一覧.Count*CPU区分行高さ)+CPU区分グリッド追加高さ;
            if(区分.コア種別==CPUコア種別.高性能) {
                var 最大幅 = this.CPU右端幅を取得(区分);
                CPUグリッド.MaximumWidthRequest=最大幅;
                CPUグリッド.WidthRequest=最大幅;
                CPUグリッド.HorizontalOptions=LayoutOptions.Start;
                見出し.WidthRequest=最大幅;
                見出し.MaximumWidthRequest=最大幅;
                見出し.HorizontalOptions=LayoutOptions.Start;
                区分レイアウト.WidthRequest=最大幅;
                区分レイアウト.MaximumWidthRequest=最大幅;
                区分レイアウト.HorizontalOptions=LayoutOptions.Start;
            } else if(区分.コア種別==CPUコア種別.高効率) {
                var 最大幅 = this.CPU表示幅を取得(区分,4);
                CPUグリッド.MaximumWidthRequest=最大幅;
                CPUグリッド.WidthRequest=最大幅;
                CPUグリッド.HorizontalOptions=LayoutOptions.Start;
                見出し.WidthRequest=最大幅;
                見出し.MaximumWidthRequest=最大幅;
                見出し.HorizontalOptions=LayoutOptions.Start;
                区分レイアウト.WidthRequest=最大幅;
                区分レイアウト.MaximumWidthRequest=最大幅;
                区分レイアウト.HorizontalOptions=LayoutOptions.Start;
            } else {
                CPUグリッド.HorizontalOptions=LayoutOptions.Fill;
                見出し.ClearValue(WidthRequestProperty);
                見出し.ClearValue(MaximumWidthRequestProperty);
                見出し.HorizontalOptions=LayoutOptions.Fill;
                区分レイアウト.ClearValue(WidthRequestProperty);
                区分レイアウト.ClearValue(MaximumWidthRequestProperty);
                区分レイアウト.HorizontalOptions=LayoutOptions.Fill;
            }
        }
        CPUグリッド.BindingContextChanged+=(_,_) => CPUグリッドバインドコンテキストを構成();
        CPUグリッドバインドコンテキストを構成();
        CPUグリッド.SelectionChanging+=(_,イベント引数) => {
            if(this.選択抑止グリッド一覧.Contains(CPUグリッド)) {
                イベント引数.Cancel=true;
                return;
            }
            if(イベント引数.AddedRows is { } 追加行一覧) {
                for(var インデックス = 追加行一覧.Count-1;インデックス>=0;インデックス--) {
                    if(追加行一覧[インデックス] is SelectedCellInfo セル&&!CPUチェック選択コントローラー.選択可能なCPUセルか(セル)) {
                        追加行一覧.RemoveAt(インデックス);
                    }
                }
            }
        };

        return 区分レイアウト;
    }

    private void CPUデータグリッド列を再構築(SfDataGrid グリッド,CPU表区分 区分) {
        グリッド.Columns.Clear();
        グリッド.Columns.Add(this.CPUコア列を作成(グリッド,区分));
        foreach(var 列 in 区分.列一覧) {
            グリッド.Columns.Add(this.CPU論理列を作成(グリッド,列));
        }
    }

    private DataGridTemplateColumn CPUコア列を作成(SfDataGrid グリッド,CPU表区分 区分) {
        return new DataGridTemplateColumn {
            MappingName=nameof(CPU表行.ラベル),
            HeaderText="コア",
            Width=this.CPUコア列幅を取得(区分),
            AllowSorting=false,
            AllowFocus=true,
            HeaderTemplate=this.CPU単純見出しテンプレートを作成("コア"),
            CellTemplate=new DataTemplate(() => {
                var レイアウト = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(CPU論理チェック列幅) },
                        new ColumnDefinition { Width = new GridLength(24) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=4,
                    Padding=new Thickness(2,0),
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                var チェック = this.CPUチェックボックスを作成();
                チェック.AutomationId="CPU.コア行チェック";
                チェック.InputTransparent=true;
                チェック.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CPU表行.チェック済みか),BindingMode.OneWay));
                var チェックホスト = new Grid {
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                チェックホスト.Add(チェック,0,0);
                var チェックタップ = new TapGestureRecognizer();
                チェックタップ.Tapped+=(_,_) => {
                    try {
                        if(レイアウト.BindingContext is CPU表行 行) {
                            if(グリッド.SelectionController is CPUチェック選択コントローラー 選択コントローラー) {
                                選択コントローラー.コアチェッククリックを処理(行,this.チェック開始時選択を取得<SelectedCellInfo>(グリッド));
                            } else {
                                this.ビューモデル?.CPU行を切り替え(行);
                            }
                        }
                    } finally {
                        this.チェック操作を完了(グリッド);
                    }
                };
                チェックホスト.GestureRecognizers.Add(チェックタップ);
                this.チェック中の選択を抑止(チェックホスト,グリッド);
                var バッジ = this.コアバッジを作成();
                バッジ.SetBinding(BackgroundColorProperty,nameof(CPU表行.バッジ背景色));
                if(バッジ.Content is Image バッジ画像) {
                    バッジ画像.SetBinding(
                        Image.SourceProperty,
                        new Binding(nameof(CPU表行.バッジ画像ソース),converter: new 画像ファイルソースコンバーター()));
                }
                var ラベル = new Label {
                    FontSize=CPUグリッドセル文字寸法,
                    TextColor=Color.FromArgb("#334155"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                ラベル.SetBinding(Label.TextProperty,nameof(CPU表行.ラベル));
                レイアウト.Add(チェックホスト,0,0);
                レイアウト.Add(バッジ,1,0);
                レイアウト.Add(ラベル,2,0);
                return レイアウト;
            }),
        };
    }

    private double CPUコア列幅を取得(CPU表区分 区分) {
        var ラベル幅 = 区分.行一覧.Count==0
            ? 24
            : 区分.行一覧.Max(static 行 => 行.ラベル.Length)*7;
        var 基本幅 = Math.Max(180,CPU論理チェック列幅+24+ラベル幅+42);
        return 区分.コア種別 switch {
            CPUコア種別.高性能 or CPUコア種別.高効率 => Math.Max(1,基本幅*0.65),
            _ => 基本幅,
        };
    }

    private double CPU右端幅を取得(CPU表区分 区分) {
        return this.CPUコア列幅を取得(区分)
            +区分.列一覧.Count*CPU論理列幅
            +2*CPU論理チェック列幅;
    }

    private double CPU表示幅を取得(CPU表区分 区分,int 論理列数) {
        return this.CPUコア列幅を取得(区分)
            +Math.Min(区分.列一覧.Count,論理列数)*CPU論理列幅
            +2*CPU論理チェック列幅;
    }

    private DataGridTemplateColumn CPU論理列を作成(SfDataGrid グリッド,CPU表列 列) {
        var セルパス = nameof(CPU表行.グリッドセル一覧)+"["+列.インデックス+"]";
        return new DataGridTemplateColumn {
            MappingName="CPU"+列.インデックス,
            HeaderText=列.タイトル,
            Width=CPU論理列幅,
            AllowSorting=false,
            HeaderTemplate=this.CPU列見出しテンプレートを作成(列),
            CellTemplate=new DataTemplate(() => {
                var レイアウト = new Grid {
                    ColumnDefinitions= {
                        new ColumnDefinition { Width = new GridLength(CPU論理チェック列幅) },
                        new ColumnDefinition { Width = new GridLength(18) },
                        new ColumnDefinition { Width = new GridLength(24) },
                        new ColumnDefinition { Width = new GridLength(CPU論理番号幅) },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing=2,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Center,
                };
                レイアウト.SetBinding(IsVisibleProperty,new Binding(セルパス,converter: new ヌル判定コンバーター()));
                var チェック = this.CPUチェックボックスを作成();
                チェック.AutomationId="CPU.セルチェック";
                チェック.InputTransparent=true;
                チェック.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(セルパス+"."+nameof(CPU表セル.選択中か),BindingMode.OneWay));
                var チェックホスト = new Grid {
                    BackgroundColor=Colors.Transparent,
                    HorizontalOptions=LayoutOptions.Fill,
                    VerticalOptions=LayoutOptions.Fill,
                };
                チェックホスト.Add(チェック,0,0);
                var チェックタップ = new TapGestureRecognizer();
                チェックタップ.Tapped+=(_,_) => {
                    try {
                        if(レイアウト.BindingContext is CPU表行 行
                            &&列.インデックス<行.グリッドセル一覧.Count
                            &&行.グリッドセル一覧[列.インデックス] is not null
                            &&グリッド.SelectionController is CPUチェック選択コントローラー 選択コントローラー) {
                            選択コントローラー.チェッククリックを処理(行,列.インデックス,this.チェック開始時選択を取得<SelectedCellInfo>(グリッド));
                        }
                    } finally {
                        this.チェック操作を完了(グリッド);
                    }
                };
                チェックホスト.GestureRecognizers.Add(チェックタップ);
                this.チェック中の選択を抑止(チェックホスト,グリッド);
                var スピナー = new ActivityIndicator {
                    WidthRequest=14,
                    HeightRequest=14,
                    Color=Color.FromArgb("#2563EB"),
                    VerticalOptions=LayoutOptions.Center,
                    HorizontalOptions=LayoutOptions.Center,
                };
                スピナー.SetBinding(ActivityIndicator.IsRunningProperty,new Binding(セルパス+"."+nameof(CPU表セル.ジョブ実行中か)));
                スピナー.SetBinding(IsVisibleProperty,new Binding(セルパス+"."+nameof(CPU表セル.ジョブ実行中か)));
                var 状態アイコン = new Image {
                    WidthRequest=14,
                    HeightRequest=14,
                    InputTransparent=true,
                    HorizontalOptions=LayoutOptions.Center,
                    VerticalOptions=LayoutOptions.Center,
                    Aspect=Aspect.AspectFit,
                };
                状態アイコン.SetBinding(
                    Image.SourceProperty,
                    new Binding(セルパス+"."+nameof(CPU表セル.状態画像ソース),converter: new 画像ファイルソースコンバーター()));
                状態アイコン.SetBinding(IsVisibleProperty,セルパス+"."+nameof(CPU表セル.状態画像表示中か));
                var バッジ = this.コアバッジを作成();
                バッジ.SetBinding(BackgroundColorProperty,セルパス+"."+nameof(CPU表セル.バッジ背景色));
                if(バッジ.Content is Image バッジ画像) {
                    バッジ画像.SetBinding(
                        Image.SourceProperty,
                        new Binding(セルパス+"."+nameof(CPU表セル.コア種別画像ソース),converter: new 画像ファイルソースコンバーター()));
                }
                var 番号ラベル = new Label {
                    FontSize=CPUグリッドセル文字寸法,
                    TextColor=Color.FromArgb("#0F172A"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                番号ラベル.SetBinding(Label.TextProperty,セルパス+"."+nameof(CPU表セル.論理プロセッサ文字列));
                番号ラベル.SetBinding(ToolTipProperties.TextProperty,セルパス+"."+nameof(CPU表セル.論理プロセッサ文字列));
                var 状態ラベル = new Label {
                    FontSize=10,
                    TextColor=Color.FromArgb("#334155"),
                    HorizontalTextAlignment=TextAlignment.Start,
                    VerticalTextAlignment=TextAlignment.Center,
                    LineBreakMode=LineBreakMode.TailTruncation,
                };
                状態ラベル.SetBinding(Label.TextProperty,セルパス+"."+nameof(CPU表セル.セル状態文));
                状態ラベル.SetBinding(ToolTipProperties.TextProperty,セルパス+"."+nameof(CPU表セル.表示文字列));
                レイアウト.SetBinding(ToolTipProperties.TextProperty,セルパス+"."+nameof(CPU表セル.表示文字列));
                レイアウト.Add(チェックホスト,0,0);
                レイアウト.Add(状態アイコン,1,0);
                レイアウト.Add(スピナー,1,0);
                レイアウト.Add(バッジ,2,0);
                レイアウト.Add(番号ラベル,3,0);
                レイアウト.Add(状態ラベル,4,0);
                return レイアウト;
            }),
        };
    }

    private DataTemplate CPU単純見出しテンプレートを作成(string テキスト) {
        return new DataTemplate(() => new Label {
            Text=テキスト,
            FontSize=10,
            FontAttributes=FontAttributes.Bold,
            TextColor=Color.FromArgb("#0F172A"),
            BackgroundColor=Color.FromArgb("#F1F5F9"),
            HorizontalOptions=LayoutOptions.Fill,
            HorizontalTextAlignment=TextAlignment.Start,
            VerticalTextAlignment=TextAlignment.Center,
            Padding=new Thickness(2,0),
        });
    }

    private DataTemplate CPU列見出しテンプレートを作成(CPU表列 列) {
        return new DataTemplate(() => {
            var レイアウト = new Grid {
                ColumnDefinitions= {
                    new ColumnDefinition { Width = new GridLength(CPU論理チェック列幅) },
                    new ColumnDefinition { Width = GridLength.Star },
                },
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
                BackgroundColor=Color.FromArgb("#F1F5F9"),
            };
            var チェック = this.CPUチェックボックスを作成();
            チェック.InputTransparent=true;
            チェック.SetBinding(SfCheckBox.IsCheckedProperty,new Binding(nameof(CPU表列.チェック済みか),BindingMode.OneWay,source: 列));
            var チェックホスト = new Grid {
                BackgroundColor=Colors.Transparent,
                HorizontalOptions=LayoutOptions.Fill,
                VerticalOptions=LayoutOptions.Fill,
            };
            チェックホスト.Add(チェック,0,0);
            レイアウト.Add(チェックホスト,0,0);
            レイアウト.Add(new Label {
                Text="論理コア",
                FontSize=10,
                FontAttributes=FontAttributes.Bold,
                TextColor=Color.FromArgb("#0F172A"),
                HorizontalTextAlignment=TextAlignment.Start,
                VerticalTextAlignment=TextAlignment.Center,
                LineBreakMode=LineBreakMode.TailTruncation,
                Padding=new Thickness(2,0),
            },1,0);
            var タップ = new TapGestureRecognizer();
            タップ.Tapped+=(_,_) => {
                if(列.区分 is { } 区分) {
                    this.ビューモデル?.CPU列を切り替え(区分,列.インデックス);
                }
            };
            チェックホスト.GestureRecognizers.Add(タップ);
            return レイアウト;
        });
    }

    private SfCheckBox CPUチェックボックスを作成() =>
        this.Syncfusionチェックボックスを作成(CPUチェックボックス幅,CPUチェックボックス高さ);

    private SfCheckBox Syncfusionチェックボックスを作成(double 幅,double 高さ,bool 三状態か = false) {
        return new SfCheckBox {
            WidthRequest=幅,
            HeightRequest=高さ,
            MinimumWidthRequest=幅,
            MinimumHeightRequest=高さ,
            IsThreeState=三状態か,
            HorizontalOptions=LayoutOptions.Center,
            VerticalOptions=LayoutOptions.Center,
            Margin=new Thickness(0),
        };
    }

    private void データグリッド線を構成(SfDataGrid グリッド,bool 行フォーカスを表示するか = false,bool セルフォーカスを表示するか = false) {
        var 線色 = Color.FromArgb("#CBD5E1");
        var スタイル = new DataGridStyle {
            GridLineColor=線色,
            HeaderGridLineColor=線色,
            GridLineStrokeThickness=1,
            HeaderGridLineStrokeThickness=1,
        };
        if(行フォーカスを表示するか) {
            スタイル.CurrentRowHighlightColor=Color.FromArgb("#7C3AED");
            スタイル.CurrentCellBorderWidth=2;
        } else if(セルフォーカスを表示するか) {
            スタイル.CurrentCellBorderColor=Color.FromArgb("#7C3AED");
            スタイル.CurrentCellBorderWidth=2;
        }
        グリッド.DefaultStyle=スタイル;
    }

    private void データグリッドスクロールバーを構成(SfDataGrid グリッド) {
        void 適用() {
            var 表示コンテナー = グリッド.GetVisualContainer();
            if(表示コンテナー is null) {
                return;
            }

            this.データグリッドスクロールバー寸法を適用(表示コンテナー);
            this.メンバー呼び出しを試行(表示コンテナー,"UpdateScrollBars");
            表示コンテナー.InvalidateMeasure();
        }

        void 安全に適用() {
            try {
                適用();
            } catch(Exception 例外) {
                起動診断.書き込む("ベンチマークグリッドのスクロールバー寸法適用に失敗しました。",例外);
            }
        }

        async void 即時および遅延適用() {
            安全に適用();
            await Task.Delay(100);
            起動診断.メインスレッドで呼び出し開始("ベンチマークグリッドスクロールバー適用",安全に適用);
        }

        グリッド.Loaded+=(_,_) => 即時および遅延適用();
        グリッド.HandlerChanged+=(_,_) => 起動診断.メインスレッドで呼び出し開始("ベンチマークグリッドスクロールバーハンドラー変更",即時および遅延適用);
        グリッド.SizeChanged+=(_,_) => 起動診断.メインスレッドで呼び出し開始("ベンチマークグリッドスクロールバー寸法変更",安全に適用);
    }

    private void データグリッドスクロールバー寸法を適用(object 表示コンテナー) {
        this.メンバー値設定を試行(表示コンテナー,"ScrollerWidth",ベンチマークスクロールバー寸法);
        this.メンバー値設定を試行(表示コンテナー,"ScrollerHeight",ベンチマークスクロールバー寸法);

        if(this.メンバー値取得を試行(表示コンテナー,"HorizontalScrollbar") is { } 水平スクロールバー) {
            this.メンバー値設定を試行(水平スクロールバー,"Height",ベンチマークスクロールバー寸法);
            this.メンバー値設定を試行(水平スクロールバー,"HeightRequest",ベンチマークスクロールバー寸法);
            this.メンバー値設定を試行(水平スクロールバー,"MinimumHeightRequest",ベンチマークスクロールバー寸法);
            this.メンバー値設定を試行(水平スクロールバー,"ScrollerHeight",ベンチマークスクロールバー寸法);
            this.スクロールバー常時表示を適用(水平スクロールバー);
        }

        if(this.メンバー値取得を試行(表示コンテナー,"VerticalScrollbar") is { } 垂直スクロールバー) {
            this.メンバー値設定を試行(垂直スクロールバー,"Width",ベンチマークスクロールバー寸法);
            this.メンバー値設定を試行(垂直スクロールバー,"WidthRequest",ベンチマークスクロールバー寸法);
            this.メンバー値設定を試行(垂直スクロールバー,"MinimumWidthRequest",ベンチマークスクロールバー寸法);
            this.メンバー値設定を試行(垂直スクロールバー,"ScrollerWidth",ベンチマークスクロールバー寸法);
            this.スクロールバー常時表示を適用(垂直スクロールバー);
        }
    }

    private void スクロールバー常時表示を適用(object スクロールバー) {
        // ScrollBarVisibility.Always を設定してもWinUI側のオートハイドで薄く消えるため、
        // 内部スクロールバーの可視性関連プロパティを存在するものだけベストエフォートで強制する。
        this.メンバー値設定を試行(スクロールバー,"IsVisible",true);
        this.メンバー値設定を試行(スクロールバー,"AutoHide",false);
        this.メンバー値設定を試行(スクロールバー,"Opacity",1d);
    }

    private object? メンバー値取得を試行(object 対象,string 名前) {
        const System.Reflection.BindingFlags フラグ = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var プロパティ = 対象.GetType().GetProperty(名前,フラグ);
        if(プロパティ is not null&&プロパティ.CanRead) {
            return プロパティ.GetValue(対象);
        }

        return 対象.GetType().GetField(名前,フラグ)?.GetValue(対象);
    }

    private void メンバー値設定を試行(object 対象,string 名前,double 値) {
        const System.Reflection.BindingFlags フラグ = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var プロパティ = 対象.GetType().GetProperty(名前,フラグ);
        if(プロパティ is not null&&プロパティ.CanWrite) {
            プロパティ.SetValue(対象,this.メンバー値を変換(値,プロパティ.PropertyType));
            return;
        }

        var フィールド = 対象.GetType().GetField(名前,フラグ);
        フィールド?.SetValue(対象,this.メンバー値を変換(値,フィールド.FieldType));
    }

    private void メンバー値設定を試行(object 対象,string 名前,bool 値) {
        const System.Reflection.BindingFlags フラグ = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var プロパティ = 対象.GetType().GetProperty(名前,フラグ);
        if(プロパティ is not null&&プロパティ.CanWrite&&プロパティ.PropertyType==typeof(bool)) {
            プロパティ.SetValue(対象,値);
            return;
        }

        var フィールド = 対象.GetType().GetField(名前,フラグ);
        if(フィールド is not null&&フィールド.FieldType==typeof(bool)) {
            フィールド.SetValue(対象,値);
        }
    }

    private object メンバー値を変換(double 値,Type 型) {
        var 対象型 = Nullable.GetUnderlyingType(型)??型;
        if(対象型==typeof(double)) {
            return 値;
        }

        if(対象型==typeof(float)) {
            return (float)値;
        }

        if(対象型==typeof(int)) {
            return (int)値;
        }

        return Convert.ChangeType(値,対象型);
    }

    private void メンバー呼び出しを試行(object 対象,string 名前,params object[] 引数一覧) {
        const System.Reflection.BindingFlags フラグ = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
        var メソッド = 対象.GetType()
            .GetMethods(フラグ)
            .FirstOrDefault(項目 => 項目.Name==名前&&項目.GetParameters().Length==引数一覧.Length);
        メソッド?.Invoke(対象,引数一覧);
    }

    private Border コアバッジを作成() {
        return new Border {
            WidthRequest=20,
            HeightRequest=18,
            Stroke=Color.FromArgb("#CBD5E1"),
            StrokeThickness=1,
            Padding=new Thickness(0),
            Content=new Image {
                WidthRequest=12,
                HeightRequest=12,
                Aspect=Aspect.AspectFit,
                HorizontalOptions=LayoutOptions.Center,
                VerticalOptions=LayoutOptions.Center,
            },
        };
    }
}
public sealed class ベンチマーク円形進行ビュー:GraphicsView {
    public static readonly BindableProperty 進行プロパティ = BindableProperty.Create(
        nameof(進行度),
        typeof(double),
        typeof(ベンチマーク円形進行ビュー),
        0d,
        propertyChanged: static (バインド可能オブジェクト,_,_) => ((ベンチマーク円形進行ビュー)バインド可能オブジェクト).Invalidate());

    public static readonly BindableProperty 進行色プロパティ = BindableProperty.Create(
        nameof(進行色),
        typeof(Color),
        typeof(ベンチマーク円形進行ビュー),
        既定進行色,
        propertyChanged: static (バインド可能オブジェクト,_,_) => ((ベンチマーク円形進行ビュー)バインド可能オブジェクト).Invalidate());

    public static readonly BindableProperty 輪郭色プロパティ = BindableProperty.Create(
        nameof(輪郭色),
        typeof(Color),
        typeof(ベンチマーク円形進行ビュー),
        既定輪郭色,
        propertyChanged: static (バインド可能オブジェクト,_,_) => ((ベンチマーク円形進行ビュー)バインド可能オブジェクト).Invalidate());

    private static readonly Color トラック色 = Color.FromArgb("#CBD5E1");
    private static readonly Color 既定進行色 = Color.FromArgb("#F97316");
    private static readonly Color 既定輪郭色 = Color.FromArgb("#64748B");

    public ベンチマーク円形進行ビュー() {
        this.Drawable=new ベンチマーク円形進行描画(this);
        this.Loaded+=(_,_) => this.Invalidate();
        this.SizeChanged+=(_,_) => this.Invalidate();
    }

    public double 進行度 {
        get => (double)this.GetValue(進行プロパティ);
        set => this.SetValue(進行プロパティ,value);
    }

    public Color 進行色 {
        get => (Color)this.GetValue(進行色プロパティ);
        set => this.SetValue(進行色プロパティ,value);
    }

    public Color 輪郭色 {
        get => (Color)this.GetValue(輪郭色プロパティ);
        set => this.SetValue(輪郭色プロパティ,value);
    }

    private sealed class ベンチマーク円形進行描画(ベンチマーク円形進行ビュー 所有者):IDrawable {
        public void Draw(ICanvas キャンバス,RectF 描画領域) {
            var 寸法 = MathF.Min(描画領域.Width,描画領域.Height);
            if(寸法<=0) {
                return;
            }

            var 線幅 = MathF.Max(1.25f,寸法*0.11f);
            var 半径 = MathF.Max(0,寸法/2f-線幅/2f);
            var 中心横座標 = 描画領域.X+描画領域.Width/2f;
            var 中心縦座標 = 描画領域.Y+描画領域.Height/2f;
            キャンバス.FillColor=トラック色;
            キャンバス.FillCircle(中心横座標,中心縦座標,半径);

            var 進行 = Math.Clamp(所有者.進行度,0,1);
            if(進行>0) {
                キャンバス.FillColor=所有者.進行色;
                if(進行>=1) {
                    キャンバス.FillCircle(中心横座標,中心縦座標,半径);
                } else {
                    キャンバス.FillPath(進行パスを作成(中心横座標,中心縦座標,半径,進行));
                }
            }

            キャンバス.StrokeColor=所有者.輪郭色;
            キャンバス.StrokeSize=線幅;
            キャンバス.DrawCircle(中心横座標,中心縦座標,半径);
            if(進行<=0) {
                return;
            }
        }

        private static PathF 進行パスを作成(float 中心横座標,float 中心縦座標,float 半径,double 進行) {
            var パス = new PathF();
            var 開始角度 = -90d;
            var 掃引角 = 360d*進行;
            var 分割数 = Math.Max(2,(int)Math.Ceiling(掃引角/12d));
            パス.MoveTo(中心横座標,中心縦座標);
            for(var インデックス = 0;インデックス<=分割数;インデックス++) {
                var 角度 = (float)((開始角度+(掃引角*インデックス/分割数))*Math.PI/180d);
                パス.LineTo(
                    中心横座標+(MathF.Cos(角度)*半径),
                    中心縦座標+(MathF.Sin(角度)*半径));
            }
            パス.Close();
            return パス;
        }
    }

}

public sealed class ヌル判定コンバーター:IValueConverter {
    public object Convert(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        return 値 is not null;
    }

    public object ConvertBack(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        throw new NotSupportedException();
    }
}

public sealed class 有効時不透明度コンバーター:IValueConverter {
    public object Convert(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        return 値 is true ? 1d : 0.35d;
    }

    public object ConvertBack(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        throw new NotSupportedException();
    }
}

public sealed class 画像ファイルソースコンバーター:IValueConverter {
    public object? Convert(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        return 値 is string パス&&!string.IsNullOrWhiteSpace(パス)
            ? ImageSource.FromFile(パス)
            : null;
    }

    public object ConvertBack(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        throw new NotSupportedException();
    }
}

public sealed class CPU選択状態真偽値コンバーター:IValueConverter {
    public object Convert(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        return 値 is CPU選択状態.チェック済み;
    }

    public object ConvertBack(object? 値,Type 対象型,object? パラメーター,System.Globalization.CultureInfo カルチャー) {
        throw new NotSupportedException();
    }
}
