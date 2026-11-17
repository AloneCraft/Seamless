namespace Seamless.Management.Server;

/// <summary>Managerの画面から、下書きのソースに対する生成コードを取得する。</summary>
/// <remarks>コンパイルと同じ準備でDB生成器・FLinq生成器を走らせるだけで、Emitとユーザーコードの実行は行わない。</remarks>
/// <remarks>製品では下書きの SDK のビルドが書き出した生成ファイルを読む(11/15 の決定：表示とビルドの結果を一致させる)。SDK のビルドが無効なら内蔵の生成器で作る。</remarks>
internal sealed class 生成ソース取得サービス(Func<DraftSdkBuilder?>? SDKビルド=null):global::Seamless.Management.Components.I生成ソース取得{
    public Task<GeneratedSourceFile[]> 取得Async(SourceSnapshot ソース,CancellationToken 取消){
        取消.ThrowIfCancellationRequested();
        if(SDKビルド?.Invoke() is{} ビルド){return Task.Run(()=>ビルド.GenerateSources(ソース,取消),取消);}
        return Task.FromResult(new RuntimeCompiler().GenerateSources(ソース));
    }
}
