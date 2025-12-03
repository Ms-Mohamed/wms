# Script de démarrage du Backend Python (Service Analytique)
# Port: 8000

Write-Host "Démarrage du Backend Python (Service Analytique)..." -ForegroundColor Cyan
Write-Host "Port: 8000" -ForegroundColor Yellow
Write-Host ""

cd backend-python
python -m uvicorn main:app --reload --port 8000

