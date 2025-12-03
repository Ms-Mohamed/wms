# Script pour appliquer les migrations avec mot de passe personnalisé
param(
    [Parameter(Mandatory=$true)]
    [string]$Password
)

Write-Host "=== Application des Migrations PostgreSQL ===" -ForegroundColor Cyan
Write-Host ""

$connectionString = "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=$Password"

Write-Host "Application de la migration avec la chaîne de connexion..." -ForegroundColor Yellow
Write-Host ""

$env:ConnectionStrings__DefaultConnection = $connectionString

cd WMS.API
dotnet ef database update --project ..\WMS.Data --connection $connectionString

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✅ Migrations appliquées avec succès !" -ForegroundColor Green
    Write-Host ""
    Write-Host "Les tables ont été créées dans PostgreSQL." -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "❌ Erreur lors de l'application des migrations." -ForegroundColor Red
    Write-Host "Vérifiez:" -ForegroundColor Yellow
    Write-Host "  - Le mot de passe PostgreSQL est correct" -ForegroundColor White
    Write-Host "  - PostgreSQL est démarré" -ForegroundColor White
    Write-Host "  - La base de données wms_db existe" -ForegroundColor White
}

