using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace UI;
internal sealed class MauiUiPreferences:IUiPreferences{
    public string Get(string key,string defaultValue) => Preferences.Default.Get(key,defaultValue);

    public void Set(string key,string value) => Preferences.Default.Set(key,value);
}
