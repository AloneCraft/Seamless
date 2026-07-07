using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace UI;
internal interface IUiPreferences{
    string Get(string key,string defaultValue);
    void Set(string key,string value);
}
