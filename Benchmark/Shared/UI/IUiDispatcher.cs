using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace UI;
internal interface IUiDispatcher{
    void BeginInvoke(Action action);
}
