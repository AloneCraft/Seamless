namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private static string 監査集計メソッド詳細(AuditQueryInfo? 情報){if(情報 is null){return "\n実行メソッド: 未登録";}var 名前空間=情報.Namespace??"未記録";if(名前空間.Length==0){名前空間="グローバル名前空間";}return $"\n名前空間: {名前空間}\n型: {情報.TypeName??"未記録"}\nメソッド: {情報.MethodName??"未記録"}\nシグネチャ: {情報.MethodSignature??"未記録"}\nモジュールID: {情報.ModuleId} / MetadataToken: {情報.MetadataToken}\nクエリテンプレート: "+(情報.QueryTemplate??"未記録、または閲覧権限なし");}
}
