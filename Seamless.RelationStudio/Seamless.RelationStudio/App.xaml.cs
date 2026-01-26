namespace Seamless.RelationStudio;

public partial class App:Application {
    public App() {
        InitializeComponent();
        Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("@33322e302e303b33323bMJtjbeNLZn+WXwuxqWQehHipzndqLvzgD/Rtdl+nQ3A=");
        //Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("@33322e302e303b33323bdyCZaXx3CWVhyAIinygDoQTNdJRQS44CpyFqhPJYGD4=");
    }

    protected override Window CreateWindow(IActivationState? activationState) {
        var window = new Window(new AppShell());
        window.Width = 500;
        window.Height = 500;
        return window;
    }
}
