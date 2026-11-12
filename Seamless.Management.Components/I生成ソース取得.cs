namespace Seamless.Management.Components;

/// <summary>下書きのソースからDB生成器・FLinq生成器が作るファイルを取得する画面用のサービス。</summary>
/// <remarks>公開の管理通信契約(IManagementOperations)は変えず、生成器を持つホストだけが登録する。未登録のホストでは画面が未対応を表示する。</remarks>
public interface I生成ソース取得{
    Task<GeneratedSourceFile[]> 取得Async(SourceSnapshot ソース,CancellationToken 取消);
}
