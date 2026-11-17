using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Xml;
using Seamless.Management;

namespace Seamless.Management.Components;

internal static partial class 計画保存出力{
    private const string SVG名前空間="http://www.w3.org/2000/svg";
    internal static string SVG(PlanSnapshot 保存,Func<string,string> 文言){
        if(保存.Data.Extension is{} 拡張&&拡張.View.Mode!="DAG"){return 可視化SVG(保存,文言);}
        _=保存.ToJson();var 設定=保存.Data.Display;var 配置=計画グラフ配置.作成(保存.Data.Plan,表示層(設定.Layer));
        var 文字列=new StringBuilder();using var 出力=XmlWriter.Create(文字列,new(){OmitXmlDeclaration=true});
        void 開始(string 名前)=>出力.WriteStartElement(名前,SVG名前空間);
        void 属性(string 名前,object 値)=>出力.WriteAttributeString(名前,Convert.ToString(値,CultureInfo.InvariantCulture));
        void 文字(string 値,int X,int Y,string? クラス=null){開始("text");属性("x",X);属性("y",Y);if(クラス is not null){属性("class",クラス);}出力.WriteString(値);出力.WriteEndElement();}
        void 線(int 元X,int 元Y,int 先X,int 先Y,string 種類,string 説明){
            var 差=Math.Max(30,Math.Abs(先Y-元Y)/2);開始("path");属性("class",線クラス(種類));
            属性("d",FormattableString.Invariant($"M {元X} {元Y} C {元X} {元Y+差}, {先X} {先Y-差}, {先X} {先Y}"));属性("marker-end",種類=="DisplayResult"?"url(#displayEnd)":"url(#planArrow)");
            開始("title");出力.WriteString(説明);出力.WriteEndElement();出力.WriteEndElement();
        }
        開始("svg");属性("viewBox",$"0 0 {配置.幅} {配置.高さ+110}");属性("width",配置.幅);属性("height",配置.高さ+110);属性("role","img");
        開始("title");出力.WriteString(保存.Data.ObjectId+" · "+文言("保存済みの実行計画"));出力.WriteEndElement();
        開始("style");出力.WriteString("text{font:13px system-ui,sans-serif;fill:#243d36}.node{fill:#fff;stroke:#6e998a}.region{fill:#f7faf8;stroke:#d9e4dd}.result{fill:#edf5ef;stroke:#6e998a;stroke-dasharray:5 3}.edge{fill:none;stroke:#597e70;stroke-width:1.5}.expression{stroke:#876b9e;stroke-dasharray:3 3}.selected{stroke:#486b9a;stroke-dasharray:8 3}.mapping{stroke:#8f8978;stroke-dasharray:2 5}.unknown{stroke:#8e8e8e;stroke-dasharray:7 5}.display{stroke:#8e8e8e;stroke-dasharray:2 5}.secondary{font-size:11px;fill:#66786e}");出力.WriteEndElement();
        開始("defs");開始("marker");属性("id","planArrow");属性("markerWidth",8);属性("markerHeight",8);属性("refX",8);属性("refY",4);属性("orient","auto");開始("path");属性("d","M 0 0 L 8 4 L 0 8 z");属性("fill","#627e74");出力.WriteEndElement();出力.WriteEndElement();
        開始("marker");属性("id","displayEnd");属性("markerWidth",8);属性("markerHeight",8);属性("refX",4);属性("refY",4);開始("circle");属性("cx",4);属性("cy",4);属性("r",2);属性("fill","#8e8e8e");出力.WriteEndElement();出力.WriteEndElement();出力.WriteEndElement();
        foreach(var 領域 in 配置.領域){開始("rect");属性("class","region");属性("x",領域.X);属性("y",8);属性("width",領域.幅);属性("height",領域.高さ);属性("rx",12);出力.WriteEndElement();文字(文言(層名(領域.層)),領域.X+20,36);}
        var 隠れた種類=(設定.HiddenEdgeKinds??[]).ToHashSet(StringComparer.Ordinal);
        foreach(var 接続 in 配置.接続.Where(接続=>!隠れた種類.Contains(接続.種類))){
            線(接続.入力.中心X,接続.入力.下端,接続.出力.中心X,接続.出力.Y,接続.種類,文言(接続名(接続.種類)));
            if(設定.ShowInputRoles){var 説明=接続.入力役割??"#"+接続.入力番号;文字(説明,接続.出力.中心X+8,接続.出力.Y-10-接続.入力番号*12,"secondary");}
        }
        if(設定.ShowDisplayResult&&設定.ShowSummaryEdges){foreach(var 元 in 配置.集約元){線(元.中心X,元.下端,配置.幅/2,配置.結果Y,"DisplayResult",文言("表示用の集約線"));}}
        foreach(var ノード in 配置.ノード){
            開始("g");開始("title");出力.WriteString(ノード.元.Label);出力.WriteEndElement();開始("rect");属性("class","node");属性("x",ノード.X);属性("y",ノード.Y);属性("width",計画配置ノード.幅);属性("height",計画配置ノード.高さ);属性("rx",8);出力.WriteEndElement();
            文字(短縮(ノード.元.Kind+" #"+ノード.元.Id,32),ノード.X+12,ノード.Y+22,"secondary");文字(短縮(ノード.元.Label,30),ノード.X+12,ノード.Y+47);文字(短縮(ノード.元.OutputType??文言("型の記録なし"),34),ノード.X+12,ノード.Y+72,"secondary");出力.WriteEndElement();
        }
        if(設定.ShowDisplayResult){開始("rect");属性("class","result");属性("x",配置.結果X);属性("y",配置.結果Y);属性("width",計画配置ノード.幅);属性("height",計画配置ノード.高さ);属性("rx",8);出力.WriteEndElement();文字("Result",配置.結果X+12,配置.結果Y+32);文字(文言("表示用の集約"),配置.結果X+12,配置.結果Y+56,"secondary");}
        var 凡例Y=配置.高さ+22;
        foreach(var 種類 in 配置.接続.Select(接続=>接続.種類).Where(種類=>!隠れた種類.Contains(種類)).Concat(設定.ShowDisplayResult&&設定.ShowSummaryEdges?new[]{"DisplayResult"}:[]).Select(接続名).Distinct(StringComparer.Ordinal)){
            文字("— "+文言(種類),24,凡例Y,"secondary");凡例Y+=16;
        }
        出力.WriteEndElement();出力.Flush();return 上限確認(文字列.ToString());
    }
    internal static string HTML(PlanSnapshot 保存,Func<string,string> 文言){
        var 図=SVG(保存,文言);var 結果=new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; img-src 'none'; base-uri 'none'; form-action 'none'\"><title>Seamless Plan</title><style>body{font:14px system-ui,sans-serif;color:#263e35;background:#fff;margin:32px}h1{font-size:25px}dl{display:grid;grid-template-columns:max-content 1fr;gap:8px 16px}dt{font-weight:600}dd{margin:0;overflow-wrap:anywhere}.diagram{overflow:auto;border:1px solid #d9e4dd;border-radius:10px}.note{padding:12px;background:#f7f8f2}pre{white-space:pre-wrap;overflow-wrap:anywhere}table{border-collapse:collapse}td,th{padding:6px 12px;border:1px solid #d9e4dd;text-align:left}</style></head><body>");
        string 安全(string 値)=>HtmlEncoder.Default.Encode(値);
        void 項目(string 名称,string 値)=>結果.Append("<dt>").Append(安全(文言(名称))).Append("</dt><dd>").Append(安全(値)).Append("</dd>");
        結果.Append("<h1>").Append(安全(保存.Data.ObjectId)).Append("</h1><p class=\"note\">").Append(安全(文言(保存.Data.IncludesCode?"このファイルにはコードやリテラルが含まれます。共有先を確認してください。":"コード・リテラル・ソースパスは出力していません。"))).Append("</p><dl>");
        項目("対象の公開版",保存.Data.Version.ToString(CultureInfo.InvariantCulture));項目("成果物",保存.Data.ArtifactHash);項目("保存内容ハッシュ",保存.ContentHash);
        if(保存.Data.Observation is{} 終端){項目("実行結果",終端.Outcome);項目("実行段階",終端.Phase);}
        if(保存.Data.Statistics is{} 実測){
            項目("実行ID",実測.ExecutionId.ToString());項目("経過時間",実測.Elapsed.TotalMilliseconds.ToString("0.###",CultureInfo.InvariantCulture)+" ms");項目("DBゲート待機",実測.DatabaseGateWait.TotalMilliseconds.ToString("0.###",CultureInfo.InvariantCulture)+" ms");項目("返却出力数",実測.ReturnedOutputCount.ToString(CultureInfo.InvariantCulture));項目("取得状態",文言(実測.IsPartial?"応答上限による部分取得":"取得完了"));
        }
        結果.Append("</dl><p>").Append(安全(文言("保存内容のハッシュは署名やDLLの真正性を保証しません。"))).Append("</p><div class=\"diagram\">").Append(図).Append("</div>");
        var 追跡=OptimizationTraceReader.Read(保存.Data.Plan.Trace,保存.Data.IncludesCode);
        結果.Append("<h2>").Append(安全(文言("最適化追跡"))).Append("</h2>");
        if(!追跡.Available){結果.Append("<p>").Append(安全(文言("最適化追跡の記録がありません。"))).Append("</p>");}
        foreach(var 変更 in 追跡.Events){
            結果.Append("<details><summary>").Append(安全($"#{変更.Sequence} {変更.Layer} / {変更.RuleId} / {変更.Phase}")).Append("</summary><p>").Append(安全($"Round {変更.Round} · Pass {変更.Pass} · Changed {変更.Changed} · Revisited {変更.Revisited}")).Append("</p>");
            if(保存.Data.IncludesCode){結果.Append("<pre>").Append(安全(変更.Before??"")).Append("</pre><pre>").Append(安全(変更.After??"")).Append("</pre>");}結果.Append("</details>");
        }
        var 計測案内="ノード別時間、CPU、I/O、詳細イベントは未収集です。";
        if(保存.Data.Extension?.ResourceHeat is not null){計測案内="ホスト全体、プロセス、クエリ寄与、ノード寄与を区別します。色は各レーンの百分率の分母に従います。";}
        結果.Append("<p>").Append(安全(文言(計測案内))).Append("</p></body></html>");return 上限確認(結果.ToString());
    }
    private static string 上限確認(string 値){if(Encoding.UTF8.GetByteCount(値)>PlanSnapshot.MaximumBytes){throw new InvalidDataException("計画の保存サイズ上限を超えています。");}return 値;}
    private static string 短縮(string 値,int 長さ){if(値.Length<=長さ){return 値;}return 値[..長さ]+"…";}
    private static 計画表示層 表示層(string 値)=>値 switch{"Selected"=>計画表示層.処理,"Original"=>計画表示層.元のクエリ,"OriginalExpression"=>計画表示層.数式,"Optimization"=>計画表示層.判断と条件,"Unknown"=>計画表示層.その他,"Logical"=>計画表示層.論理処理,_=>計画表示層.すべて};
    private static string 層名(計画表示層 値)=>値 switch{計画表示層.処理=>"採用された処理",計画表示層.元のクエリ=>"元のクエリ",計画表示層.数式=>"元の数式",計画表示層.判断と条件=>"判断と条件",_=>"その他のノード"};
    private static string 接続名(string 値)=>値 switch{"OriginalDataFlow"=>"元クエリのデータフロー","ExpressionDependency"=>"元の数式の依存","SelectedReference"=>"採用処理の入力参照","SourceMapping"=>"ソース対応","DisplayResult"=>"表示用の集約線",_=>"種別未記録の接続"};
    private static string 線クラス(string 値)=>値 switch{"ExpressionDependency"=>"edge expression","SelectedReference"=>"edge selected","SourceMapping"=>"edge mapping","DisplayResult"=>"edge display","OriginalDataFlow"=>"edge",_=>"edge unknown"};
}
