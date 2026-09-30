# Creates a local-only demo admin configuration without printing credentials.
# Existing credentials are preserved. Run from the project root.
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot '..\IntelligentProgrammingPlatform.csproj'
[xml]$project = Get-Content -LiteralPath $projectPath
$secretsId = [string]$project.Project.PropertyGroup.UserSecretsId
if ([string]::IsNullOrWhiteSpace($secretsId)) {
    throw 'Initialize User Secrets with dotnet user-secrets init first.'
}
$secretPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretsId\secrets.json"
$settings = @{}
if (Test-Path -LiteralPath $secretPath) {
    $existing = Get-Content -LiteralPath $secretPath -Raw | ConvertFrom-Json
    foreach ($property in $existing.PSObject.Properties) { $settings[$property.Name] = $property.Value }
}
if (-not $settings['SeedAdmin:Email']) { $settings['SeedAdmin:Email'] = 'admin@local.dev' }
if (-not $settings['SeedAdmin:DisplayName']) { $settings['SeedAdmin:DisplayName'] = 'Development Admin' }
if (-not $settings['SeedAdmin:Password']) {
    $randomBytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($randomBytes) } finally { $generator.Dispose() }
    $settings['SeedAdmin:Password'] = 'Aa1!' + [Convert]::ToBase64String($randomBytes)
}
# Passing JSON via standard input keeps the password out of command arguments.
$settings | ConvertTo-Json -Compress | dotnet user-secrets set --project $projectPath
if ($LASTEXITCODE -ne 0) { throw 'Could not save development admin configuration.' }
Write-Output 'Development admin configuration is stored in User Secrets. No password was printed.'
