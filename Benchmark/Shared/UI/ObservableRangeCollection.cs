using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class ObservableRangeCollection<T>:ObservableCollection<T>{
    public void ReplaceRange(IEnumerable<T> items){
        this.CheckReentrancy();
        this.Items.Clear();
        foreach(var item in items){
            this.Items.Add(item);
        }
        this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.Count)));
        this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
