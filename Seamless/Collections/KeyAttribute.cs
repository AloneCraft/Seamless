using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Database;

/// <summary>宣言型から生成する主キーの構成プロパティを指定する。</summary>
/// <remarks>
/// サーバーとクライアントが同じ行の型を共有できるよう、DB の本体(Seamless.Database)ではなく、クライアントからも届く
/// この部品に置く。名前空間は宣言で書く名前(Seamless.Database.Key)のまま。
/// </remarks>
[AttributeUsage(AttributeTargets.Property,AllowMultiple=false,Inherited=false)]
public sealed class KeyAttribute:Attribute;
