namespace Sample.NetShop.Core;

/// <summary>物語の1章。説明文とコードを見せながら、実際にライブラリを動かす。</summary>
public abstract class 物語の章{
    private string[]? コード行キャッシュ;

    public abstract string 題名{get;}
    /// <summary>この章で使うライブラリの機能名。</summary>
    public abstract string 使う機能{get;}
    public abstract string 説明{get;}
    /// <summary>画面に見せるコード。実際に実行するコードを要約したもの。</summary>
    public abstract string コード{get;}

    public IReadOnlyList<string>コード行{
        get{
            this.コード行キャッシュ??=this.コード.Replace("\r\n","\n").Split('\n');
            return this.コード行キャッシュ;
        }
    }

    public abstract Task 実行Async(章の文脈 文脈);
}
