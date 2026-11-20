<#
.SYNOPSIS
  One-command setup for the Agentic Engineering Kit on Windows. Safe to re-run.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1                 # core setup
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1 -WithLocalLlm   # also the private code index (Ollama optional)
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1 -Publish        # also create the public GitHub repos and push
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1 -PublishOnly    # only publish (skips tests and setup); safe to re-run
#>
param(
    [switch]$WithLocalLlm,
    [switch]$Publish,
    [switch]$PublishOnly,
    [string]$GitHubUser = "Legion91919",
    [string[]]$IndexRoots = @("C:\Dev", "D:\dev")
)
$ErrorActionPreference = "Stop"
$Kit = Split-Path -Parent $PSScriptRoot
$Workspace = Split-Path -Parent $Kit
$Showcases = @(Get-ChildItem -Directory (Join-Path $Workspace "showcase") -ErrorAction SilentlyContinue)
$Blocks = Join-Path $Workspace "agentic-building-blocks"
$Casino = Join-Path $Workspace "casino-platform"
$Repos = @($Kit) + @(@($Blocks, $Casino) | Where-Object { Test-Path $_ }) + ($Showcases | ForEach-Object { $_.FullName })
$BinDir = Join-Path $env:USERPROFILE ".agentic\bin\arch-advisor"
$AdvisorExe = Join-Path $BinDir "arch-advisor.exe"
$Warnings = New-Object System.Collections.Generic.List[string]

function Write-Step([string]$Text) { Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Assert-Command([string]$Name, [string]$Hint) {
    $c = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $c) { throw "'$Name' not found. $Hint" }
    return $c.Source
}
function Invoke-Native([string]$Exe, [string[]]$Arguments, [string]$What) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)." }
}
function Invoke-Quiet([string]$Exe, [string[]]$Arguments) {
    # Windows PowerShell 5.1 turns redirected stderr into terminating errors under "Stop"; relax it here.
    $old = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    try { & $Exe @Arguments *> $null } finally { $ErrorActionPreference = $old }
    return $LASTEXITCODE
}
function Find-PythonExe {
    foreach ($name in @("python", "py")) {
        $c = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue |
             Where-Object { $_.Source -notlike "*\WindowsApps\*" } | Select-Object -First 1
        if ($c) { return $c.Source }
    }
    throw "Python 3.9+ not found (the Microsoft Store stub doesn't count). winget install Python.Python.3.12"
}

if ($PublishOnly) { $Publish = $true }
$Git = Assert-Command "git" "winget install Git.Git"
$Py = Find-PythonExe
if (-not $PublishOnly) {
Write-Step "Checking tools"
$Dotnet = Assert-Command "dotnet" "winget install Microsoft.DotNet.SDK.8"
Write-Host "dotnet $(& $Dotnet --version) | $(& $Py --version) | $(& $Git --version)"

Write-Step "Testing the kit (C# + Python)"
Push-Location $Kit
try {
    Invoke-Native $Dotnet @("test", "-c", "Release", "--nologo", "--logger", "console;verbosity=normal") "dotnet test"
    Invoke-Native $Py @("-m", "unittest", "discover", "-s", "tests/tools") "Python tests"
} finally { Pop-Location }

Write-Step "Installing arch-advisor to $BinDir"
Invoke-Native $Dotnet @("publish", (Join-Path $Kit "src\ArchAdvisor.Mcp"), "-c", "Release", "-o", $BinDir, "-p:UseAppHost=true", "--nologo") "dotnet publish"
if (-not (Test-Path $AdvisorExe)) { throw "Publish finished but $AdvisorExe is missing." }
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (($userPath -split ";") -notcontains $BinDir) {
    [Environment]::SetEnvironmentVariable("Path", ($userPath.TrimEnd(";") + ";" + $BinDir), "User")
    Write-Host "Added $BinDir to your user PATH (open a new terminal to pick it up)."
}
$env:Path = "$env:Path;$BinDir"
Invoke-Native $AdvisorExe @("recommend", "MoneyMovement", "SmallTeam") "arch-advisor smoke test"

Write-Step "Privacy hooks and hidden .agentic folders"
foreach ($r in $Repos) {
    $name = Split-Path -Leaf $r
    if (Test-Path (Join-Path $r ".git")) {
        Invoke-Native $Py @((Join-Path $r "tools\privacy_guard.py"), "--install", "--repo", $r) "Hook install in $name"
    }
    $ctx = Join-Path $r ".agentic"
    if (Test-Path $ctx) { (Get-Item $ctx -Force).Attributes = "Directory,Hidden" }
}

Write-Step "Registering MCP servers with Claude Code (user scope)"
$Claude = Get-Command claude -ErrorAction SilentlyContinue | Select-Object -First 1
if ($Claude) {
    foreach ($s in @("arch-advisor", "my-code")) { [void](Invoke-Quiet "claude" @("mcp", "remove", $s, "-s", "user")) }
    # Same command as the repos' .mcp.json ("arch-advisor" on PATH), so user and project scopes don't conflict.
    Invoke-Native "claude" @("mcp", "add", "-s", "user", "arch-advisor", "--", "arch-advisor") "Registering arch-advisor"
    if ($WithLocalLlm) {
        Invoke-Native "claude" @("mcp", "add", "-s", "user", "my-code", "--", $Py, (Join-Path $Kit "local-llm\my_code_mcp.py")) "Registering my-code"
    }
    & claude mcp list
} else {
    $Warnings.Add("Claude Code CLI not found: MCP servers not registered (each repo's .mcp.json still works).")
}

if ($WithLocalLlm) {
    Write-Step "Private code index"
    $Ollama = Get-Command ollama -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($Ollama) {
        Invoke-Native $Ollama.Source @("pull", "nomic-embed-text") "ollama pull nomic-embed-text"
        Invoke-Native $Ollama.Source @("pull", "qwen2.5-coder:7b") "ollama pull qwen2.5-coder:7b"
    } else {
        $Warnings.Add("Ollama not installed: the index uses the simpler keyword embedder for now. Install with 'winget install Ollama.Ollama' and re-run with -WithLocalLlm to upgrade it.")
    }
    $roots = @($IndexRoots | Where-Object { Test-Path $_ })
    Invoke-Native $Py (@((Join-Path $Kit "local-llm\my_code_index.py"), "build") + $roots) "Building the code index"
    & $Py (Join-Path $Kit "local-llm\my_code_index.py") stats
}

} # end of setup steps skipped by -PublishOnly

Write-Step "Privacy guard over every repo"
$blocked = @()
foreach ($r in $Repos) {
    & $Py (Join-Path $r "tools\privacy_guard.py") --all --repo $r
    if ($LASTEXITCODE -ne 0) { $blocked += (Split-Path -Leaf $r) }
}
if ($blocked.Count -gt 0) { throw "Privacy guard found problems in: $($blocked -join ', '). Fix them before publishing." }

if ($Publish) {
    Write-Step "Publishing to GitHub as $GitHubUser"
    $Gh = Assert-Command "gh" "winget install --id GitHub.cli, open a new terminal, then: gh auth login"
    Write-Host "This creates PUBLIC repos (existing ones are just pushed):" -ForegroundColor Yellow
    $Repos | ForEach-Object { Write-Host "  $GitHubUser/$(Split-Path -Leaf $_)" }
    if ((Read-Host "Type YES to continue") -ne "YES") {
        $Warnings.Add("Publishing skipped.")
    } else {
        foreach ($r in $Repos) {
            $name = Split-Path -Leaf $r
            $url = "https://github.com/$GitHubUser/$name.git"
            if ((Invoke-Quiet $Gh @("repo", "view", "$GitHubUser/$name")) -ne 0) {
                # GitHub limits how fast repos can be created: pause between creates and retry when told to slow down.
                $created = $false
                for ($attempt = 1; $attempt -le 6 -and -not $created; $attempt++) {
                    if ((Invoke-Quiet $Gh @("repo", "create", "$GitHubUser/$name", "--public")) -eq 0) { $created = $true; break }
                    $wait = 60 * $attempt
                    Write-Host "  GitHub asked to slow down creating $name; waiting $wait s (attempt $attempt of 6)..." -ForegroundColor Yellow
                    Start-Sleep -Seconds $wait
                }
                if (-not $created) { throw "Could not create $name yet. Wait a while and re-run with -PublishOnly; finished repos are skipped." }
                Write-Host "  created $GitHubUser/$name"
                Start-Sleep -Seconds 20
            }
            $remotes = @(& $Git -C $r remote)
            if ($remotes -notcontains "origin") { Invoke-Native $Git @("-C", $r, "remote", "add", "origin", $url) "Adding origin to $name" }
            Invoke-Native $Git @("-C", $r, "push", "-u", "origin", "main") "Pushing $name"
        }
        [void](Invoke-Quiet $Gh @("variable", "set", "KIT_REPO", "--body", "$GitHubUser/agentic-engineering-kit", "--repo", "$GitHubUser/agentic-engineering-kit"))
    }
}

Write-Host "`nSetup complete." -ForegroundColor Green
foreach ($w in $Warnings) { Write-Host "  ! $w" -ForegroundColor Yellow }
Write-Host "Try (new terminal):  arch-advisor recommend DocumentIngestion RegulatoryAudit SmallTeam"
Write-Host "Then open a showcase in Claude Code and run /baton."
