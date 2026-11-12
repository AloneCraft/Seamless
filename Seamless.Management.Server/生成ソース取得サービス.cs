namespace Seamless.Management.Server;

/// <summary>Managerの画面から、下書きのソースに対する生成コードを取得する。</summary>
/// <remarks>コンパイルと同じ準備でDB生成器・FLinq生成器を走らせるだけで、Emitとユーザーコードの実行は行わない。</remarks>
internal sealed class 生成ソース取得サービス:global::Seamless.Management.Components.I生成ソース取得{
    public Task<GeneratedSourceFile[]> 取得Async(SourceSnapshot ソース,CancellationToken 取消){
        取消.ThrowIfCancellationRequested();
        return Task.FromResult(new RuntimeCompiler().GenerateSources(ソース));
    }
}
