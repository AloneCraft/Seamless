[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Application,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$Worker,
    [string]$DesktopName,
    [switch]$ProbeOnly,
    [switch]$LiveOnly
)
$ErrorActionPreference='Stop'
if(-not [IO.Path]::IsPathRooted($Application) -or -not [IO.Path]::IsPathRooted($OutputDirectory)){throw 'Absolute paths are required'}
if([IO.Path]::GetPathRoot($OutputDirectory) -ne 'F:\'){throw 'GUI artifacts must be on F'}
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$env:TEMP=$OutputDirectory;$env:TMP=$OutputDirectory
$env:SEAMLESS_BENCHMARK_DATA_ROOT=Join-Path $OutputDirectory 'profile'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class IsolatedGuiNative{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    public struct StartupInfo{
        public int cb;public string reserved;public string desktop;public string title;
        public int x,y,width,height,xChars,yChars,fill,flags;
        public short show,reservedLength;public IntPtr reservedPointer,input,output,error;
    }
    [StructLayout(LayoutKind.Sequential)]public struct ProcessInfo{public IntPtr process,thread;public int pid,tid;}
    [StructLayout(LayoutKind.Sequential)]public struct BasicLimit{
        public long processTime,jobTime;public uint flags;public UIntPtr minWorking,maxWorking;
        public uint activeProcesses;public UIntPtr affinity;public uint priority,scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]public struct IoCounters{public ulong reads,writes,others,readBytes,writeBytes,otherBytes;}
    [StructLayout(LayoutKind.Sequential)]public struct ExtendedLimit{
        public BasicLimit basic;public IoCounters io;public UIntPtr processMemory,jobMemory,peakProcess,peakJob;
    }
    [StructLayout(LayoutKind.Sequential)]public struct Rect{public int left,top,right,bottom;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,uint flags,uint access,IntPtr security);
    [DllImport("user32.dll",SetLastError=true)]public static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll",SetLastError=true)]public static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
    [DllImport("user32.dll")]public static extern IntPtr GetThreadDesktop(uint tid);
    [DllImport("kernel32.dll")]public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder text,int length,out int needed);
    public static string Name(IntPtr handle){int needed;var text=new StringBuilder(256);if(!GetUserObjectInformation(handle,2,text,512,out needed))throw new System.ComponentModel.Win32Exception();return text.ToString();}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern bool CreateProcess(string app,StringBuilder args,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern bool SetInformationJobObject(IntPtr job,int type,ref ExtendedLimit info,uint length);
    [DllImport("kernel32.dll",SetLastError=true)]public static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")]public static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")]public static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll")]public static extern bool GetExitCodeProcess(IntPtr process,out uint code);
    [DllImport("kernel32.dll")]public static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")]public static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
    [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    public delegate bool EnumWindow(IntPtr window,IntPtr parameter);
    [DllImport("user32.dll")]static extern bool EnumDesktopWindows(IntPtr desktop,EnumWindow callback,IntPtr parameter);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out int pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int length);
    public static IntPtr MainWindow(int pid){
        IntPtr found=IntPtr.Zero;
        EnumDesktopWindows(GetThreadDesktop(GetCurrentThreadId()),delegate(IntPtr window,IntPtr unused){
            int owner;GetWindowThreadProcessId(window,out owner);
            if(owner==pid){var text=new StringBuilder(512);GetWindowText(window,text,512);if(text.ToString()=="Seamless Dynamic Benchmark"){found=window;return false;}}
            return true;
        },IntPtr.Zero);
        return found;
    }
}
'@
if(-not $Worker){
    & "$PSScriptRoot/../../TestSeamless/Invoke-LocalProcess.ps1" -InitializeOnly | Out-Null
    $DesktopName='SeamlessGui_'+[Guid]::NewGuid().ToString('N')
    $desktop=[IsolatedGuiNative]::CreateDesktop($DesktopName,[IntPtr]::Zero,[IntPtr]::Zero,0,0x00C3,[IntPtr]::Zero)
    if($desktop -eq [IntPtr]::Zero){throw [ComponentModel.Win32Exception]::new()}
    $job=[IntPtr]::Zero;$process=[IsolatedGuiNative+ProcessInfo]::new()
    try{
        $job=[IsolatedGuiNative]::CreateJobObject([IntPtr]::Zero,$null)
        $limits=[IsolatedGuiNative+ExtendedLimit]::new();$limits.basic.flags=0x2000
        if(-not [IsolatedGuiNative]::SetInformationJobObject($job,9,[ref]$limits,[Runtime.InteropServices.Marshal]::SizeOf($limits))){throw [ComponentModel.Win32Exception]::new()}
        $hostPath=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $command='"'+$hostPath+'" -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -Worker -Application "'+$Application+'" -OutputDirectory "'+$OutputDirectory+'" -DesktopName '+$DesktopName
        if($ProbeOnly){$command+=' -ProbeOnly'}
        if($LiveOnly){$command+=' -LiveOnly'}
        $startup=[IsolatedGuiNative+StartupInfo]::new();$startup.cb=[Runtime.InteropServices.Marshal]::SizeOf($startup);$startup.desktop='winsta0\'+$DesktopName
        if(-not [IsolatedGuiNative]::CreateProcess($hostPath,[Text.StringBuilder]::new($command),[IntPtr]::Zero,[IntPtr]::Zero,$false,0x08000004,[IntPtr]::Zero,$OutputDirectory,[ref]$startup,[ref]$process)){throw [ComponentModel.Win32Exception]::new()}
        if(-not [IsolatedGuiNative]::AssignProcessToJobObject($job,$process.process)){throw [ComponentModel.Win32Exception]::new()}
        [void][IsolatedGuiNative]::ResumeThread($process.thread)
        $deadline=[DateTime]::UtcNow.AddMinutes(15)
        while([IsolatedGuiNative]::WaitForSingleObject($process.process,1000) -eq 258){if([DateTime]::UtcNow -gt $deadline){throw 'GUI worker timed out'}}
        [uint32]$exitCode=0;[void][IsolatedGuiNative]::GetExitCodeProcess($process.process,[ref]$exitCode)
        if($exitCode -ne 0){throw "GUI worker failed with exit code $exitCode. See $OutputDirectory"}
        Get-Content -LiteralPath (Join-Path $OutputDirectory 'gui-result.json') -Raw
    }finally{
        if($job -ne [IntPtr]::Zero){[void][IsolatedGuiNative]::CloseHandle($job)}
        if($process.thread -ne [IntPtr]::Zero){[void][IsolatedGuiNative]::CloseHandle($process.thread)}
        if($process.process -ne [IntPtr]::Zero){[void][IsolatedGuiNative]::CloseHandle($process.process)}
        [void][IsolatedGuiNative]::CloseDesktop($desktop)
    }
    return
}
$app=$null
try{
    $currentDesktop=[IsolatedGuiNative]::Name([IsolatedGuiNative]::GetThreadDesktop([IsolatedGuiNative]::GetCurrentThreadId()))
    $input=[IsolatedGuiNative]::OpenInputDesktop(0,$false,1)
    try{$inputDesktop=[IsolatedGuiNative]::Name($input)}finally{[void][IsolatedGuiNative]::CloseDesktop($input)}
    if($currentDesktop -ne $DesktopName -or $currentDesktop -eq $inputDesktop){throw 'The GUI worker is not on its isolated desktop'}
    [void][IsolatedGuiNative]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
    Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes,WindowsBase,System.Drawing
    $start=[Diagnostics.ProcessStartInfo]::new($Application);$start.UseShellExecute=$false;$start.WorkingDirectory=$OutputDirectory
    $app=[Diagnostics.Process]::Start($start)
    $created=$app.StartTime.ToUniversalTime().ToString('o');$deadline=[DateTime]::UtcNow.AddSeconds(90)
    do{
        if($app.HasExited){throw "GUI exited before its window was ready. Exit=$($app.ExitCode)"}
        $app.Refresh();$window=[IsolatedGuiNative]::MainWindow($app.Id)
        if($window -ne [IntPtr]::Zero){break}
        Start-Sleep -Milliseconds 250
    }while([DateTime]::UtcNow -lt $deadline)
    if($window -eq [IntPtr]::Zero){throw 'The GUI did not expose its window'}
    if([IO.Path]::GetFullPath($app.MainModule.FileName) -ne [IO.Path]::GetFullPath($Application)){throw 'The launched application path differs'}
    Start-Sleep -Seconds 2
    $root=[Windows.Automation.AutomationElement]::FromHandle($window)
    $elements=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
    $tree=@(foreach($element in $elements){$info=$element.Current;[pscustomobject]@{Name=$info.Name;ID=$info.AutomationId;Type=$info.ControlType.ProgrammaticName;Offscreen=$info.IsOffscreen;PID=$info.ProcessId;Patterns=(@($element.GetSupportedPatterns() | ForEach-Object ProgrammaticName) -join ',')}})
    $tree | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'uia-tree.json') -Encoding utf8
    $checks=[Collections.Generic.List[string]]::new()
    function Find-UI([string]$ID){
        return $root.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::AutomationIdProperty,$ID))
    }
    function Require-UI([string]$ID){
        $element=Find-UI $ID
        if(-not $element){throw "Missing UI element $ID"}
        return $element
    }
    function Invoke-UI([string]$ID){
        $element=Require-UI $ID
        $scroll=$null
        if($element.Current.IsOffscreen -and $element.TryGetCurrentPattern([Windows.Automation.ScrollItemPattern]::Pattern,[ref]$scroll)){$scroll.ScrollIntoView()}
        $element.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    function Show-UI([string]$ID){
        $element=Require-UI $ID;$scroll=$null
        if($element.TryGetCurrentPattern([Windows.Automation.ScrollItemPattern]::Pattern,[ref]$scroll)){$scroll.ScrollIntoView()}
        $until=[DateTime]::UtcNow.AddSeconds(5)
        while($element.Current.IsOffscreen -and [DateTime]::UtcNow -lt $until){Start-Sleep -Milliseconds 50}
        if($element.Current.IsOffscreen){throw "The target remains outside the visible viewport $ID"}
    }
    function Read-UI([string]$ID){
        $element=Require-UI $ID;$pattern=$null
        if($element.TryGetCurrentPattern([Windows.Automation.TextPattern]::Pattern,[ref]$pattern)){return $pattern.DocumentRange.GetText(4096)}
        if($element.TryGetCurrentPattern([Windows.Automation.ValuePattern]::Pattern,[ref]$pattern)){return $pattern.Current.Value}
        return $element.Current.Name
    }
    function Record-Step([string]$Step){
        $checks.Add($Step)
        [ordered]@{Status='Running';ProcessID=$app.Id;CreationUtc=$created;Desktop=$currentDesktop;Checks=@($checks)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'gui-progress.json') -Encoding utf8
    }
    function Capture-UI([string]$Name){
        $rect=[IsolatedGuiNative+Rect]::new();[void][IsolatedGuiNative]::GetWindowRect($window,[ref]$rect)
        $bitmap=[Drawing.Bitmap]::new($rect.right-$rect.left,$rect.bottom-$rect.top)
        $graphics=[Drawing.Graphics]::FromImage($bitmap);$dc=$graphics.GetHdc()
        try{$captured=[IsolatedGuiNative]::PrintWindow($window,$dc,2)}finally{$graphics.ReleaseHdc($dc);$graphics.Dispose()}
        if(-not $captured){$bitmap.Dispose();throw 'Window rendering capture failed'}
        try{$bitmap.Save((Join-Path $OutputDirectory ($Name+'.png')),[Drawing.Imaging.ImageFormat]::Png)}finally{$bitmap.Dispose()}
    }
    function Wait-Run([int]$BeforeRuns){
        $until=[DateTime]::UtcNow.AddMinutes(5)
        do{
            $status=Read-UI '回帰ケース.状態'
            if($status.StartsWith('失敗') -or $status.StartsWith('実行失敗')){throw ($status+' '+(Read-UI '回帰ケース.詳細'))}
            $saved=@(Get-ChildItem -LiteralPath (Join-Path $env:SEAMLESS_BENCHMARK_DATA_ROOT 'RegressionRuns') -Filter '*.jsonl' -ErrorAction SilentlyContinue)
            if($status.StartsWith('完了') -and (Require-UI '回帰ケース.実行').Current.IsEnabled -and $saved.Count -eq $BeforeRuns+1){
                if($status -notmatch '合格 1 / 失敗 0 / 未実行 0'){throw "Unexpected regression result $status"}
                $detail=Read-UI '回帰ケース.詳細'
                if(-not $detail.Contains($caseID) -or -not $detail.Contains('fallback=0') -or -not $detail.Contains('Passed')){throw "Unexpected case detail $detail"}
                return
            }
            Start-Sleep -Milliseconds 500
        }while([DateTime]::UtcNow -lt $until)
        throw "Regression run did not complete. $status"
    }
    Capture-UI 'startup'
    if(-not $ProbeOnly){
        foreach($id in @('動的ライブ.開始','動的ライブ.ランダム探索開始','動的ライブ.停止','動的ライブ.一時停止','動的ライブ.検証対象','動的ライブ.ランキング','動的ライブ.集計','動的ライブ.段階.1','動的ライブ.段階.2','動的ライブ.段階.3','動的詳細.Query再実行','動的詳細.Query縮小')){[void](Require-UI $id)}
        foreach($id in @('動的設定.実行','動的結果.タブ.System','動的結果.タブ.Seamless')){
            if($elements | Where-Object {$_.Current.AutomationId -eq $id}){throw "Obsolete UI control $id"}
        }
        Invoke-UI '動的ライブ.検証対象'
        foreach($id in @('動的ライブ.対象.SystemArray','動的ライブ.型.Int','動的ライブ.探索件数','動的ライブ.シード再現')){[void](Require-UI $id)}
        Capture-UI 'live-targets';Record-Step 'LiveTabControls'
        $targetLabel=Read-UI '動的ライブ.検証対象'
        if($targetLabel -notmatch '有効 44組'){throw "Unexpected default coverage $targetLabel"}
        $targetElements=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
        foreach($element in $targetElements){
            $id=$element.Current.AutomationId
            if($id -match '^動的ライブ\.(対象|型)\.' -and $id -notin @('動的ライブ.対象.SystemArray','動的ライブ.型.Int')){
                $element.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle()
            }
        }
        $count=Require-UI '動的ライブ.探索件数'
        $count.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue('4')
        if((Read-UI '動的ライブ.検証対象') -notmatch '有効 1組 / 探索予定 4件'){throw 'Expected four random queries on one target'}
        Invoke-UI '動的ライブ.検証対象'
        Invoke-UI '動的ライブ.ランダム探索開始'
        $deadline=[DateTime]::UtcNow.AddMinutes(2)
        do{
            $status=Read-UI '動的ライブ.状態'
            if($status.StartsWith('検証エラー')){throw $status}
            if($status -eq '検証が完了しました' -and (Require-UI '動的ライブ.ランダム探索開始').Current.IsEnabled){break}
            Start-Sleep -Milliseconds 200
        }while([DateTime]::UtcNow -lt $deadline)
        if($status -ne '検証が完了しました'){throw "Live exploration did not finish: $status"}
        $ranking=Require-UI '動的ライブ.ランキング'
        $rows=$ranking.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem))
        if($rows.Count -ne 4){throw "Expected four ranked queries, found $($rows.Count)"}
        foreach($index in 1,2){
            if((Read-UI "動的ライブ.段階状態.$index") -ne '○'){throw "Skipped stage $index must remain unexecuted"}
        }
        if((Read-UI '動的ライブ.段階状態.3') -ne '✔'){throw 'Completed random stage must be marked completed'}
        if((Read-UI '動的ライブ.集計') -notmatch '幾何平均'){throw 'The random results did not include a measured speed comparison'}
        $rows[0].GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
        $deadline=[DateTime]::UtcNow.AddSeconds(10)
        do{
            $status=Read-UI '動的ライブ.状態'
            if($status -match 'SystemArray / Int.*#\d+'){break}
            Start-Sleep -Milliseconds 100
        }while([DateTime]::UtcNow -lt $deadline)
        if($status -notmatch 'SystemArray / Int.*#\d+'){throw "Live ranking did not open its saved result: $status"}
        Capture-UI 'live-random';Record-Step 'DirectRandomExplorationAndSavedResult'
        if(-not $LiveOnly){
            Invoke-UI '動的結果.タブ.回帰ケース'
            Start-Sleep -Milliseconds 500
            foreach($id in @('回帰ケース.検索','回帰ケース.読込','回帰ケース.一覧','回帰ケース.実行','回帰ケース.全実行','回帰ケース.中止','回帰ケース.展開コード','回帰ケース.状態','回帰ケース.詳細')){[void](Require-UI $id)}
            $caseID='Test.System.Linq.Intercept.EnumerableComparisonCatalogTests.BenchmarkMethodNames_match_enumerable_result[0]'
            $search=Require-UI '回帰ケース.検索'
            $edit=$search.FindFirst([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Edit))
            if(-not $edit){throw 'The regression search does not expose its text editor'}
            $edit.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).SetValue($caseID)
            Invoke-UI '回帰ケース.読込'
            Record-Step 'SearchEnteredBeforeLoading'
            $until=[DateTime]::UtcNow.AddMinutes(5)
            do{
                $status=Read-UI '回帰ケース.状態'
                if($status -match '^1206ケース'){break}
                if($status.StartsWith('失敗') -or $status.StartsWith('実行失敗')){throw ($status+' '+(Read-UI '回帰ケース.詳細'))}
                Start-Sleep -Milliseconds 500
            }while([DateTime]::UtcNow -lt $until)
            if($status -notmatch '^1206ケース'){throw "Case list did not load. $status"}
            $list=Require-UI '回帰ケース.一覧'
            $items=$list.FindAll([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::ListItem))
            if($items.Count -ne 1){throw "Search should retain one item, found $($items.Count)"}
            $items[0].GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select()
            Start-Sleep -Milliseconds 500
            $detail=Read-UI '回帰ケース.詳細'
            if(-not $detail.Contains('ZLinq') -or -not $detail.Contains('System.Linq') -or -not $detail.Contains('FLinqUnoptimized')){throw 'Comparison implementations are absent from the selected case'}
            Show-UI '回帰ケース.詳細'
            Capture-UI 'selected-case';Record-Step 'CaseSearchSelectionAndComparisonSource'
            Invoke-UI '回帰ケース.実行';Wait-Run 0
            Show-UI '回帰ケース.詳細'
            Capture-UI 'first-result';Record-Step 'SelectedCasePassedWithoutFallback'
            Invoke-UI '回帰ケース.実行';Wait-Run 1
            Show-UI '回帰ケース.詳細'
            Capture-UI 'repeated-result';Record-Step 'RepeatedCasePassedWithoutFallback'
            Invoke-UI '回帰ケース.展開コード'
            $until=[DateTime]::UtcNow.AddSeconds(60)
            do{$expanded=Read-UI '回帰ケース.詳細';if($expanded.Contains('namespace Intercept')){break};Start-Sleep -Milliseconds 500}while([DateTime]::UtcNow -lt $until)
            if(-not $expanded.Contains('namespace Intercept')){throw 'Expanded code did not appear in the GUI'}
            Show-UI '回帰ケース.詳細'
            Capture-UI 'expanded-source';Record-Step 'SavedExpandedSourceVisible'
        }
    }
    $status='Passed';if($ProbeOnly){$status='Probed'}
    $assemblyStream=[IO.File]::OpenRead([IO.Path]::ChangeExtension($Application,'.dll'))
    $sha256=[Security.Cryptography.SHA256]::Create()
    try{$assemblyHash=[BitConverter]::ToString($sha256.ComputeHash($assemblyStream)).Replace('-','')}finally{$assemblyStream.Dispose();$sha256.Dispose()}
    [ordered]@{Status=$status;ProcessID=$app.Id;CreationUtc=$created;Executable=$app.MainModule.FileName;AssemblySHA256=$assemblyHash;Desktop=$currentDesktop;InputDesktop=$inputDesktop;ElementCount=$tree.Count;Checks=@($checks);DataRoot=$env:SEAMLESS_BENCHMARK_DATA_ROOT} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'gui-result.json') -Encoding utf8
}catch{
    $failure=$_
    $failure | Out-String | Set-Content -LiteralPath (Join-Path $OutputDirectory 'gui-error.log') -Encoding utf8
    try{
        if($root){
            $root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition) | ForEach-Object {[pscustomobject]@{Name=$_.Current.Name;ID=$_.Current.AutomationId;Type=$_.Current.ControlType.ProgrammaticName;Offscreen=$_.Current.IsOffscreen;Patterns=(@($_.GetSupportedPatterns() | ForEach-Object ProgrammaticName) -join ',')}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'failure-tree.json') -Encoding utf8
            Capture-UI 'failure'
        }
    }catch{$_ | Out-String | Add-Content -LiteralPath (Join-Path $OutputDirectory 'gui-error.log') -Encoding utf8}
    [ordered]@{Status='Failed';Error=$failure.ToString();Checks=@($checks)} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'gui-progress.json') -Encoding utf8
    throw $failure
}finally{
    if($app -and -not $app.HasExited){[void]$app.CloseMainWindow();if(-not $app.WaitForExit(10000)){$app.Kill()}}
}
