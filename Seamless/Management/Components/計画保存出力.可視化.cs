using System.Globalization;
using System.Text;
using System.Xml;
using Seamless.Management;

namespace Seamless.Management.Components;

internal static partial class 計画保存出力{
    private static string 可視化SVG(PlanSnapshot 保存,Func<string,string> 文言){
        _=保存.ToJson();var 拡張=保存.Data.Extension!;var データ=保存.Visualization()!;var 設定=拡張.View;
        var 幅=1240d;var 高さ=180d;Action<保存図> 描画;
        switch(設定.Mode){
            case "Sankey":{
                var 出所=new[]{設定.Sankey.Basis};if(設定.Sankey.Basis=="Compare"){出所=["Estimated","Observed"];}
                var 配置=出所.Select(値=>(出所:値,図:サンキー配置.作成(データ,設定.Sankey.Unit,値,設定.Sankey.Scale))).ToArray();
                幅=配置.Max(値=>値.図.幅);高さ=配置.Sum(値=>値.図.高さ+70)+70;
                描画=図=>{
                    var Y=60d;
                    foreach(var 項目 in 配置){
                        図.文字($"{項目.出所} · {設定.Sankey.Unit} · {設定.Sankey.Scale}",20,Y);図.開始("g",("transform",$"translate(0 {数値(Y+10)})"));
                        foreach(var 接続 in 項目.図.接続){
                            var 未知=接続.始量.DecimalValue is null||接続.終量.DecimalValue is null;var ゼロ=接続.始量.DecimalValue=="0"&&接続.終量.DecimalValue=="0";
                            var 経路=接続.帯;var 塗り="#6da995";var 線="none";var 破線="";
                            if(未知||ゼロ){経路=接続.中心線;塗り="none";線="#75857f";破線="5 4";}
                            図.開始("g");図.題名(接続.元.Id+" · "+接続.元.Relation+" · "+量(接続.始量)+" → "+量(接続.終量));
                            図.要素("path",("d",経路),("fill",塗り),("stroke",線),("stroke-dasharray",破線),("data-unknown",未知.ToString()),("data-zero",ゼロ.ToString()));図.終了();
                        }
                        foreach(var ノード in 項目.図.ノード){図.開始("g");図.題名(ノード.元.Id+" · "+ノード.元.Label);図.矩形(ノード.X,ノード.Y,サンキー配置.ノード幅,ノード.高さ,選択色(ノード.元.Id,ノード.元.Label,設定));図.文字(短縮(ノード.元.Label,24),ノード.X+8,ノード.Y+25);図.文字(ノード.元.Id,ノード.X+8,ノード.Y+45);図.終了();}
                        図.終了();Y+=項目.図.高さ+70;
                    }
                    図.文字(文言("帯は入力と出力で独立した量を示します。未知の量は破線です。論理参照は実転送を意味しません。"),20,高さ-15);
                };break;
            }
            case "Gantt":{
                var 配置=実行時間モデル.作成(データ);幅=実行時間モデル.幅;高さ=Math.Max(180,配置.Sum(項目=>項目.高さ+55)+80);
                描画=図=>{
                    var Y=55d;
                    foreach(var 時計 in 配置){
                        図.開始("g",("transform",$"translate(0 {数値(Y)})"));図.文字(時計.時計.Id+" · "+時計.時計.TicksPerSecond+" ticks/s",12,20);
                        foreach(var レーン in 時計.レーン){図.文字(レーン.ID,10,レーン.Y+16);}
                        for(var index=0;index<=5;index++){var 時刻=時計.開始Ticks+(decimal)時計.経過Ticks*index/5;var X=実行時間モデル.左端+実行時間モデル.描画幅*index/5d;図.文字(((double)(時刻-時計.開始Ticks)*1000/時計.時計.TicksPerSecond).ToString("0.###",CultureInfo.InvariantCulture)+" ms",X,45);}
                        foreach(var 区間 in 時計.区間){var 色="#568f7d";if(区間.値.Outcome is "Failed" or "Cancelled"){色="#c86c6c";}図.矩形(区間.X,区間.Y,区間.幅,22,色);図.文字(区間.値.Scope+" · "+区間.値.StartTicks+"–"+区間.値.EndTicks,区間.X,区間.Y-4);}
                        foreach(var 点 in 時計.イベント){図.要素("path",("d",$"M{数値(点.X)} {点.Y-6} v36"),("stroke","#375a78"));図.文字(点.値.Kind,点.X+4,点.Y+34);}
                        図.終了();Y+=時計.高さ+55;
                    }
                    if(配置.Length==0){図.文字(文言("未収集"),20,90);}
                    図.文字(文言("横軸は各実行の単調時計による経過時間です。包含区間を合算せず、CPU時間と区別します。"),20,高さ-15);
                };break;
            }
            case "Heat":{
                資源ヒート配置? 配置=null;var 記録=拡張.ResourceHeat;
                if(記録 is not null){string? 種類=null;if(設定.Heat.Kind.Length>0){種類=設定.Heat.Kind;}配置=資源ヒート配置.作成(記録,データ,設定.Heat.Scope,種類,設定.Heat.RangeStart,設定.Heat.RangeEnd);高さ=配置.高さ+110;}
                描画=図=>{
                    if(配置 is null||記録 is null){図.文字(文言("資源サンプルは未収集です。次回の実行で資源計測を指定できます。"),20,90);return;}
                    図.文字(設定.Heat.Scope+" · "+記録.Clock.TicksPerSecond+" ticks/s · "+設定.Heat.Palette+" · "+設定.Heat.PlaybackSpeed+"×",20,55);
                    for(var index=0;index<=5;index++){var raw=(decimal)配置.開始+((decimal)配置.終了-配置.開始)*index/5;var ms=(raw-記録.Clock.AnchorTimestamp)*1000/記録.Clock.TicksPerSecond;図.文字(ms.ToString("0.###",CultureInfo.InvariantCulture)+" ms",資源ヒート配置.左端+資源ヒート配置.描画幅*index/5d,80);}
                    図.開始("g",("transform","translate(0 60)"));
                    foreach(var レーン in 配置.レーン){図.文字(短縮(レーン.値.Label,26),10,レーン.Y+15);}
                    foreach(var 区間 in 配置.実行区間){図.文字(区間.値.LaneId,10,区間.Y+14);図.矩形(区間.X,区間.Y,区間.幅,18,"#739a89");}
                    foreach(var サンプル in 配置.サンプル){
                        図.開始("g",("data-state",サンプル.値.State),("data-evidence",サンプル.値.Evidence));図.題名(サンプル.値.State+" · "+サンプル.値.Evidence+" · "+サンプル.値.Value+" · "+サンプル.値.MissingReason);
                        var 色="#e0e4e3";if(サンプル.値.State=="Available"&&サンプル.値.Percent is{} percent){色=資源ヒート配置.色(percent,設定.Heat.Palette);}
                        if(サンプル.値.Aggregation=="Instant"||サンプル.幅==0){図.要素("path",("d",$"M{数値(サンプル.X)} {サンプル.Y+1} l5 8 -5 8 -5 -8 Z"),("fill",色));}
                        else{図.矩形(サンプル.X,サンプル.Y+1,サンプル.幅,18,色);}
                        if(設定.Heat.ShowNumbers&&サンプル.値.Percent is{} 値){図.文字(値.ToString("0.#",CultureInfo.InvariantCulture)+"%",サンプル.X,サンプル.Y+15);}
                        図.終了();
                    }
                    if(設定.Heat.Overlay){
                        var 線図=資源折線配置.作成(配置);
                        foreach(var line in 線図.区間){図.開始("g");図.題名(line.レーンID+" · "+line.最小値+"–"+line.最大値+" "+line.単位);図.要素("path",("d",line.経路),("fill","none"),("stroke","#243d68"),("stroke-width",1.5));図.終了();}
                        foreach(var point in 線図.点){図.要素("circle",("cx",point.X),("cy",point.Y),("r",2.5),("fill","#243d68"));}
                    }
                    var 時刻X=資源ヒート配置.左端+資源ヒート配置.描画幅*設定.Heat.Cursor/1000d;図.要素("path",("d",$"M{数値(時刻X)} 35 V{配置.高さ}"),("stroke","#ad6d32"));図.終了();
                    図.文字(文言("不明・未対応・欠落は状態と理由で区別します。")+" · "+記録.DroppedSamples+" dropped · "+配置.表示省略数+" not displayed",20,高さ-15);
                };break;
            }
            case "Tree":
            case "DOT":{
                var 行=保存.Data.Plan.ToDot().Split('\n');if(設定.Mode=="Tree"){行=保存.Data.Plan.Nodes.Select(項目=>項目.Kind+" · "+項目.Id+" · "+項目.Label+" ← "+string.Join(", ",項目.Inputs)).ToArray();}
                高さ=Math.Max(180,行.Length*20+90);描画=図=>{for(var index=0;index<行.Length;index++){図.文字(行[index],20,65+index*20);}};break;
            }
            default:throw new InvalidDataException("この表示の図出力は未対応です。");
        }
        var 結果=new StringBuilder();using(var writer=XmlWriter.Create(結果,new(){OmitXmlDeclaration=true})){
            var 図=new 保存図(writer);var 拡大=1d;if(設定.Mode=="Gantt"){拡大=設定.GanttZoom;}if(設定.Mode=="Heat"){拡大=設定.Heat.Zoom;}
            図.開始("svg",("viewBox",$"0 0 {数値(幅)} {数値(高さ)}"),("width",幅*拡大),("height",高さ*拡大),("role","img"),("data-mode",設定.Mode));
            図.開始("title");writer.WriteString(設定.Mode+" · "+保存.Data.ObjectId);図.終了();
            図.開始("style");writer.WriteString("text{font:12px system-ui,sans-serif;fill:#243d36}rect{stroke:#79978c}svg{background:white}");図.終了();
            図.文字(設定.Mode+" · "+保存.Data.ObjectId,20,25);描画(図);図.終了();writer.Flush();
        }
        return 上限確認(結果.ToString());
    }
    private static string 数値(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
    private static string 量(FlowQuantity value){if(value.DecimalValue is null){return "Unknown";}var 完全="";if(!value.Complete){完全=" (partial)";}return value.DecimalValue+完全;}
    private static string 選択色(string id,string label,PlanSnapshotViewState view){if(view.SelectedNodeId==id){return "#bdddcf";}if(view.Search.Length>0&&(id.Contains(view.Search,StringComparison.OrdinalIgnoreCase)||label.Contains(view.Search,StringComparison.OrdinalIgnoreCase))){return "#fff2b9";}return "#f7faf8";}
    private sealed class 保存図(XmlWriter writer){
        internal void 開始(string name,params (string Name,object Value)[] attributes){writer.WriteStartElement(name,SVG名前空間);foreach(var item in attributes){writer.WriteAttributeString(item.Name,Convert.ToString(item.Value,CultureInfo.InvariantCulture));}}
        internal void 終了()=>writer.WriteEndElement();
        internal void 要素(string name,params (string Name,object Value)[] attributes){this.開始(name,attributes);this.終了();}
        internal void 文字(string value,double x,double y){this.開始("text",("x",x),("y",y));writer.WriteString(value);this.終了();}
        internal void 題名(string value){this.開始("title");writer.WriteString(value);this.終了();}
        internal void 矩形(double x,double y,double width,double height,string fill)=>this.要素("rect",("x",x),("y",y),("width",width),("height",height),("fill",fill),("rx",3));
    }
}
