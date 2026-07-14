using System.Text;

namespace UI;

internal static class 起動診断 {
    private const int 最大パンくず件数 = 120;
    private static readonly object 排他 = new();
    private static readonly List<Action<string>> 購読者一覧 = [];
    private static readonly StringBuilder ログバッファ = new();
    private static readonly Queue<string> パンくず一覧 = new();
    private static readonly string[] Syncfusionアセンブリ名一覧 = [
        "Syncfusion.Licensing",
        "Syncfusion.Maui.Core",
        "Syncfusion.Maui.DataGrid",
        "Syncfusion.Maui.Toolkit",
    ];

    public static string ログパス { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Seamless",
        "Benchmark",
        "Logs",
        "startup.log");

    public static void グローバルハンドラーを設定() {
        AppDomain.CurrentDomain.UnhandledException+=(_,引数) => {
            書き込む("未処理例外",引数.ExceptionObject as Exception);
            クラッシュ状況を書き込む("未処理例外");
        };
        TaskScheduler.UnobservedTaskException+=(_,引数) => {
            書き込む("未監視タスク例外",引数.Exception);
            クラッシュ状況を書き込む("未監視タスク例外");
        };
    }

    public static void パンくずを追加(string メッセージ) {
        try {
            var 文字列 = DateTimeOffset.Now.ToString("O",System.Globalization.CultureInfo.InvariantCulture)
                +" "
                +メッセージ;
            lock(排他) {
                パンくず一覧.Enqueue(文字列);
                while(パンくず一覧.Count>最大パンくず件数) {
                    パンくず一覧.Dequeue();
                }
            }
        } catch {
        }
    }

    public static void メインスレッドで呼び出し開始(string 状況,Action 処理) {
        パンくずを追加("UIディスパッチをキューへ追加: "+状況);
        try {
            if(MainThread.IsMainThread) {
                UI処理を実行(状況,処理);
                return;
            }

            MainThread.BeginInvokeOnMainThread(() => UI処理を実行(状況,処理));
        } catch(Exception 例外) {
            購読通知せず書き込む("UIディスパッチの予約に失敗しました: "+状況,例外);
            購読通知せずクラッシュ状況を書き込む("UIディスパッチの予約に失敗しました: "+状況);
        }
    }

    public static void UI処理を実行(string 状況,Action 処理) {
        パンくずを追加("UIディスパッチ開始: "+状況);
        try {
            処理();
            パンくずを追加("UIディスパッチ終了: "+状況);
        } catch(Exception 例外) {
            書き込む("UIディスパッチ処理に失敗しました: "+状況,例外);
            クラッシュ状況を書き込む("UIディスパッチ処理に失敗しました: "+状況);
            throw;
        }
    }

    public static void 書き込む(string メッセージ,Exception? 例外 = null) {
        try {
            var 文字列 = 記録文字列を構築(メッセージ,例外);
            ログ文字列を追記(文字列);
            購読者へ通知(文字列);
        } catch {
        }
    }

    public static void クラッシュ状況を書き込む(string タイトル) {
        try {
            var 文字列 = クラッシュ状況文字列を構築(タイトル);
            ログ文字列を追記(文字列);
            購読者へ通知(文字列);
        } catch {
        }
    }

    public static string クラッシュ状況文字列を取得(string タイトル) => クラッシュ状況文字列を構築(タイトル);

    private static void 購読通知せず書き込む(string メッセージ,Exception? 例外 = null) {
        try {
            ログ文字列を追記(記録文字列を構築(メッセージ,例外));
        } catch {
        }
    }

    private static void 購読通知せずクラッシュ状況を書き込む(string タイトル) {
        try {
            ログ文字列を追記(クラッシュ状況文字列を構築(タイトル));
        } catch {
        }
    }

    public static string ログ文字列を取得() {
        lock(排他) {
            return ログバッファ.ToString();
        }
    }

    public static IDisposable 購読(Action<string> 追記時処理) {
        lock(排他) {
            購読者一覧.Add(追記時処理);
        }

        return new 購読解除(追記時処理);
    }

    public static void 既存ログから初期化() {
        try {
            if(!File.Exists(ログパス)) {
                return;
            }

            var 文字列 = File.ReadAllText(ログパス);
            lock(排他) {
                if(ログバッファ.Length==0) {
                    ログバッファ.Append(文字列);
                }
            }
        } catch {
        }
    }

    private static string 記録文字列を構築(string メッセージ,Exception? 例外) {
        return DateTimeOffset.Now.ToString("O",System.Globalization.CultureInfo.InvariantCulture)
            +" "
            +メッセージ
            +Environment.NewLine
            +(例外 is null ? string.Empty : 例外+Environment.NewLine);
    }

    private static void ログ文字列を追記(string 文字列) {
        lock(排他) {
            Directory.CreateDirectory(Path.GetDirectoryName(ログパス)!);
            File.AppendAllText(ログパス,文字列);
            ログバッファ.Append(文字列);
        }
    }

    private static string クラッシュ状況文字列を構築(string タイトル) {
        string[] スナップショット;
        lock(排他) {
            スナップショット=パンくず一覧.ToArray();
        }

        var ビルダー = new StringBuilder();
        ビルダー.Append(DateTimeOffset.Now.ToString("O",System.Globalization.CultureInfo.InvariantCulture));
        ビルダー.Append(' ');
        ビルダー.AppendLine("クラッシュ状況開始: "+タイトル);
        foreach(var 項目 in スナップショット) {
            ビルダー.AppendLine(項目);
        }
        ビルダー.Append(DateTimeOffset.Now.ToString("O",System.Globalization.CultureInfo.InvariantCulture));
        ビルダー.Append(' ');
        ビルダー.AppendLine("クラッシュ状況終了: "+タイトル);
        return ビルダー.ToString();
    }

    private static void 購読者へ通知(string 文字列) {
        foreach(var 購読者 in 購読者一覧.ToArray()) {
            try {
                購読者(文字列);
            } catch {
            }
        }
    }

    public static void Syncfusionアセンブリを書き込む(string 状況) {
        try {
            foreach(var アセンブリ名 in Syncfusionアセンブリ名一覧) {
                var アセンブリ = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(アセンブリ => string.Equals(
                        アセンブリ.GetName().Name,
                        アセンブリ名,
                        StringComparison.OrdinalIgnoreCase));
                if(アセンブリ is null) {
                    書き込む($"{状況}: {アセンブリ名} は読み込まれていません。");
                    continue;
                }

                var 場所 = アセンブリ.Location;
                var 製品版 = "";
                var ファイル版 = "";
                if(!string.IsNullOrWhiteSpace(場所)&&File.Exists(場所)) {
                    var 情報 = System.Diagnostics.FileVersionInfo.GetVersionInfo(場所);
                    製品版=情報.ProductVersion??"";
                    ファイル版=情報.FileVersion??"";
                }

                書き込む(
                    $"{状況}: {アセンブリ名} アセンブリ={アセンブリ.GetName().Version} 製品版={製品版} ファイル版={ファイル版} 場所={場所}");
            }
        } catch(Exception 例外) {
            書き込む($"{状況}: Syncfusionアセンブリの検査に失敗しました。",例外);
        }
    }

    public static ContentPage エラーページを作成(string タイトル,Exception 例外) {
        Syncfusionアセンブリを書き込む("エラーページを作成");
        return new ContentPage {
            Title=タイトル,
            BackgroundColor=Color.FromArgb("#F8FAFC"),
            Content=new ScrollView {
                Content=new VerticalStackLayout {
                    Padding=new Thickness(24),
                    Spacing=12,
                    Children= {
                        new Label{
                            Text=タイトル,
                            FontSize=20,
                            FontAttributes=FontAttributes.Bold,
                            TextColor=Color.FromArgb("#991B1B"),
                        },
                        new Label{
                            Text="起動に失敗しました。ログを確認してください:",
                            FontSize=13,
                            TextColor=Color.FromArgb("#334155"),
                        },
                        new Label{
                            Text=ログパス,
                            FontSize=12,
                            TextColor=Color.FromArgb("#0F766E"),
                            LineBreakMode=LineBreakMode.WordWrap,
                        },
                        new Label{
                            Text=例外.ToString(),
                            FontSize=11,
                            TextColor=Color.FromArgb("#1E293B"),
                            LineBreakMode=LineBreakMode.WordWrap,
                        },
                    },
                },
            },
        };
    }

    private sealed class 購読解除:IDisposable {
        private readonly Action<string> 追記時処理;

        public 購読解除(Action<string> 追記時処理) {
            this.追記時処理=追記時処理;
        }

        public void Dispose() {
            lock(排他) {
                購読者一覧.Remove(this.追記時処理);
            }
        }
    }
}
