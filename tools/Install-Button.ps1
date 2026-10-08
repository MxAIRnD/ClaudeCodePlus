<#
.SYNOPSIS
    Publishes ClaudeCodePlus (Release, framework-dependent) into the repo's publish\ folder,
    creates the Startup shortcut "Claude Code Plus button" (ClaudeCodePlus.exe --button: the
    "All projects" button beside the Claude app's new-session chip row) and starts the button.

.DESCRIPTION
    Run it through Install-Button.cmd started with explorer.exe, never from a Claude Code shell:
    a shell inside the packaged Claude app redirects %APPDATA% writes into a private store, so
    the shortcut would look created and not exist, and the button would start inside that box.
    Verify afterwards with Verify-Button.cmd (a second, unpackaged read).

    Always republishes (the shortcut points into publish\, so a rerun updates the app in place);
    a running button from publish\ is stopped first (it would lock the exe) and started again at
    the end. An existing shortcut is left untouched unless -Force is given. Writes nothing else
    outside the repo.
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repo       = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repo 'src\App.Host\App.Host.csproj'
$publishDir = Join-Path $repo 'publish'
$exe        = Join-Path $publishDir 'ClaudeCodePlus.exe'
$startup    = [Environment]::GetFolderPath('Startup')
$lnk        = Join-Path $startup 'Claude Code Plus button.lnk'

Write-Host ("Run at      : {0:yyyy-MM-dd HH:mm:ss}" -f (Get-Date))
Write-Host "Repo        : $repo"
Write-Host "Publish to  : $publishDir"
Write-Host "Startup     : $lnk"

$running = @(Get-Process -Name 'ClaudeCodePlus' -ErrorAction Ignore | Where-Object { $_.Path -eq $exe })
foreach ($p in $running) {
    Write-Host ("Stopping    : running button pid {0} ({1})" -f $p.Id, $p.Path)
    Stop-Process -Id $p.Id
    $p.WaitForExit(10000) | Out-Null
}

& dotnet publish $project -c Release -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $exe)) { throw "Published exe not found: $exe" }
Write-Host ("Published   : {0} ({1} bytes)" -f $exe, (Get-Item -LiteralPath $exe).Length)

if (-not (Test-Path -LiteralPath $startup)) {
    throw "Startup folder not found: $startup"
}

if ((Test-Path -LiteralPath $lnk) -and -not $Force) {
    Write-Host 'Shortcut    : already exists, left untouched (rerun with -Force to recreate it)'
}
else {
    $shell    = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($lnk)
    $shortcut.TargetPath       = $exe
    $shortcut.Arguments        = '--button'
    $shortcut.WorkingDirectory = $publishDir
    $shortcut.Description      = 'The "All projects" button beside the Claude app''s new-session chip row'
    $shortcut.IconLocation     = "$exe,0"
    $shortcut.Save()

    $item  = Get-Item -LiteralPath $lnk
    $check = $shell.CreateShortcut($lnk)
    Write-Host ("Created     : {0} ({1} bytes, {2:yyyy-MM-dd HH:mm:ss})" -f $item.FullName, $item.Length, $item.LastWriteTime)
    Write-Host ("Target      : {0} {1}" -f $check.TargetPath, $check.Arguments)
}

$button = Start-Process -FilePath $exe -ArgumentList '--button' -WorkingDirectory $publishDir -PassThru
Write-Host ("Button      : started, pid {0}" -f $button.Id)
Write-Host 'INSTALL_OK'
