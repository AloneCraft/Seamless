using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Reasoning;

public static class Const {
    public const double second = 0.1;
    public const double ms = second*1000;
    public const double us = ms*1000;
    public const double ns = us*1000;
    public static readonly long Ticks = (long)(Stopwatch.Frequency * second);
    public const int Ki = 1024, Mi = Ki * Ki, Gi = Mi * Ki;
    public const int K = 1000, M = K * K, G = M * K;
    public const double m=0.001,u=m*m,n=u*m;
    public const int 最小Bits = 12;
    public const int 最大Bits = 27;
    public const int 最小Bytes = 1 << 最小Bits;
}
