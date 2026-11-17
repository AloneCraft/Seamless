using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
namespace Seamless.Linq;

[global::System.AttributeUsage(
    global::System.AttributeTargets.Parameter|
    global::System.AttributeTargets.ReturnValue|
    global::System.AttributeTargets.Field|
    global::System.AttributeTargets.Property,
    AllowMultiple=false,
    Inherited=false)]
public sealed class UnorderedEnumerableAttribute:global::System.Attribute;