# Script pour démarrer tous les services WMS
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  WMS - Démarrage des Services" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Backend C# (Port 5000)
Write-Host "Démarrage du Backend C# (Port 5000)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$scriptPath\backend-csharp\WMS.API'; Write-Host '=== Backend C# (Core Transactionnel) ===' -ForegroundColor Green; Write-Host 'Port: 5000' -ForegroundColor Cyan; Write-Host 'Swagger: http://localhost:5000/swagger' -ForegroundColor Cyan; Write-Host ''; dotnet run"

Start-Sleep -Seconds 3

# Backend Python (Port 8000)
Write-Host "Démarrage du Backend Python (Port 8000)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$scriptPath\backend-python'; Write-Host '=== Backend Python (Service Analytique) ===' -ForegroundColor Green; Write-Host 'Port: 8000' -ForegroundColor Cyan; Write-Host 'Docs: http://localhost:8000/docs' -ForegroundColor Cyan; Write-Host ''; python -m uvicorn main:app --reload --port 8000"

Start-Sleep -Seconds 3

# Frontend React (Port 3000)
Write-Host "Démarrage du Frontend React (Port 3000)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$scriptPath\frontend'; Write-Host '=== Frontend React ===' -ForegroundColor Green; Write-Host 'Port: 3000' -ForegroundColor Cyan; Write-Host 'URL: http://localhost:3000' -ForegroundColor Cyan; Write-Host ''; npm run dev"

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Tous les services sont en cours de démarrage..." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Services démarrés dans des terminaux séparés." -ForegroundColor Yellow
Write-Host ""
Write-Host "URLs de vérification:" -ForegroundColor Cyan
Write-Host "  - Frontend:     http://localhost:3000" -ForegroundColor White
Write-Host "  - Backend C#:   http://localhost:5000/swagger" -ForegroundColor White
Write-Host "  - Backend Python: http://localhost:8000/docs" -ForegroundColor White
Write-Host ""
Write-Host "Appuyez sur une touche pour continuer..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

