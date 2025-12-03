# Script de diagnostic pour le Backend Python
Write-Host "=== Diagnostic Backend Python ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Vérifier que le Backend Python répond
Write-Host "1. Test de disponibilité du Backend Python..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/docs" -Method GET -TimeoutSec 5 -ErrorAction Stop
    Write-Host "   ✅ Backend Python répond (Code: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Backend Python ne répond pas: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Test de l'endpoint predict avec gestion d'erreur améliorée
Write-Host ""
Write-Host "2. Test de /api/analytics/predict/5..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/api/analytics/predict/5" -Method GET -TimeoutSec 10 -ErrorAction Stop
    Write-Host "   Code HTTP: $($response.StatusCode)" -ForegroundColor White
    if ($response.StatusCode -eq 200) {
        $json = $response.Content | ConvertFrom-Json
        Write-Host "   ✅ SUCCÈS !" -ForegroundColor Green
        Write-Host "   Produit: $($json.product_name)" -ForegroundColor White
        if ($json.message) {
            Write-Host "   Message: $($json.message)" -ForegroundColor Yellow
        } else {
            Write-Host "   Prévisions: $($json.forecasts.Count) mois" -ForegroundColor White
        }
    }
} catch {
    Write-Host "   ❌ Erreur HTTP" -ForegroundColor Red
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $responseBody = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
        
        Write-Host "   Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
        Write-Host "   Réponse complète:" -ForegroundColor Yellow
        Write-Host $responseBody -ForegroundColor Red
        
        # Essayer de parser le JSON
        try {
            $errorJson = $responseBody | ConvertFrom-Json
            if ($errorJson.detail) {
                Write-Host ""
                Write-Host "   💡 Détail de l'erreur:" -ForegroundColor Yellow
                Write-Host "   $($errorJson.detail)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "   (Réponse non-JSON)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   Erreur: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 3: Test de l'endpoint optimize
Write-Host ""
Write-Host "3. Test de /api/analytics/optimize/5..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8000/api/analytics/optimize/5" -Method GET -TimeoutSec 10 -ErrorAction Stop
    Write-Host "   Code HTTP: $($response.StatusCode)" -ForegroundColor White
    if ($response.StatusCode -eq 200) {
        $json = $response.Content | ConvertFrom-Json
        Write-Host "   ✅ SUCCÈS !" -ForegroundColor Green
        Write-Host "   Produit: $($json.product_name)" -ForegroundColor White
        Write-Host "   Stock actuel: $($json.current_stock)" -ForegroundColor White
        Write-Host "   Point de commande: $($json.reorder_point)" -ForegroundColor White
        Write-Host "   QEC: $($json.eoq)" -ForegroundColor White
    }
} catch {
    Write-Host "   ❌ Erreur HTTP" -ForegroundColor Red
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $responseBody = $reader.ReadToEnd()
        $reader.Close()
        $stream.Close()
        
        Write-Host "   Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
        Write-Host "   Réponse complète:" -ForegroundColor Yellow
        Write-Host $responseBody -ForegroundColor Red
        
        # Essayer de parser le JSON
        try {
            $errorJson = $responseBody | ConvertFrom-Json
            if ($errorJson.detail) {
                Write-Host ""
                Write-Host "   💡 Détail de l'erreur:" -ForegroundColor Yellow
                Write-Host "   $($errorJson.detail)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "   (Réponse non-JSON)" -ForegroundColor Gray
        }
    } else {
        Write-Host "   Erreur: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Fin du Diagnostic ===" -ForegroundColor Cyan

