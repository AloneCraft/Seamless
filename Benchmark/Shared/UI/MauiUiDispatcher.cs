using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace UI;
internal sealed class MauiUiDispatcher:IUiDispatcher{
    public void BeginInvoke(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
