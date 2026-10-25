using Seamless.Execution;

namespace Shared.Benchmark.History;

public sealed class 測定設定画面:ContentView{
    private readonly Picker プロファイル選択=new(){Title="プロファイル",ItemsSource=new[]{"Minimum","Optimized"},SelectedIndex=0};
    private readonly Picker 拡張選択=new(){Title="拡張方式",ItemsSource=new[]{"プロファイルに従う","doubling","fixed","estimated"},SelectedIndex=0};
    private readonly Picker 保存選択=new(){Title="保存方式",ItemsSource=new[]{"Memory","Temporary"},SelectedIndex=1};
    private readonly Picker 入出力選択=new(){Title="ファイル入出力",ItemsSource=new[]{"BackendDefault","Buffered","Unbuffered"},SelectedIndex=0};
    private readonly Picker アクセス選択=new(){Title="ファイルアクセス",ItemsSource=new[]{"Sequential","Random"},SelectedIndex=0,IsEnabled=false};
    private readonly Entry 索引指定=new(){Placeholder="索引バイト数（空欄=プロファイル）"};
    private readonly Entry メモリ指定=new(){Placeholder="ログメモリバイト数（空欄=プロファイル）"};
    private readonly Entry メモリ上限=new(){Text="268435456"};
    private readonly Entry ディスク上限=new(){Text="17179869184"};
    private readonly Entry プロファイルファイル=new(){Placeholder="独自プロファイルJSONのパス（任意）"};
    private readonly Entry 最適化指定=new(){Text="15"};
    public 測定設定画面(bool 最適化指定を表示するか=true){
        this.最適化指定.IsVisible=最適化指定を表示するか;
        this.入出力選択.SelectedIndexChanged+=(_,_)=>{this.アクセス選択.IsEnabled=this.入出力選択.SelectedIndex!=0;if(!this.アクセス選択.IsEnabled){this.アクセス選択.SelectedIndex=0;}};
        this.Content=new ScrollView{Content=new VerticalStackLayout{Padding=12,Spacing=8,Children={
            new Label{Text="初期構成と拡張方式",FontSize=20},this.プロファイル選択,this.拡張選択,this.保存選択,this.索引指定,this.メモリ指定,
            new Label{Text="ファイル入出力（Temporaryで比較）"},this.入出力選択,this.アクセス選択,
            new Label{Text="メモリ上限（バイト）"},this.メモリ上限,new Label{Text="ディスク上限（バイト）"},this.ディスク上限,
            this.プロファイルファイル,new Label{Text="FLinq最適化フラグ（0=なし、15=すべて）",IsVisible=最適化指定を表示するか},this.最適化指定,
            new Label{Text="Minimumは最小構成から始めます。Optimizedに適用できる測定済み設定がなければMinimumへ戻り、その理由を履歴に記録します。Fixedは索引とログメモリ設定を固定します。データ件数の上限ではありません。"}
        }}};
    }
    public string プロファイル=>this.プロファイル選択.SelectedItem?.ToString()??"Minimum";
    public string 拡張{get{if(this.拡張選択.SelectedIndex==0){return "profile";}return this.拡張選択.SelectedItem?.ToString()??"profile";}}
    public string 保存=>this.保存選択.SelectedItem?.ToString()??"Temporary";
    public string 入出力=>this.入出力選択.SelectedItem?.ToString()??"BackendDefault";
    public string アクセス=>this.アクセス選択.SelectedItem?.ToString()??"Sequential";
    public ProfileSettings 明示設定=>new(){IndexBytes=数値(this.索引指定.Text),LogMemoryBytes=数値(this.メモリ指定.Text)};
    public long メモリ予算=>long.Parse(this.メモリ上限.Text);
    public long ディスク予算=>long.Parse(this.ディスク上限.Text);
    public int 最適化フラグ=>int.Parse(this.最適化指定.Text);
    public string? プロファイルJSON{get{if(string.IsNullOrWhiteSpace(this.プロファイルファイル.Text)){return null;}return File.ReadAllText(this.プロファイルファイル.Text);}}
    private static long? 数値(string? 値){if(string.IsNullOrWhiteSpace(値)){return null;}return long.Parse(値);}
}
