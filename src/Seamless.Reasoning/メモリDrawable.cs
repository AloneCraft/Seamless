//namespace Seamless.Reasoning;
//public class メモリDrawable{
//    public コア[]? Arrayコア;
//    public long[]? ArrayXラベル;
//    //public double[,] LatencyData { get; set; }
//    public float TranslationX{get;set;}=0;// 追加：左右の平行移動
//    public float TranslationY{get;set;}=0;// 追加：上下の平行移動
//    public float RotationAngle{get;set;}=35f;// 左右
//    public float ElevationAngle{get;set;}=30f;// 上下 (追加)

//    public float Scale{get;set;}=1.0f;// 初期倍率
//    private (float sx, float sy) Project2D(int xIndex, float value, int stepCount, float maxVal, RectF dirtyRect)
//    {
//        float marginL = 60f;
//        float marginR = 60f; // 少し広めに
//        float marginT = 60f;
//        float marginB = 60f;

//        float drawWidth = dirtyRect.Width - marginL - marginR;
//        float drawHeight = dirtyRect.Height - marginT - marginB;

//        // 0除算防止
//        if (stepCount <= 1) stepCount = 2;
//        if (maxVal <= 0) maxVal = 1;

//        float sx = marginL + (xIndex / (float)(stepCount - 1)) * drawWidth;
//        // Y軸：値が大きいほど上（syが小さくなる）に行くように計算
//        float sy = marginT + drawHeight - (value / maxVal) * drawHeight;

//        return (sx, sy);
//    }
//    private (float sx, float sy) Project2D2(int xIndex, float value, int stepCount, float maxVal, RectF dirtyRect)
//    {
//        // グラフの余白（ラベル表示用）
//        float marginL = 60f;
//        float marginR = 20f;
//        float marginT = 40f;
//        float marginB = 40f;

//        float drawWidth = dirtyRect.Width - marginL - marginR;
//        float drawHeight = dirtyRect.Height - marginT - marginB;

//        // X軸：データのインデックスを横幅にマッピング
//        float sx = marginL + (xIndex / (float)(stepCount - 1)) * drawWidth;

//        // Y軸：値を縦幅にマッピング（上方向が0なので、逆転させる）
//        // maxValは帯域幅の最大値（例：1000Mbpsなど）
//        float sy = marginT + drawHeight - (value / maxVal) * drawHeight;

//        return (sx, sy);
//    }

//    public void Draw(ICanvas canvas,RectF dirtyRect){
//        if(this.Arrayコア==null||this.ArrayXラベル==null) return;

//        var stepCount=this.ArrayXラベル.Length;
//        var coreCount=this.Arrayコア.Length;
//        var centerX=dirtyRect.Width/2;
//        var centerY=dirtyRect.Height/2;

//        // --- 1. 最大値の自動計算（画面内に収めるため） ---
//        float maxLatency=0.1f;// 0除算防止
//        foreach(var コア in this.Arrayコア){
//            foreach(var item in コア.ListRam){
//                maxLatency=Math.Max(maxLatency,(float)item.レイテンシns);
//            }
//        }
//        // 少し余裕を持たせる（最大値の1.2倍をグラフの天井にする）
//        maxLatency*=1.2f;

//        // --- 2. 軸の描画 ---
//        canvas.StrokeColor=Colors.Gray;
//        canvas.StrokeSize=1;
//        var (ax0,ay0)=Project2D(0,0,stepCount,maxLatency,dirtyRect);
//        var (ax1,ay1)=Project2D(stepCount-1,0,stepCount,maxLatency,dirtyRect);
//        canvas.DrawLine(ax0,ay0,ax1,ay1);

//        // --- 3. コアごとの折れ線描画 ---
//        for(int z=0;z<coreCount;z++){
//            var コア=this.Arrayコア[z];
//            var path=new PathF();

//            // コアごとに色を変える
//            Color lineColor=Color.FromHsla((float)z/coreCount,0.7f,0.5f);
//            canvas.StrokeColor=lineColor;
//            canvas.StrokeSize=2;

//            for(int x=0;x<stepCount;x++){
//                float val=(float)コア.ListRam[x].レイテンシns;
//                var (sx,sy)=Project2D(x,val,stepCount,maxLatency,dirtyRect);

//                if(x==0) path.MoveTo(sx,sy);
//                else path.LineTo(sx,sy);

//                // 小さな点でプロット
//                canvas.FillColor=lineColor;
//                canvas.FillCircle(sx,sy,2);
//            }
//            canvas.DrawPath(path);
//        }

//        // --- 4. X軸ラベル（サイズ） ---
//        canvas.FontColor=Colors.White;
//        canvas.FontSize=10;
//        for(int x=0;x<stepCount;x++){
//            var (sx,sy)=Project2D(x,0,stepCount,maxLatency,dirtyRect);
//            if(x%2==0){
//                // 混雑防止のため1つおきに表示
//                var Bytes=this.ArrayXラベル[x];
//                string s;
//                if(Bytes<Const.Ki){
//                    s=$"{Bytes}Byte/Sec";
//                } else if(Bytes<Const.Mi){
//                    s=$"{Bytes/Const.Ki}KiByte/Sec";
//                } else if(Bytes<Const.Gi){
//                    s=$"{Bytes/Const.Mi}MiByte/Sec";
//                } else{
//                    s=$"{Bytes/Const.Gi}GiByte/Sec";
//                }

//                canvas.DrawString(s,sx,sy+25,HorizontalAlignment.Center);
//            }
//        }
//    }

//    public void Draw2(ICanvas canvas, RectF dirtyRect)
//    {
//        if (this.Arrayコア == null || this.ArrayXラベル == null) return;

//        var stepCount = this.ArrayXラベル.Length;
//        float maxBandwidth = 1000f; // 帯域幅の最大スケール（適宜調整）

//        // --- 1. 背景と軸の描画 ---
//        canvas.StrokeColor = Colors.DarkGray;
//        canvas.StrokeSize = 2;
//        // X軸
//        var (ax0, ay0) = Project2D(0, 0, stepCount, maxBandwidth, dirtyRect);
//        var (ax1, ay1) = Project2D(stepCount - 1, 0, stepCount, maxBandwidth, dirtyRect);
//        canvas.DrawLine(ax0, ay0, ax1, ay1);

//        // --- 2. データの描画（コアごとの折れ線） ---
//        for (int z = 0; z < this.Arrayコア.Length; z++)
//        {
//            var コア = this.Arrayコア[z];
//            var path = new PathF();
//            Color lineColor = z % 2 == 0 ? Colors.RoyalBlue : Colors.DeepSkyBlue;
//            canvas.StrokeColor = z % 2 == 0 ? Colors.RoyalBlue : Colors.DeepSkyBlue;
//            canvas.StrokeSize = 3;

//            for (int x = 0; x < stepCount; x++)
//            {
//                // レイテンシnsを帯域幅（仮にMB/sなど）に見立てて計算
//                float value = (float)コア.ListRam[x].レイテンシns / 1000f;
//                var (sx, sy) = Project2D(x, value, stepCount, maxBandwidth, dirtyRect);

//                if (x == 0) path.MoveTo(sx, sy);
//                else path.LineTo(sx, sy);

//                // ポイントに丸を打つ（オプション）
//                canvas.FillColor = lineColor;
//                canvas.FillCircle(sx, sy, 4);
//            }
//            canvas.DrawPath(path);
//        }

//        // --- 3. ラベルの描画 ---
//        canvas.FontColor = Colors.White;
//        canvas.FontSize = 12;
//        for (int x = 0; x < stepCount; x += 2) // 1つ飛ばしで表示
//        {
//            var (sx, sy) = Project2D(x, 0, stepCount, maxBandwidth, dirtyRect);
//            var Bytes=this.ArrayXラベル[x];
//            string s;
//            if(Bytes< Const.Ki) {
//                s=$"{Bytes}Byte/Sec";
//            } else if(Bytes< Const.Mi) {
//                s=$"{Bytes/ Const.Ki}KiByte/Sec";
//            } else if(Bytes< Const.Gi) {
//                s=$"{Bytes/ Const.Mi}MiByte/Sec";
//            } else {
//                s=$"{Bytes/ Const.Gi}GiByte/Sec";
//            }

//            canvas.DrawString(s, sx, sy + 20, HorizontalAlignment.Center);
//        }
//    }
//}
