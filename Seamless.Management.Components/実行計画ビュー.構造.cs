using Microsoft.AspNetCore.Components;
using Seamless.Management;

namespace Seamless.Management.Components;

public partial class 実行計画ビュー{
    private string 構造段階="Relational";
    private string コード経路="Auto";
    private GeneratedExecutionCode? 表示生成コード(ExecutionPlanDocument 計画){
        if(this.コード経路=="Profiled"||this.コード経路=="Auto"&&this.共通可視化?.NodeQuantities is{Length:>0}){return 計画.ProfiledGeneratedCode;}
        return 計画.GeneratedCode;
    }
    private void コード経路変更時(){
        string? 経路=null;if(this.コード経路!="Auto"){経路=this.コード経路;}
        this.保存表示設定=this.保存表示設定 with{GeneratedVariant=経路};
    }
    private QueryPlanRequest? 取得要求;
    private bool 生成取得中か,生成要求完了か;
    private string? 生成案内;
    private long 生成世代;
    private CancellationTokenSource? 生成取消;
    private string グラフ初期層{
        get{
            if(this.構造段階=="Logical"){return "Logical";}
            return "Original";
        }
    }
    private void 構造を初期化(){
        this.コード経路="Auto";
        this.生成世代++;this.生成取消?.Cancel();this.生成取消?.Dispose();this.生成取消=null;
        this.生成取得中か=false;this.生成要求完了か=false;this.生成案内=null;this.取得要求=null;
        this.構造段階="Relational";this.保存表示設定=this.保存表示設定 with{StructureLevel="Relational",GeneratedVariant=null};
    }
    private Task 構造変更時Async(ChangeEventArgs 引数){
        var 値=引数.Value?.ToString();if(値 is not("Relational" or "Logical" or "Generated")){return Task.CompletedTask;}
        this.構造段階=値;this.保存表示設定=this.保存表示設定 with{StructureLevel=値};
        if(値=="Generated"&&!this.生成要求完了か&&this.選択計画?.GeneratedCode is null){return this.生成構造取得時Async();}
        return Task.CompletedTask;
    }
    private void 表示設定時(PlanSnapshotDisplaySettings 設定)=>this.保存表示設定=設定 with{StructureLevel=this.構造段階};
    private async Task 生成構造取得時Async(){
        if(!this.表示中か||this.生成取得中か){return;}
        if(this.取得要求 is not{} 要求||this.取得結果 is not{} 基準||this.選択計画 is null){this.生成案内=表示文言.取得("対象と版を特定したクエリを選択すると、生成構造を取得できます。");return;}
        this.生成取消?.Cancel();this.生成取消?.Dispose();this.生成取消=CancellationTokenSource.CreateLinkedTokenSource(this.読込取消?.Token??default);
        var 取消=this.生成取消.Token;var 世代=++this.生成世代;this.生成取得中か=true;this.生成案内=null;
        try{
            var 応答=await this.管理操作.QueryPlansAsync(要求 with{IncludeGeneratedCode=true},取消).WaitAsync(取消);
            if(世代!=this.生成世代||!ReferenceEquals(基準,this.取得結果)){return;}
            if(応答.DefinitionId!=基準.DefinitionId||応答.Selection.ObjectId!=基準.Selection.ObjectId||応答.Version!=基準.Version||応答.ArtifactHash!=基準.ArtifactHash||応答.SourceHash!=基準.SourceHash||応答.FromDraft!=基準.FromDraft||応答.DraftRevision!=基準.DraftRevision){
                this.生成案内=表示文言.取得("生成構造の版が現在の計画と異なります。対象の計画を再取得してください。");return;
            }
            var 置換=new List<ExecutionPlanDocument>();
            foreach(var 元 in this.読込済み計画??[]){
                var 候補=応答.Selection.Plans.Where(項目=>項目.MethodKey==元.MethodKey&&項目.SourceHash==元.SourceHash&&項目.GeneratedBodyHash==元.GeneratedBodyHash&&項目.CallSite==元.CallSite).ToArray();
                if(候補.Length!=1){this.生成案内=表示文言.取得("現在のメソッドに一致する生成構造を特定できません。");return;}
                置換.Add(候補[0]);
            }
            this.読込済み計画=置換.ToArray();this.生成要求完了か=true;
            if(this.選択計画?.GeneratedCode is null){this.生成案内=表示文言.取得("この計画には最終生成C#構造の記録がありません。");}
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){}
        catch(Exception 例外){
            if(世代!=this.生成世代){return;}
            this.生成案内=例外.Message;
            if(例外 is ManagementException{Code:"Permission" or "Authentication"}){this.読込済み計画=this.読込済み計画?.Select(項目=>項目 with{GeneratedCode=null,ProfiledGeneratedCode=null}).ToArray();}
        }finally{if(世代==this.生成世代){this.生成取得中か=false;}}
    }
}
