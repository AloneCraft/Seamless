namespace UI;

internal interface IベンチマークUIディスパッチャー {
    void 呼び出し開始(Action 処理);
}

internal sealed class MauiベンチマークUIディスパッチャー:IベンチマークUIディスパッチャー {
    public void 呼び出し開始(Action 処理) => 起動診断.メインスレッドで呼び出し開始("IベンチマークUIディスパッチャー.呼び出し開始",処理);
}

internal interface IベンチマークUI設定 {
    string 取得(string キー,string 既定値);
    void 設定(string キー,string 値);
}

internal sealed class MauiベンチマークUI設定:IベンチマークUI設定 {
    public string 取得(string キー,string 既定値) => Preferences.Default.Get(キー,既定値);

    public void 設定(string キー,string 値) => Preferences.Default.Set(キー,値);
}
