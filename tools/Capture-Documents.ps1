<#
.SYNOPSIS
    Read-only capture of the Claude app's page documents and of the newest desktop session record,
    for the checks in research/CAPTURES_2026-10-15_v1.md (section 8 of the solution review).

.DESCRIPTION
    Start it through Capture-Documents.cmd (explorer.exe "<repo>\tools\Capture-Documents.cmd"), then
    switch to the Claude window and do the thing to capture: start a new session and post a prompt
    (check A), show the new-session screen with no folder picked (check B), type /clear in a
    throwaway session (check D). Output is appended to tools\_capture.log next to the .cmd.

    For -Seconds (25) at -IntervalMs (100) it reads, through UI Automation with cached properties
    (the overlay's own way of reading), every Document element of the Claude window in front and
    logs, per document, the Name's length and whether it ends with " - Claude Code" (the title
    itself is never printed) and the ValuePattern value, the page URL, with a local_ session id cut
    to its first 8 hex digits. When they change it also lists the buttons on the lowest row of the
    window (names cut to 40 characters, bounds in physical px), for check B. Beside that it watches
    the newest-written local_*.json record of %APPDATA%\Claude\claude-code-sessions: the file
    name's prefix, createdAt, lastFocusedAt, lastActivityAt, whether cliSessionId and title are
    present (the title's length and source, never its text) and the lineage fields. A line is
    written when something changed, and once a second regardless. Nothing in the app or on disk
    is changed. -AnyClaudeWindow reads the app's main window whether or not it is in front.
#>
[CmdletBinding()]
param(
    [int]$Seconds = 25,
    [int]$IntervalMs = 100,
    [switch]$AnyClaudeWindow
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -Namespace ClaudeCodePlus -Name User32 -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
'@

$store      = Join-Path $env:APPDATA 'Claude\claude-code-sessions'
$sessionUrl = '^https://claude\.ai/epitaxy/local_([0-9a-f]{8})[0-9a-f-]{28}$'
$anyLocalId = 'local_([0-9a-f]{8})[0-9a-f-]{28}'

# The properties the overlay reads, fetched with the search; no live element references kept.
$request = New-Object System.Windows.Automation.CacheRequest
$request.AutomationElementMode = [System.Windows.Automation.AutomationElementMode]::None
$request.Add([System.Windows.Automation.AutomationElement]::NameProperty)
$request.Add([System.Windows.Automation.AutomationElement]::ControlTypeProperty)
$request.Add([System.Windows.Automation.AutomationElement]::BoundingRectangleProperty)
$request.Add([System.Windows.Automation.AutomationElement]::IsOffscreenProperty)
$request.Add([System.Windows.Automation.ValuePattern]::ValueProperty)

$documents  = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Document)
$buttons    = [System.Windows.Automation.AndCondition]::new(
    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button),
    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::IsOffscreenProperty, $false))
$candidates = [System.Windows.Automation.OrCondition]::new($documents, $buttons)

function Get-ClaudeWindow {
    if ($AnyClaudeWindow) {
        $p = Get-Process -Name claude -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
        if ($p) { return @{ Handle = [IntPtr]$p.MainWindowHandle; Note = 'main window' } }
        return @{ Handle = [IntPtr]::Zero; Note = 'no Claude main window' }
    }
    $h = [ClaudeCodePlus.User32]::GetForegroundWindow()
    $procId = [uint32]0
    [void][ClaudeCodePlus.User32]::GetWindowThreadProcessId($h, [ref]$procId)
    $name = ''
    if ($procId -ne 0) {
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($proc) { $name = $proc.ProcessName }
    }
    if ($name -ieq 'claude') { return @{ Handle = $h; Note = 'in front' } }
    return @{ Handle = [IntPtr]::Zero; Note = "not in front (foreground: $name)" }
}

# The URL as logged: a session id cut to its first 8 hex digits; a file URL cut to its tail, with the app version.
function Format-Url([object]$value) {
    if ($null -eq $value) { return 'url=null' }
    if ($value -eq [System.Windows.Automation.AutomationElement]::NotSupported) { return 'url=not-supported' }
    $s = [string]$value
    if ($s -eq '') { return 'url=empty' }
    if ($s -match $sessionUrl) { return "url=epitaxy/local_$($Matches[1])..." }
    if ($s.StartsWith('file:///')) {
        $version = 'unknown'
        if ($s -match 'Claude_([0-9.]+)_') { $version = $Matches[1] }
        return "url=file:.../$($s.Substring($s.LastIndexOf('/') + 1)) (app $version)"
    }
    $s = [regex]::Replace($s, $anyLocalId, 'local_$1...')
    if ($s.Length -gt 100) { $s = $s.Substring(0, 100) + '...' }
    return "url=$s"
}

function Format-Document([string]$name, [object]$value) {
    $titled = 'no'
    if ($name.EndsWith(' - Claude Code')) { $titled = 'yes' }
    return "[doc name-len=$($name.Length) titled=$titled $(Format-Url $value)]"
}

# The buttons on the lowest row of the window: names cut to 40 characters, with bounds.
function Format-LowestRow([System.Collections.Generic.List[object]]$all) {
    if ($all.Count -eq 0) { return 'row: no buttons' }
    $lowest = $all | Sort-Object { $_.Y + $_.H } -Descending | Select-Object -First 1
    $centre = $lowest.Y + ($lowest.H / 2.0)
    $row = $all | Where-Object { [Math]::Abs(($_.Y + ($_.H / 2.0)) - $centre) -lt $lowest.H } | Sort-Object X
    $parts = foreach ($b in $row) {
        $n = $b.Name
        if ($n.Length -gt 40) { $n = $n.Substring(0, 40) + '...' }
        "'$n' $($b.X),$($b.Y) $($b.W)x$($b.H)"
    }
    return "row: " + ($parts -join ' | ')
}

function Read-Window([IntPtr]$handle) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $activation = $request.Activate()
    try { $found = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $candidates) }
    finally { $activation.Dispose() }
    $docs = New-Object System.Collections.Generic.List[string]
    $btns = New-Object System.Collections.Generic.List[object]
    foreach ($e in $found) {
        $c = $e.Cached
        if ($c.ControlType -eq [System.Windows.Automation.ControlType]::Document) {
            $docs.Add((Format-Document ([string]$c.Name) ($e.GetCachedPropertyValue([System.Windows.Automation.ValuePattern]::ValueProperty, $true))))
        }
        else {
            $r = $c.BoundingRectangle
            if (-not $r.IsEmpty -and $r.Width -gt 0 -and $r.Height -gt 0) {
                $btns.Add([pscustomobject]@{ Name = [string]$c.Name; X = [int]$r.X; Y = [int]$r.Y; W = [int]$r.Width; H = [int]$r.Height })
            }
        }
    }
    return @{ Docs = "docs=$($docs.Count) $($docs -join ' ')"; Row = (Format-LowestRow $btns) }
}

function Format-Ms([object]$ms) {
    if ($null -eq $ms) { return '-' }
    try { return [DateTimeOffset]::FromUnixTimeMilliseconds([long]$ms).ToLocalTime().ToString('HH:mm:ss.fff') } catch { return "?$ms" }
}

$recordMemo = @{ Key = ''; Text = '' }
function Get-NewestRecord {
    if (-not (Test-Path -LiteralPath $store)) { return 'record: no store folder' }
    $newest = $null
    foreach ($f in [System.IO.DirectoryInfo]::new($store).EnumerateFiles('local_*.json', [System.IO.SearchOption]::AllDirectories)) {
        if ($null -eq $newest -or $f.LastWriteTimeUtc -gt $newest.LastWriteTimeUtc) { $newest = $f }
    }
    if ($null -eq $newest) { return 'record: none' }
    $key = "$($newest.FullName)|$($newest.Length)|$($newest.LastWriteTimeUtc.Ticks)"
    if ($key -eq $recordMemo.Key) { return $recordMemo.Text }
    $prefix = $newest.Name
    if ($prefix -match $anyLocalId) { $prefix = "local_$($Matches[1])..." }
    $text = "record=$prefix written=$($newest.LastWriteTime.ToString('HH:mm:ss.fff')) size=$($newest.Length)"
    try {
        $j = [System.IO.File]::ReadAllText($newest.FullName) | ConvertFrom-Json
        $cli = 'no'
        if ($j.PSObject.Properties['cliSessionId'] -and $j.cliSessionId) { $cli = "yes($(([string]$j.cliSessionId).Substring(0, [Math]::Min(8, ([string]$j.cliSessionId).Length))))" }
        $title = 'no'
        if ($j.PSObject.Properties['title'] -and $j.title) { $title = "yes(len $(([string]$j.title).Length))" }
        $source = '-'
        if ($j.PSObject.Properties['titleSource']) { $source = [string]$j.titleSource }
        $prior = 0
        if ($j.PSObject.Properties['priorCliSessionIds'] -and $j.priorCliSessionIds) { $prior = @($j.priorCliSessionIds).Count }
        $preClear = 'no'
        if ($j.PSObject.Properties['preClearCliSessionId'] -and $j.preClearCliSessionId) { $preClear = 'yes' }
        $archived = 'no'
        if ($j.PSObject.Properties['isArchived'] -and $j.isArchived) { $archived = 'yes' }
        $text += " created=$(Format-Ms $j.createdAt) focused=$(Format-Ms $j.lastFocusedAt) activity=$(Format-Ms $j.lastActivityAt) cli=$cli title=$title titleSource=$source prior=$prior preClear=$preClear archived=$archived"
    }
    catch {
        $text += " unreadable ($($_.Exception.GetType().Name): being written?)"
    }
    $recordMemo.Key = $key
    $recordMemo.Text = $text
    return $text
}

$windowNote = 'the foreground window when it is Claude'
if ($AnyClaudeWindow) { $windowNote = 'the Claude main window' }
Write-Output ("==== Capture-Documents {0:yyyy-MM-dd HH:mm:ss} for {1} s every {2} ms; window: {3}; store: {4}" -f (Get-Date), $Seconds, $IntervalMs, $windowNote, $store)

$end = (Get-Date).AddSeconds($Seconds)
$lastLine = ''
$lastRow = ''
$nextBeat = Get-Date
while ((Get-Date) -lt $end) {
    $t0 = Get-Date
    $w = Get-ClaudeWindow
    $row = ''
    if ($w.Handle -eq [IntPtr]::Zero) {
        $line = "claude: $($w.Note)"
    }
    else {
        try {
            $read = Read-Window $w.Handle
            $line = "claude: $($w.Note) $($read.Docs)"
            $row = $read.Row
        }
        catch {
            $line = "claude: $($w.Note) uia-error: $($_.Exception.GetType().Name)"
        }
    }
    $line += ' | ' + (Get-NewestRecord)
    $took = [int]((Get-Date) - $t0).TotalMilliseconds
    $now = Get-Date
    if ($line -ne $lastLine -or $now -ge $nextBeat) {
        Write-Output ("{0:HH:mm:ss.fff} ({1,3} ms) {2}" -f $now, $took, $line)
        $lastLine = $line
        $nextBeat = $now.AddSeconds(1)
    }
    if ($row -ne '' -and $row -ne $lastRow) {
        Write-Output ("{0:HH:mm:ss.fff}          {1}" -f $now, $row)
        $lastRow = $row
    }
    $rest = $IntervalMs - [int]((Get-Date) - $t0).TotalMilliseconds
    if ($rest -gt 0) { Start-Sleep -Milliseconds $rest }
}
Write-Output ("==== done {0:HH:mm:ss.fff}" -f (Get-Date))
