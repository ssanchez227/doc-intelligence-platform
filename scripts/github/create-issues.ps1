<#
.SYNOPSIS
  Creates the scenario labels, milestones and issues from docs/acceptance-criteria.md.
.DESCRIPTION
  Safe to re-run: labels are upserted, and milestones and issues that already exist
  (matched by title) are skipped. Requires the GitHub CLI, signed in with the repo scope.
.EXAMPLE
  ./scripts/github/create-issues.ps1 -Repo ssanchez227/doc-intelligence-platform -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $Repo,
    [string] $CriteriaPath = (Join-Path $PSScriptRoot '..\..\docs\acceptance-criteria.md')
)

$ErrorActionPreference = 'Stop'

$milestones = [ordered]@{
    'M0' = 'M0 Foundation'
    'M1' = 'M1 Content lifecycle'
    'M2' = 'M2 Access and reading'
    'M3' = 'M3 Ingestion and search'
    'M4' = 'M4 Questions and citations'
    'M5' = 'M5 Review and approval'
    'M6' = 'M6 Audit and administration'
    'M7' = 'M7 Modern capabilities'
    'M8' = 'M8 Hardening and demo'
}
$priorityColors = @{ 'Must' = 'b60205'; 'Should' = 'fbca04'; 'Could' = '0e8a16' }

function Invoke-Gh {
    param([Parameter(ValueFromRemainingArguments)] [string[]] $GhArgs)
    $out = & gh @GhArgs
    if ($LASTEXITCODE -ne 0) { throw "gh $($GhArgs -join ' ') failed with exit code $LASTEXITCODE" }
    $out
}

# Parse the criteria file into one record per scenario.
$lines = Get-Content -LiteralPath $CriteriaPath -Encoding UTF8
$scenarios = @()
$current = $null
foreach ($line in $lines) {
    if ($line -match '^### (UC-\d{2}) (.+)$') {
        if ($current) { $scenarios += $current }
        $current = [pscustomobject]@{ Id = $Matches[1]; Name = $Matches[2]; Meta = $null; Body = New-Object System.Collections.Generic.List[string] }
    }
    elseif ($line -match '^## ') {
        if ($current) { $scenarios += $current; $current = $null }
    }
    elseif ($current) {
        if (-not $current.Meta -and $line -match '^Actor: (.+?) \S Priority: (Must|Should|Could) \S Milestone: (M\d)') {
            $current.Meta = [pscustomobject]@{ Actor = $Matches[1]; Priority = $Matches[2]; Milestone = $Matches[3]; Line = $line }
        }
        else { $current.Body.Add($line) }
    }
}
if ($current) { $scenarios += $current }

$bad = $scenarios | Where-Object { -not $_.Meta }
if ($bad) { throw "Missing Actor/Priority/Milestone line for: $($bad.Id -join ', ')" }
Write-Host "Parsed $($scenarios.Count) scenarios."

# Labels: priorities and one per scenario.
foreach ($p in $priorityColors.Keys) {
    if ($PSCmdlet.ShouldProcess($p, 'upsert label')) {
        Invoke-Gh label create $p --repo $Repo --color $priorityColors[$p] --description "$p priority" --force | Out-Null
    }
}
foreach ($s in $scenarios) {
    if ($PSCmdlet.ShouldProcess($s.Id, 'upsert label')) {
        Invoke-Gh label create $s.Id --repo $Repo --color 1d76db --description $s.Name --force | Out-Null
    }
}

# Milestones, one per build chat.
$existing = (Invoke-Gh api "repos/$Repo/milestones?state=all&per_page=100") | ConvertFrom-Json
foreach ($title in $milestones.Values) {
    if ($existing | Where-Object { $_.title -eq $title }) { Write-Host "Milestone exists: $title"; continue }
    if ($PSCmdlet.ShouldProcess($title, 'create milestone')) {
        Invoke-Gh api "repos/$Repo/milestones" -f "title=$title" | Out-Null
        Write-Host "Created milestone: $title"
    }
}

# Issues, one per scenario.
$existingIssues = (Invoke-Gh issue list --repo $Repo --state all --limit 500 --json title) | ConvertFrom-Json
$tmp = New-TemporaryFile
try {
    foreach ($s in $scenarios) {
        $title = "$($s.Id) $($s.Name)"
        if ($existingIssues | Where-Object { $_.title -eq $title }) { Write-Host "Issue exists: $title"; continue }

        $text = ($s.Body -join "`n").Trim()
        $body = "$($s.Meta.Line)`n`n## Acceptance criteria`n`n$text`n`nSource: ``docs/acceptance-criteria.md`` and the scenario table in ``docs/plan.md``.`n"
        [IO.File]::WriteAllText($tmp.FullName, $body, (New-Object System.Text.UTF8Encoding $false))

        if ($PSCmdlet.ShouldProcess($title, 'create issue')) {
            Invoke-Gh issue create --repo $Repo --title $title --body-file $tmp.FullName `
                --label $s.Id --label $s.Meta.Priority --milestone $milestones[$s.Meta.Milestone] | Out-Null
            Write-Host "Created issue: $title"
        }
    }
}
finally { Remove-Item $tmp.FullName -ErrorAction SilentlyContinue }
