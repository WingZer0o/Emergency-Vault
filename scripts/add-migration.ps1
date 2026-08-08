<#
.SYNOPSIS
    Creates a new EF Core migration for AppDbContext.
.PARAMETER Name
    The migration name (e.g. "AddRefreshTokenIndex").
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Name
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot "backend"
$envFile = Join-Path $repoRoot ".env"

if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#=][^=]*)=(.*)$') {
            Set-Item -Path "env:$($matches[1].Trim())" -Value $matches[2].Trim()
        }
    }
} else {
    Write-Warning "$envFile not found - Infisical login (and thus the migration command) will likely fail without INFISICAL_CLIENT_ID/SECRET/PROJECT_ID set."
}

Push-Location $backendDir
try {
    dotnet ef migrations add $Name `
        --project src/AuthSystem.Infrastructure `
        --startup-project src/AuthSystem.Api `
        --output-dir Persistence/Migrations
} finally {
    Pop-Location
}
