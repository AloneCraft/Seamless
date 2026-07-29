namespace Benchmark.Dynamic;

public enum 動的分割方向 {
    垂直,
    水平,
}

public sealed class 動的GridSplitter:ContentView {
    private double 前回総移動量;

    public 動的GridSplitter(動的分割方向 方向,Action<double> 移動時) {
        this.方向=方向;
        this.移動時=移動時;
        this.BackgroundColor=Color.FromArgb("#CBD5E1");
        this.AutomationId=方向==動的分割方向.垂直?"設定領域.GridSplitter":"詳細領域.GridSplitter";
        AutomationProperties.SetIsInAccessibleTree(this,true);
        var ジェスチャー=new PanGestureRecognizer();
        ジェスチャー.PanUpdated+=this.移動更新時;
        this.GestureRecognizers.Add(ジェスチャー);
    }

    public 動的分割方向 方向{get;}
    private Action<double> 移動時{get;}

    private void 移動更新時(object? 送信元,PanUpdatedEventArgs 引数) {
        if(引数.StatusType==GestureStatus.Started) {
            this.前回総移動量=0;
            return;
        }
        if(引数.StatusType==GestureStatus.Running) {
            var 総移動量=this.方向==動的分割方向.垂直?引数.TotalX:引数.TotalY;
            var 差分=総移動量-this.前回総移動量;
            this.前回総移動量=総移動量;
            this.移動時(差分);
            return;
        }
        this.前回総移動量=0;
    }
}
