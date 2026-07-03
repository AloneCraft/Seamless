using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace UI;

internal interface IBenchmarkUiDispatcher{
    void BeginInvoke(Action action);
}

internal sealed class MauiBenchmarkUiDispatcher:IBenchmarkUiDispatcher{
    public void BeginInvoke(Action action) => MainThread.BeginInvokeOnMainThread(action);
}

internal interface IBenchmarkUiPreferences{
    string Get(string key,string defaultValue);
    void Set(string key,string value);
}

internal sealed class MauiBenchmarkUiPreferences:IBenchmarkUiPreferences{
    public string Get(string key,string defaultValue) => Preferences.Default.Get(key,defaultValue);

    public void Set(string key,string value) => Preferences.Default.Set(key,value);
}
