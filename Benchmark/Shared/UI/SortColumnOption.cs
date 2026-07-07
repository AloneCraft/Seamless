using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UI;
public sealed record SortColumnOption(GridSortColumn Column,string DisplayName);

/// <summary>驕ｸ謚・髱樣∈謚槭ち繝悶・繝・ム繝ｼ縺ｮ驟崎牡繧剃ｸ蜈・ｮ｡逅・☆繧九・縺､縺ｮ繧ｿ繝悶せ繝医Μ繝・・(繧ｽ繝ｼ繧ｹ/荳区ｮｵ/繝√Ε繝ｼ繝郁ｨｺ譁ｭ)縺ｧ蜈ｱ譛峨☆繧九・/summary>
public static class TabHeaderStyle{
    public static FontAttributes GetFontAttributes(bool isSelected) => isSelected ? FontAttributes.Bold : FontAttributes.None;
    public static Color GetTextColor(bool isSelected) => isSelected ? Color.FromArgb("#0F172A") : Color.FromArgb("#475569");
    public static Color GetBackgroundColor(bool isSelected) => isSelected ? Colors.White : Color.FromArgb("#F1F5F9");
    public static Brush GetStroke(bool isSelected) => new SolidColorBrush(isSelected ? Color.FromArgb("#0F766E") : Color.FromArgb("#CBD5E1"));
}
