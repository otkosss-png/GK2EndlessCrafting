param([string]$GameDir = "E:\SteamLibrary\steamapps\common\Graveyard Keeper 2")
$dll = Join-Path $PSScriptRoot "..\src\GK2EndlessCrafting\bin\Release\GK2EndlessCrafting.dll"
$dst = Join-Path $GameDir "BepInEx\plugins\GK2EndlessCrafting"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Get-Process GraveyardKeeper2 -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
Copy-Item $dll (Join-Path $dst "GK2EndlessCrafting.dll") -Force
Remove-Item (Join-Path $GameDir "BepInEx\cache") -Recurse -Force -ErrorAction SilentlyContinue
Start-Process "steam://rungameid/4358690"
Write-Output "deployed"
