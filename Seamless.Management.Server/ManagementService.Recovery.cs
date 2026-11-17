namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    // 復元の手順とプロンプト(段階8)。管理領域の場所を含むので、管理の権限が要る。バックアップの場所とエラー文は画面では分からないので空にする。
    public RecoveryGuideDocument RecoveryGuide(UserAccount actor,RecoveryScenario scenario){
        lock(this.store.Gate){actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);}
        return RecoveryGuideCommand.Create(scenario,this.store.Root,null,null);
    }
}
