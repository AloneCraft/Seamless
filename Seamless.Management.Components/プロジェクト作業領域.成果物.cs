namespace Seamless.Management.Components;

public partial class プロジェクト作業領域{
    private Task 成果物検査時Async(ソース文書 文書)=>this.実行Async(async()=>{
        文書.成果物=null;文書.成果物案内=null;文書.成果物エラーか=false;
        try{
            await this.同期Async(文書);
            var 結果=await this.操作.InspectSourceProjectBuildAsync(new(文書.情報.Id,文書.成果物パス.Trim(),文書.基準.Hash()),this.寿命.Token);
            文書.成果物=結果;
            文書.成果物案内=表示文言.取得(string.IsNullOrWhiteSpace(文書.成果物パス)?"SDKでビルドし、DLL・PDB・元ソース・依存DLLの検査に成功しました。":"DLL・PDB・元ソース・依存DLLの検査に成功しました。");
            if(this.成果物取込可能か){文書.成果物案内+=表示文言.取得("取り込んだ後、DB画面で変更を確認して適用できます。");}
        }catch(Exception 例外){
            文書.成果物エラーか=true;文書.成果物案内=例外.Message;
            throw;
        }
    },false);
    // DLL の場所を空にして検査を頼むと、サーバーが SDK でビルドしてから検査する(取り込みのときも同じくビルドし直して検査する)。
    private Task SDKビルド検査時Async(ソース文書 文書){文書.成果物パス="";return this.成果物検査時Async(文書);}
    private Task 成果物取込時Async(ソース文書 文書)=>this.実行Async(async()=>{
        var 成果物=文書.成果物??throw new InvalidOperationException("ビルド成果物を検査してください。");
        文書.成果物エラーか=false;
        await this.下書き反映中変更時.InvokeAsync(true);
        try{
            await this.下書き反映前.InvokeAsync(成果物.DefinitionId);
            await this.同期Async(文書);
            if(成果物.SourceHash!=文書.基準.Hash()||成果物.SourceHash!=文書.現在.Hash()){throw new ManagementException("Conflict","検査後にソースが変更されています。再検査してください。");}
            var 結果=await this.操作.ImportSourceProjectBuildAsync(new(new(文書.情報.Id,文書.成果物パス.Trim(),成果物.SourceHash),成果物.BundleHash),this.寿命.Token);
            await this.成果物取込後.InvokeAsync(結果);
            文書.成果物案内=表示文言.取得("成果物を取り込みました。DB画面の「変更を確認」から検証・明示適用へ進めます。");
        }catch(Exception 例外){文書.成果物エラーか=true;文書.成果物案内=例外.Message;throw;}
        finally{await this.下書き反映中変更時.InvokeAsync(false);}
    },false);
}
