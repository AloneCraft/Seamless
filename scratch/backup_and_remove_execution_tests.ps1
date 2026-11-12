$backupDir = "F:\Seamless\Backups\ExecutionTests_20261107"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Copy-Item -Path "Execution.Tests" -Destination $backupDir -Recurse -Force
Write-Host "Execution.Tests backed up to $backupDir"
Remove-Item -Path "Execution.Tests" -Recurse -Force
Write-Host "Execution.Tests removed successfully."
