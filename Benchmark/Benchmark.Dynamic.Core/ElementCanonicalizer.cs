using System.Globalization;
using Benchmark.Dynamic.ElementTypes;

namespace Benchmark.Dynamic.Core;

/// <summary>
/// int 以外の要素を、失われない文字列(正規形)にする。実装どうしの結果の比較と保存に使う。
/// 浮動小数点はビット列で書き、NaN は1通りに揃える(+0 と -0 は区別する)。
/// decimal は桁(スケール)まで含めて書くので、<c>1.1m</c> と <c>1.10m</c> は別の値になる。
/// </summary>
public static class ElementCanonicalizer{
    public static string Canonical(object? value){
        return value switch{
            null=>"null",
            int item=>"i:"+item.ToString(CultureInfo.InvariantCulture),
            long item=>"l:"+item.ToString(CultureInfo.InvariantCulture),
            float item=>"f:"+(float.IsNaN(item)?0x7FC00000u:BitConverter.SingleToUInt32Bits(item)).ToString("X8",CultureInfo.InvariantCulture),
            double item=>"d:"+(double.IsNaN(item)?0x7FF8000000000000UL:BitConverter.DoubleToUInt64Bits(item)).ToString("X16",CultureInfo.InvariantCulture),
            decimal item=>"m:"+DecimalBits(item),
            string item=>"s:"+item.Length.ToString(CultureInfo.InvariantCulture)+":"+item,
            Int256 item=>"I:"+item.ToHex(),
            Int256Object item=>"I:"+item.Value.ToHex(),
            Quadruple item=>"Q:"+item.ToHex(),
            QuadrupleObject item=>"Q:"+item.Value.ToHex(),
            Order item=>$"O:{item.Id.ToString(CultureInfo.InvariantCulture)},{item.CustomerId.ToString(CultureInfo.InvariantCulture)},{DecimalBits(item.Price)}",
            CollidingKey item=>"K:"+item.Value.ToString(CultureInfo.InvariantCulture),
            _=>throw new ArgumentException($"{value.GetType()} is not a supported element type.",nameof(value)),
        };
    }

    private static string DecimalBits(decimal value){
        return string.Join(",",decimal.GetBits(value).Select(static part=>part.ToString("X8",CultureInfo.InvariantCulture)));
    }
}
