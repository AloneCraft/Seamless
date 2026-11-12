using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Seamless.Management.Components;

/// <remarks>
/// 左ツリーのメニューから、下書きのソースに対する生成コードを読取専用で表示する。
/// 「データベース」見出しは全DB、各DBはそのDBの全ソース(利用者ファイル＋生成ファイル)。
/// 分類・スキーマ・テーブル・ビュー・関数は、対象の名前を含む生成ファイルだけに絞る。
/// 表示するのは稼働DBではなく下書きから生成した結果で、編集権限で下書きを読めるDBだけが対象になる。
/// </remarks>
public partial class 管理ワークスペース{
    [Inject]private IServiceProvider サービス{get;set;}=default!;
    private 生成コード表示状態? 生成コード;
    private CancellationTokenSource? 生成コード取消;

    private sealed class 生成コード表示状態{
        public required string 題名{get;init;}
        public GeneratedSourceFile[] ファイル{get;set;}=[];
        public bool 読込中か{get;set;}=true;
        public string? 案内{get;set;}
        public bool 未対応か{get;set;}
    }

    private async Task 生成コードを開くAsync(管理対象 対象,bool 全ソースか){
        this.生成コード取消?.Cancel();this.生成コード取消?.Dispose();
        var 取消=CancellationTokenSource.CreateLinkedTokenSource(this.寿命.Token);this.生成コード取消=取消;
        var 対象DB一覧=new List<CatalogDatabase>();
        string 題名;
        if(対象.種類=="分類"){
            対象DB一覧.AddRange(this.DB一覧.Where(DB=>!DB.IsSystem));
            題名=表示文言.取得("全データベースのソース");
        }else if(this.対象DB(対象) is{} DB){
            対象DB一覧.Add(DB);
            題名=DB.Name;
            var 名前=生成コード対象名(対象);
            if(名前.Length>0){題名+=" / "+名前;}
            if(全ソースか){題名+=" · "+表示文言.取得("全ソース");}else{題名+=" · "+表示文言.取得("生成コード");}
        }else{
            return;
        }
        var 状態=new 生成コード表示状態{題名=題名};this.生成コード=状態;await this.InvokeAsync(this.StateHasChanged);
        var 取得=this.サービス.GetService<I生成ソース取得>();
        if(取得 is null){
            状態.読込中か=false;状態.未対応か=true;
            状態.案内=表示文言.取得("このホストは生成コードの表示に対応していません（未対応）。");
            return;
        }
        var 結果=new List<GeneratedSourceFile>();var 読めないDB=new List<string>();
        try{
            foreach(var DB in 対象DB一覧){
                if(!this.文書一覧.ContainsKey(DB.Id)&&this.権限があるか(DB.Name,ManagementPermission.Edit)){await this.DB文書を読み込むAsync(DB);}
                if(!this.文書一覧.TryGetValue(DB.Id,out var 文書)){読めないDB.Add(DB.Name);continue;}
                var ソース=文書.定義.Source;
                var ファイル=await Task.Run(()=>取得.取得Async(ソース,取消.Token),取消.Token);
                if(!全ソースか){ファイル=絞り込み(ファイル,文書,対象);}
                foreach(var 項目 in ファイル){
                    var パス=項目.Path;
                    if(対象DB一覧.Count>1){パス=DB.Name+"/"+パス;}
                    結果.Add(項目 with{Path=パス});
                }
            }
            if(取消.IsCancellationRequested||!ReferenceEquals(this.生成コード,状態)){return;}
            状態.ファイル=結果.ToArray();
            if(読めないDB.Count>0){
                状態.未対応か=true;
                状態.案内=表示文言.取得("編集権限がないため下書きを読めないDBは表示していません（未対応）: {0}",string.Join(", ",読めないDB));
            }else{
            }
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){
            return;
        }catch(Exception 例外){
            状態.案内=表示文言.取得("生成コードを作成できません: ")+例外.Message;状態.未対応か=true;
        }finally{
            if(ReferenceEquals(this.生成コード,状態)){状態.読込中か=false;await this.InvokeAsync(this.StateHasChanged);}
        }
    }

    private void 生成コードを閉じる時(){this.生成コード取消?.Cancel();this.生成コード=null;}

    private static string 生成コード対象名(管理対象 対象){
        if(対象.表 is{} 表){return 表;}
        if(対象.種類=="オブジェクト分類"){
            var 名前=表示文言.取得(対象.追加種類??"");
            if(対象.スキーマ.Length>0){return 対象.スキーマ+" / "+名前;}
            return 名前;
        }
        return 対象.スキーマ;
    }

    /// <summary>対象に関係する生成ファイルだけを残す。利用者ファイルは含めない。名前は識別子の単位で照合する。</summary>
    private static GeneratedSourceFile[] 絞り込み(GeneratedSourceFile[] ファイル,文書状態 文書,管理対象 対象){
        var 名前一覧=new List<string>();
        if(対象.表 is{} 表){
            名前一覧.Add(表.Split('.').Last());
        }else if(対象.種類=="オブジェクト分類"){
            var 種類=対象.追加種類??"";
            名前一覧.AddRange(DatabaseDesigner.Objects(文書.定義).Where(項目=>項目.Schema==対象.スキーマ&&(項目.Kind==種類||種類=="Function"&&項目.Kind.EndsWith("Function",StringComparison.Ordinal))).Select(項目=>項目.Name));
        }else if(対象.スキーマ.Length>0){
            名前一覧.Add(対象.スキーマ);
        }else{
        }
        if(名前一覧.Count==0){return [];}
        var 照合=new Regex(@"\b("+string.Join("|",名前一覧.Distinct(StringComparer.Ordinal).Select(Regex.Escape))+@")\b",RegexOptions.CultureInvariant);
        return ファイル.Where(項目=>項目.Kind!="User"&&照合.IsMatch(項目.Text)).ToArray();
    }
}
