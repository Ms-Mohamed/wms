# Script de correction rapide - Connexion PostgreSQL
Write-Host "=== Correction de la Connexion PostgreSQL ===" -ForegroundColor Cyan
Write-Host ""

$appsettingsPath = "WMS.API\appsettings.json"

if (Test-Path $appsettingsPath) {
    Write-Host "Fichier appsettings.json trouvé." -ForegroundColor Green
    Write-Host ""
    Write-Host "IMPORTANT : Vous devez modifier le mot de passe PostgreSQL dans:" -ForegroundColor Yellow
    Write-Host "  $appsettingsPath" -ForegroundColor White
    Write-Host ""
    Write-Host "Ligne à modifier:" -ForegroundColor Yellow
    Write-Host '  "DefaultConnection": "Host=localhost;Port=5432;Database=wms_db;Username=postgres;Password=VOTRE_MOT_DE_PASSE"' -ForegroundColor White
    Write-Host ""
    
    $continue = Read-Host "Avez-vous corrigé le mot de passe ? (O/N)"
    
    if ($continue -eq "O" -or $continue -eq "o") {
        Write-Host ""
        Write-Host "Application des migrations..." -ForegroundColor Cyan
        cd WMS.API
        dotnet ef database update --project ..\WMS.Data
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✅ Migrations appliquées avec succès !" -ForegroundColor Green
            Write-Host ""
            Write-Host "Vous pouvez maintenant redémarrer le Backend C#:" -ForegroundColor Yellow
            Write-Host "  cd WMS.API" -ForegroundColor White
            Write-Host "  dotnet run" -ForegroundColor White
        } else {
            Write-Host ""
            Write-Host "❌ Erreur lors de l'application des migrations." -ForegroundColor Red
            Write-Host "Vérifiez le mot de passe et que PostgreSQL est démarré." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Veuillez corriger le mot de passe dans appsettings.json puis relancez ce script." -ForegroundColor Yellow
    }
} else {
    Write-Host "❌ Fichier appsettings.json non trouvé." -ForegroundColor Red
}

