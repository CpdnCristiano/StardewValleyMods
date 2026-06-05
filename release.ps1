# release.ps1 - Build local e cria GitHub Release automaticamente
# Uso: .\release.ps1
# Requer: git configurado com acesso ao repo

$ErrorActionPreference = "Stop"

# ── Configurações ──────────────────────────────────────────────────────────────
$REPO       = "CpdnCristiano/StardewValleyMods"
$PROJECT    = "StardewArchipelagoTranslations\StardewArchipelagoTranslations.csproj"
$MANIFEST   = "StardewArchipelagoTranslations\manifest.json"
$DIST       = "dist"

# ── 1. Ler versão do manifest.json ────────────────────────────────────────────
Write-Host "`n🔍 Lendo versão do manifest.json..." -ForegroundColor Cyan
$manifest = Get-Content $MANIFEST | ConvertFrom-Json
$VERSION  = $manifest.Version
$TAG      = "v$VERSION"
$ZIP_NAME = "StardewArchipelagoTranslations-$TAG.zip"

Write-Host "   Versão: $VERSION" -ForegroundColor Green
Write-Host "   Tag:    $TAG"
Write-Host "   Arquivo: $ZIP_NAME"

# ── 2. Build ──────────────────────────────────────────────────────────────────
Write-Host "`n🔨 Buildando o mod..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $DIST | Out-Null
Remove-Item "$DIST\*.zip" -ErrorAction SilentlyContinue

dotnet build $PROJECT `
    -c Release `
    /p:EnableModDeploy=false `
    /p:EnableModZip=true `
    /p:ModZipPath="$PWD\$DIST"

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ Build falhou!" -ForegroundColor Red
    exit 1
}

# Renomear o zip gerado
$generated = Get-ChildItem "$DIST\*.zip" | Select-Object -First 1
if (-not $generated) {
    Write-Host "`n❌ Nenhum .zip encontrado em dist\!" -ForegroundColor Red
    exit 1
}
Rename-Item $generated.FullName "$DIST\$ZIP_NAME" -Force
Write-Host "`n✅ Build concluído: $DIST\$ZIP_NAME" -ForegroundColor Green

# ── 3. Pedir token do GitHub ──────────────────────────────────────────────────
Write-Host "`n🔑 Token do GitHub necessário para criar a release." -ForegroundColor Yellow
Write-Host "   Gere um em: https://github.com/settings/tokens (escopo: repo)"
$TOKEN = Read-Host "   Cole o token aqui"

$headers = @{
    Authorization = "Bearer $TOKEN"
    Accept        = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
}

# ── 4. Criar a tag e a release no GitHub ──────────────────────────────────────
Write-Host "`n🚀 Criando release $TAG no GitHub..." -ForegroundColor Cyan

$body = @{
    tag_name         = $TAG
    target_commitish = "master"
    name             = "Stardew Archipelago Translations $TAG"
    body             = @"
## 🎮 Stardew Archipelago Translations $TAG

Mod de tradução standalone para o StardewArchipelago.

### 📦 Instalação
1. Baixe o arquivo `.zip` abaixo
2. Extraia dentro da pasta `Mods` do Stardew Valley
3. Certifique-se de ter o StardewArchipelago instalado

### 🌐 Idiomas suportados
- 🇧🇷 Português (pt)
"@
    draft           = $false
    prerelease      = $false
} | ConvertTo-Json

$release = Invoke-RestMethod `
    -Uri "https://api.github.com/repos/$REPO/releases" `
    -Method POST `
    -Headers $headers `
    -Body $body `
    -ContentType "application/json"

Write-Host "   Release criada: $($release.html_url)" -ForegroundColor Green

# ── 5. Upload do .zip ─────────────────────────────────────────────────────────
Write-Host "`n📤 Fazendo upload de $ZIP_NAME..." -ForegroundColor Cyan

$uploadUrl = $release.upload_url -replace '\{.*\}', ''
$zipPath   = "$PWD\$DIST\$ZIP_NAME"
$zipBytes  = [System.IO.File]::ReadAllBytes($zipPath)

Invoke-RestMethod `
    -Uri "${uploadUrl}?name=$ZIP_NAME" `
    -Method POST `
    -Headers $headers `
    -Body $zipBytes `
    -ContentType "application/zip" | Out-Null

Write-Host "`n✅ Release publicada com sucesso!" -ForegroundColor Green
Write-Host "   🔗 $($release.html_url)" -ForegroundColor Cyan
