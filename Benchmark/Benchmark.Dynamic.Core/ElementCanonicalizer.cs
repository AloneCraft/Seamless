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
            byte item=>"by:"+item.ToString(CultureInfo.InvariantCulture),
            sbyte item=>"sby:"+item.ToString(CultureInfo.InvariantCulture),
            short item=>"sh:"+item.ToString(CultureInfo.InvariantCulture),
            ushort item=>"ush:"+item.ToString(CultureInfo.InvariantCulture),
            uint item=>"ui:"+item.ToString(CultureInfo.InvariantCulture),
            ulong item=>"ul:"+item.ToString(CultureInfo.InvariantCulture),
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
            bool item => "b:" + (item ? "1" : "0"),
            System.Runtime.CompilerServices.ITuple tuple => "T:" + string.Join(";", Enumerable.Range(0, tuple.Length).Select(i => Canonical(tuple[i]))),
            Realistic.Trade item => $"Tr:{item.Id},{item.AccountId},{item.Symbol},{item.Quantity},{DecimalBits(item.Price)},{(byte)item.Status},{item.TimestampTicks},{item.IsCash}",
            Realistic.Account item => $"Ac:{item.Id},{item.CustomerId},{item.BrokerId},{item.Name},{DecimalBits(item.Balance)},{item.Tier}",
            Realistic.Security item => $"Sc:{item.Symbol},{item.Sector},{item.CompanyId},{DecimalBits(item.LastPrice)}",
            Realistic.Customer item => $"Cu:{item.Id},{item.Name},{item.City},{item.Age},{item.IsPremium}",
            Realistic.SalesOrder item => $"So:{item.Id},{item.CustomerId},{DecimalBits(item.Amount)},{item.OrderedAtTicks},{(int)item.Status},{item.CouponCode}",
            _=>throw new ArgumentException($"{value.GetType()} is not a supported element type.",nameof(value)),
        };
    }

    private static string DecimalBits(decimal value){
        return string.Join(",",decimal.GetBits(value).Select(static part=>part.ToString("X8",CultureInfo.InvariantCulture)));
    }
}
