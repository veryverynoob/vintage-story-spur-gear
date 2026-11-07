# Usage (PowerShell):
#   $env:VINTAGE_STORY = "C:\Users\<you>\AppData\Roaming\Vintagestory"   # optional, this is the default
#   .\build.ps1
# Result: spurgears.zip -> put it into the game's Mods folder
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet build -c Release -o out
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

if (Test-Path pkg) { Remove-Item pkg -Recurse -Force }
if (Test-Path spurgears.zip) { Remove-Item spurgears.zip -Force }
New-Item -ItemType Directory pkg | Out-Null
Copy-Item modinfo.json, out\spurgears.dll pkg\
Copy-Item assets pkg\assets -Recurse

# build the zip by hand so entry names use forward slashes (Compress-Archive in PS 5.1 uses backslashes)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$root = (Resolve-Path pkg).Path
$zip = [System.IO.Compression.ZipFile]::Open((Join-Path $PSScriptRoot "spurgears.zip"), "Create")
try {
    Get-ChildItem $root -Recurse -File | ForEach-Object {
        $name = $_.FullName.Substring($root.Length + 1).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $name) | Out-Null
    }
} finally { $zip.Dispose() }

Write-Host "built spurgears.zip"
