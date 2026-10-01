namespace Sample.NetShop;

public static class MauiProgram{
    public static MauiApp CreateMauiApp(){
        var ビルダー=MauiApp.CreateBuilder();
        ビルダー.UseMauiApp<App>();
        ビルダー.Services.AddSingleton<MainPage>();
        return ビルダー.Build();
    }
}
