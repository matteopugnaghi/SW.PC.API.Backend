$ErrorActionPreference='Stop'
$projects=@('A70.AMITWP','A72.TOUTWP','test-proyecto','_template')
$base='c:\Users\mpugnaghi\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_\Projects'

# Unicode escapes to avoid any source-encoding issues
# (PowerShell variable names are case-insensitive, so use distinct lowercase names)
$eacu = [char]0x00E9   # e-acute
$agra = [char]0x00E0   # a-grave
$iacu = [char]0x00ED   # i-acute
$oacu = [char]0x00F3   # o-acute
$ecap = [char]0x00C9   # E-acute (capital)

$keys = [ordered]@{
  'orders.export.col.lineNumber'  = @{ SPA = "L${iacu}nea";              ENG = 'Line';        FRA = 'Ligne';                       ITA = 'Riga' }
  'orders.export.col.sku'         = @{ SPA = 'SKU';                       ENG = 'SKU';         FRA = 'SKU';                         ITA = 'SKU' }
  'orders.export.col.description' = @{ SPA = "Descripci${oacu}n";        ENG = 'Description'; FRA = 'Description';                 ITA = 'Descrizione' }
  'orders.export.col.quantity'    = @{ SPA = 'Cantidad';                  ENG = 'Quantity';    FRA = "Quantit${eacu}";              ITA = "Quantit${agra}" }
  'orders.export.col.unit'        = @{ SPA = 'Unidad';                    ENG = 'Unit';        FRA = "Unit${eacu}";                 ITA = "Unit${agra}" }
  'orders.export.col.elementName' = @{ SPA = 'Elemento';                  ENG = 'Element';     FRA = "${ecap}l${eacu}ment";         ITA = 'Elemento' }
  'orders.export.dataset.label'   = @{ SPA = 'Pedido de consumibles';
                                       ENG = 'Consumables order';
                                       FRA = 'Commande de consommables';
                                       ITA = 'Ordine consumabili' }
  'orders.export.dataset.desc'    = @{ SPA = "Una fila por l${iacu}nea con SKU, descripci${oacu}n, cantidad, unidad y elemento.";
                                       ENG = 'One row per line with SKU, description, quantity, unit and element.';
                                       FRA = "Une ligne par article avec SKU, description, quantit${eacu}, unit${eacu} et ${eacu}l${eacu}ment.";
                                       ITA = "Una riga per articolo con SKU, descrizione, quantit${agra}, unit${agra} ed elemento." }
}

foreach($p in $projects){
  $f = Join-Path $base "$p\translations\translations.json"
  if(-not(Test-Path $f)){ Write-Host "SKIP $p"; continue }
  $raw = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
  $j = $raw | ConvertFrom-Json
  if(-not $j.translations){ Write-Host "NO translations in $p"; continue }
  foreach($k in $keys.Keys){
    $j.translations | Add-Member -NotePropertyName $k -NotePropertyValue ([pscustomobject]$keys[$k]) -Force
  }
  $out = $j | ConvertTo-Json -Depth 100
  [System.IO.File]::WriteAllText($f, $out, (New-Object System.Text.UTF8Encoding($false)))
  Write-Host "OK $p"
}
try {
  Invoke-RestMethod -Uri http://localhost:5000/api/translations/clear-cache -Method Post -TimeoutSec 5 | Out-Null
  Write-Host 'Cache cleared'
} catch { Write-Host "Cache clear FAIL: $($_.Exception.Message)" }
