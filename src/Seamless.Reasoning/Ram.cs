using System.Text;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Reasoning;
// 計測設定（パラメータ）
public struct Ram(long Bytes,double レイテンシsecond,long BytesSecond){
    //public string 名前 { get; set; } = 名前;// "L1i", "L1d", "L2", "L3"
    public long Bytes{get; set;}=Bytes;
    public double レイテンシsecond{get; set;}=レイテンシsecond;
    public long BytesSecond{get; set;}=BytesSecond;

    public double CacheSizeKiB=>this.Bytes/(double)Const.Ki;
    public double LatencyNs=>this.レイテンシsecond*1_000_000_000.0;
    public double BandwidthGiB=>this.BytesSecond/(double)Const.Gi;

    public List<int> 直接アクセス可能なコア{get; set;}
    public override string ToString(){
        var sb=new StringBuilder();
        //sb.Append(this.名前);
        //sb.Append(',');
        //1G=1,000,000,000Bytes
        //1M=    1,000,000Bytes
        //1K=        1,000Bytes
        //1 =            1Bytes
        if(this.Bytes<Const.Ki){
            //999Bytes
            sb.Append($"{this.Bytes,3}Bytes,");
        }
        else if(this.Bytes<Const.Mi){
            //999KBytes
            sb.Append($"{this.Bytes/Const.Ki,3}KiBytes,");
        }
        else if(this.Bytes<Const.Gi){
            //999MBytes
            sb.Append($"{this.Bytes/Const.Mi,3}MiBytes,");
        }
        else{
            //999GBytes
            sb.Append($"{this.Bytes/Const.Gi,3}GiBytes,");
        }
        //1 s=1,000,000,000ns
        //1ms=    1,000,000ns
        //1us=        1,000ns
        //1ns=            1ns
        if(this.レイテンシsecond<Const.u){
            //999.99ns
            sb.Append($"{this.レイテンシsecond*Const.G,6:F2}ns,");
        }
        else if(this.レイテンシsecond<Const.m){
            //0.001s 1ms
            sb.Append($"{this.レイテンシsecond*Const.M,6:F2}us,");
        }
        else if(this.レイテンシsecond<1){
            sb.Append($"{this.レイテンシsecond*Const.K,6:F2}ms,");
        }
        else{
            sb.Append($"{this.レイテンシsecond,6:F2}s,");
        }
        if(this.BytesSecond<Const.Ki){
            sb.Append($"{this.BytesSecond,3}Byte/Sec");
        }
        else if(this.BytesSecond<Const.Mi){
            sb.Append($"{this.BytesSecond/Const.Ki,3}KiByte/Sec");
        }
        else if(this.BytesSecond<Const.Gi){
            sb.Append($"{this.BytesSecond/Const.Mi,3}MiByte/Sec");
        }
        else{
            sb.Append($"{this.BytesSecond/Const.Gi,3}GiByte/Sec");
        }
        return sb.ToString();
    }
}

public enum SharingLevel{
    HyperThreading, // L1/L2を共有（同一物理コア）
    SameCCX,        // L3を共有（同一ダイ内）
    SameSocket,     // ダイは違うが同一ソケット内
    RemoteSocket    // NUMAノードを跨ぐ遠い関係
}
