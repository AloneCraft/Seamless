Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead("G:\GitHub\Seamless\Work\VirtualDbFeed\Seamless.0.0.0.nupkg")
$entry = $zip.Entries | Where-Object { $_.Name.EndsWith(".nuspec") }
$stream = $entry.Open()
$reader = New-Object System.IO.StreamReader($stream)
$content = $reader.ReadToEnd()
$reader.Close()
$stream.Close()
$zip.Dispose()
Write-Host $content
