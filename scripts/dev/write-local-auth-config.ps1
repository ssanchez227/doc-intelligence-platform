<#
.SYNOPSIS
Writes the Entra settings for local runs from the infra/identity outputs.

.DESCRIPTION
Reads tenant and client IDs from `terraform output` in infra/identity (run
`terraform init -backend-config=backend.hcl` there first), then writes:
- src/web/.env.development.local (gitignored), read by `npm run dev`
- the API's user secrets (Auth:TenantId, Auth:Audience, Cors:AllowedOrigins:0,
  and Groups:<key> for each test group), which `dotnet run` loads in Development.
None of these values are secrets, but they belong to one tenant, so they stay out of the repo.
#>
[CmdletBinding()]
param(
    [string]$WebOrigin = 'http://localhost:5173',
    [string]$ApiUrl = 'http://localhost:5299'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."

Push-Location "$root/infra/identity"
try {
    $out = terraform output -json | ConvertFrom-Json
}
finally {
    Pop-Location
}
if (-not $out.tenant_id) { throw 'No outputs from infra/identity. Run terraform init there first.' }

$envFile = "$root/src/web/.env.development.local"
@(
    "VITE_API_BASE_URL=$ApiUrl"
    "VITE_ENTRA_TENANT_ID=$($out.tenant_id.value)"
    "VITE_ENTRA_CLIENT_ID=$($out.web_client_id.value)"
    "VITE_API_SCOPE=$($out.api_scope.value)"
) | Set-Content -Path $envFile -Encoding ascii
Write-Host "Wrote $envFile"

$api = "$root/src/api/DocIntel.Api"
dotnet user-secrets set 'Auth:TenantId' $out.tenant_id.value --project $api | Out-Null
dotnet user-secrets set 'Auth:Audience' $out.api_client_id.value --project $api | Out-Null
dotnet user-secrets set 'Cors:AllowedOrigins:0' $WebOrigin --project $api | Out-Null
# Group keys used by the API (readers, contributors, ...) -> this tenant's group IDs.
foreach ($group in $out.group_object_ids.value.PSObject.Properties) {
    dotnet user-secrets set "Groups:$($group.Name)" $group.Value --project $api | Out-Null
}
Write-Host 'Set the API user secrets (Auth, Cors, Groups).'
