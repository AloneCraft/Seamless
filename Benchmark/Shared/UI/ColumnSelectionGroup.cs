using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed class ColumnSelectionGroup:ObservableObject{
    private bool? isChecked;
    private string headerText;

    public ColumnSelectionGroup(string id,string headerText,IReadOnlyList<string> columnKeys){
        this.Id=id;
        this.headerText=headerText;
        this.ColumnKeys=columnKeys;
    }

    public string Id { get; }
    public string HeaderText{
        get => this.headerText;
        private set => this.SetProperty(ref this.headerText,value);
    }
    public IReadOnlyList<string> ColumnKeys { get; }

    public bool? IsChecked{
        get => this.isChecked;
        set => this.SetProperty(ref this.isChecked,value);
    }

    public void SetHeaderText(string value){
        this.HeaderText=value;
    }
}
