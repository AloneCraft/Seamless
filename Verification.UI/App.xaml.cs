namespace Verification.UI;

public partial class App:Application {
    private readonly MainPage 開始ページ;

    public App(MainPage 開始ページ) {
        this.InitializeComponent();
        this.開始ページ=開始ページ;
    }

    protected override Window CreateWindow(IActivationState? activationState)=>new(this.開始ページ) {
        Title="Seamless Verification",
        MinimumWidth=1180,
        MinimumHeight=760,
    };
}
