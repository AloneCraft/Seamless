namespace Seamless.Management.UI;

public sealed record 接続設定(string 名前,string ホスト,int ポート,string 利用者,string? 証明書パス){public override string ToString()=>this.名前+" · "+this.ホスト+":"+this.ポート;}
