namespace UI;

public sealed class ベンチマーク円形進行ビュー:GraphicsView {
    public static readonly BindableProperty 進行プロパティ = BindableProperty.Create(
        nameof(進行度),
        typeof(double),
        typeof(ベンチマーク円形進行ビュー),
        0d,
        propertyChanged: static (バインド可能オブジェクト,_,_) => ((ベンチマーク円形進行ビュー)バインド可能オブジェクト).Invalidate());

    public static readonly BindableProperty 進行色プロパティ = BindableProperty.Create(
        nameof(進行色),
        typeof(Color),
        typeof(ベンチマーク円形進行ビュー),
        既定進行色,
        propertyChanged: static (バインド可能オブジェクト,_,_) => ((ベンチマーク円形進行ビュー)バインド可能オブジェクト).Invalidate());

    public static readonly BindableProperty 輪郭色プロパティ = BindableProperty.Create(
        nameof(輪郭色),
        typeof(Color),
        typeof(ベンチマーク円形進行ビュー),
        既定輪郭色,
        propertyChanged: static (バインド可能オブジェクト,_,_) => ((ベンチマーク円形進行ビュー)バインド可能オブジェクト).Invalidate());

    private static readonly Color トラック色 = Color.FromArgb("#CBD5E1");
    private static readonly Color 既定進行色 = Color.FromArgb("#F97316");
    private static readonly Color 既定輪郭色 = Color.FromArgb("#64748B");

    public ベンチマーク円形進行ビュー() {
        this.Drawable=new ベンチマーク円形進行描画(this);
        this.Loaded+=(_,_) => this.Invalidate();
        this.SizeChanged+=(_,_) => this.Invalidate();
    }

    public double 進行度 {
        get => (double)this.GetValue(進行プロパティ);
        set => this.SetValue(進行プロパティ,value);
    }

    public Color 進行色 {
        get => (Color)this.GetValue(進行色プロパティ);
        set => this.SetValue(進行色プロパティ,value);
    }

    public Color 輪郭色 {
        get => (Color)this.GetValue(輪郭色プロパティ);
        set => this.SetValue(輪郭色プロパティ,value);
    }

    private sealed class ベンチマーク円形進行描画(ベンチマーク円形進行ビュー 所有者):IDrawable {
        public void Draw(ICanvas キャンバス,RectF 描画領域) {
            var 寸法 = MathF.Min(描画領域.Width,描画領域.Height);
            if(寸法<=0) {
                return;
            }

            var 線幅 = MathF.Max(1.25f,寸法*0.11f);
            var 半径 = MathF.Max(0,寸法/2f-線幅/2f);
            var 中心横座標 = 描画領域.X+描画領域.Width/2f;
            var 中心縦座標 = 描画領域.Y+描画領域.Height/2f;
            キャンバス.FillColor=トラック色;
            キャンバス.FillCircle(中心横座標,中心縦座標,半径);

            var 進行 = Math.Clamp(所有者.進行度,0,1);
            if(進行>0) {
                キャンバス.FillColor=所有者.進行色;
                if(進行>=1) {
                    キャンバス.FillCircle(中心横座標,中心縦座標,半径);
                } else {
                    キャンバス.FillPath(進行パスを作成(中心横座標,中心縦座標,半径,進行));
                }
            }

            キャンバス.StrokeColor=所有者.輪郭色;
            キャンバス.StrokeSize=線幅;
            キャンバス.DrawCircle(中心横座標,中心縦座標,半径);
            if(進行<=0) {
                return;
            }
        }

        private static PathF 進行パスを作成(float 中心横座標,float 中心縦座標,float 半径,double 進行) {
            var パス = new PathF();
            var 開始角度 = -90d;
            var 掃引角 = 360d*進行;
            var 分割数 = Math.Max(2,(int)Math.Ceiling(掃引角/12d));
            パス.MoveTo(中心横座標,中心縦座標);
            for(var インデックス = 0;インデックス<=分割数;インデックス++) {
                var 角度 = (float)((開始角度+(掃引角*インデックス/分割数))*Math.PI/180d);
                パス.LineTo(
                    中心横座標+(MathF.Cos(角度)*半径),
                    中心縦座標+(MathF.Sin(角度)*半径));
            }
            パス.Close();
            return パス;
        }
    }
}
