// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
namespace Tsavorite.core;
// この新artifactだけの決定的barrier。製品candidateには含めない。
internal static class RecoveryCleanupDiagnostics{
    internal static Action<LocalStorageDevice,IntPtr> Allocated{get;set;}
    internal static Action<LocalStorageDevice,IntPtr> Freed{get;set;}
    internal static Action<LocalStorageDevice> BeforeReturn{get;set;}
    internal static Action<LocalStorageDevice> AfterReturn{get;set;}
}
