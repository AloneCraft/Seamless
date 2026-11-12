namespace Seamless.Database.Client;

/// <summary>ビルド済みの生成入口から、業務処理を実行せず公開定義を取得する。</summary>
public interface INativeGeneratedQueryModule:INativeRecordQueryModule{
    string ModuleId{get;}
    string DatabaseName{get;}
    IReadOnlyList<NativeQueryDefinition> Describe(string schemaFingerprint);
}
