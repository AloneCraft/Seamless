namespace Sample.NetShop.Tests;

/// <summary>テストごとの一時フォルダ。破棄で中身ごと消す。</summary>
internal sealed class テスト用フォルダ:IDisposable{
    public テスト用フォルダ(){
        this.パス=Path.Combine(Path.GetTempPath(),"Sample.NetShop.Tests",Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(this.パス);
    }

    public string パス{get;}

    public string 下(string 名前)=>Path.Combine(this.パス,名前);

    public void Dispose(){
        if(Directory.Exists(this.パス)){
            Directory.Delete(this.パス,true);
        }
    }
}
