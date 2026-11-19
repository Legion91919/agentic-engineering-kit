<#
.SYNOPSIS
  One-command setup for the Agentic Engineering Kit on Windows.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1                 # core setup
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1 -WithLocalLlm   # also Ollama + private code index
  powershell -ExecutionPolicy Bypass -File tools\setup.ps1 -Publish        # also create the public GitHub repos and push

  Steps: check tools, test, install the arch-advisor global tool, install privacy hooks,
  hide .agentic folders, register MCP servers with Claude Code, run the privacy guard on
  every repo, optionally build the local index and publish.
#>
param(
    [switch]$WithLocalLlm,
    [switch]$Publish,
    [string]$GitHubUser = "Legion91919",
    [string[]]$IndexRoots = @("C:\Dev", "D:\dev")
)
$ErrorActionPreference = "Stop"
$Kit = Split-Path -Parent $PSScriptRoot
$Workspace = Split-Path -Parent $Kit
$Showcases = @(Get-ChildItem -Directory (Join-Path $Workspace "showcase") -ErrorAction SilentlyContinue)
$Repos = @($Kit) + ($Showcases | ForEach-Object { $_.FullName })

function Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }
function Need($cmd, $hint) {
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue)) { throw "'$cmd' not found. $hint" }
}
function Python {
    if (Get-Command python -ErrorAction SilentlyContinue) { return "python" }
    if (Get-Command py -ErrorAction SilentlyContinue) { return "py" }
    throw "Python 3.9+ not found. winget install Python.Python.3.12"
}

Step "Checking tools"
Need dotnet "winget install Microsoft.DotNet.SDK.8"
Need git "winget install Git.Git"
$Py = Python
& dotnet --version | Out-Host

Step "Testing the kit (C# + Python)"
Push-Location $Kit
& dotnet test -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }
& $Py -m unittest discover -s tests/tools
if ($LASTEXITCODE -ne 0) { throw "Python tests failed" }

Step "Installing the arch-advisor global tool"
& dotnet pack src/ArchAdvisor.Mcp -c Release -o nupkg | Out-Null
& dotnet tool update -g arch-advisor --add-source nupkg --ignore-failed-sources
if ($LASTEXITCODE -ne 0) { & dotnet tool install -g arch-advisor --add-source nupkg --ignore-failed-sources }
Pop-Location

Step "Privacy hooks and hidden .agentic folders"
foreach ($r in $Repos) {
    $guard = Join-Path $r "tools\privacy_guard.py"
    if (Test-Path (Join-Path $r ".git")) { & $Py $guard --install --repo $r | Out-Host }
    $ctx = Join-Path $r ".agentic"
    if (Test-Path $ctx) { attrib +h $ctx }
}

Step "Registering MCP servers with Claude Code (user scope)"
if (Get-Command claude -ErrorAction SilentlyContinue) {
    & claude mcp add -s user arch-advisor -- arch-advisor 2>$null
    if ($WithLocalLlm) { & claude mcp add -s user my-code -- $Py (Join-Path $Kit "local-llm\my_code_mcp.py") 2>$null }
    & claude mcp list
} else {
    Write-Host "Claude Code CLI not found; skipping. The repos' .mcp.json files still register the servers per project."
}

if ($WithLocalLlm) {
    Step "Local model and private code index"
    Need ollama "winget install Ollama.Ollama"
    & ollama pull nomic-embed-text
    & ollama pull qwen2.5-coder:7b
    $roots = $IndexRoots | Where-Object { Test-Path $_ }
    & $Py (Join-Path $Kit "local-llm\my_code_index.py") build @roots
    & $Py (Join-Path $Kit "local-llm\my_code_index.py") stats
}

Step "Privacy guard over every repo"
$blocked = @()
foreach ($r in $Repos) {
    & $Py (Join-Path $r "tools\privacy_guard.py") --all --repo $r
    if ($LASTEXITCODE -ne 0) { $blocked += $r }
}
if ($blocked.Count -gt 0) { throw "Privacy guard found problems in: $($blocked -join ', '). Fix them before publishing." }

if ($Publish) {
    Step "Publishing to GitHub as $GitHubUser"
    Need gh "winget install GitHub.cli, then: gh auth login"
    Write-Host "This creates PUBLIC repos for:" -ForegroundColor Yellow
    $Repos | ForEach-Object { Write-Host "  $GitHubUser/$(Split-Path -Leaf $_)" }
    if ((Read-Host "Type YES to continue") -ne "YES") { Write-Host "Skipped publishing."; exit 0 }
    foreach ($r in $Repos) {
        $name = Split-Path -Leaf $r
        & gh repo view "$GitHubUser/$name" 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "$name already exists; pushing."
            & git -C $r push -u origin main
        } else {
            & gh repo create "$GitHubUser/$name" --public --source $r --remote origin --push
        }
    }
    & gh variable set KIT_REPO --body "$GitHubUser/agentic-engineering-kit" --repo "$GitHubUser/agentic-engineering-kit" 2>$null
}

Write-Host "`nSetup complete." -ForegroundColor Green
Write-Host "Try:  arch-advisor recommend DocumentIngestion RegulatoryAudit SmallTeam"
Write-Host "Then open a showcase in Claude Code and run /baton."
