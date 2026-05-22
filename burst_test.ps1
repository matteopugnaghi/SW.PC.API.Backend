$out = ".\burst_results.txt"
"=== Step A: 320 parallel curl GETs to / ===" | Out-File $out -Encoding ASCII
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$jobs = 1..320 | ForEach-Object {
    Start-Job -ScriptBlock {
        & curl.exe -k -s -o NUL -w "%{http_code}" https://localhost:5001/
    }
}
$jobs | Wait-Job -Timeout 60 | Out-Null
$results = $jobs | ForEach-Object { Receive-Job -Job $_ }
$jobs | Remove-Job -Force
$sw.Stop()
("Elapsed: {0} ms" -f $sw.ElapsedMilliseconds) | Out-File $out -Append -Encoding ASCII
$results | Group-Object | Sort-Object Name | ForEach-Object { ("  HTTP {0}: {1}" -f $_.Name, $_.Count) | Out-File $out -Append -Encoding ASCII }

if (-not ($results | Where-Object { $_ -eq "429" })) {
    "No 429s in first batch, firing two batches of 200 back-to-back..." | Out-File $out -Append -Encoding ASCII
    $sw2 = [System.Diagnostics.Stopwatch]::StartNew()
    $b1 = 1..200 | ForEach-Object { Start-Job -ScriptBlock { & curl.exe -k -s -o NUL -w "%{http_code}" https://localhost:5001/ } }
    Start-Sleep -Milliseconds 100
    $b2 = 1..200 | ForEach-Object { Start-Job -ScriptBlock { & curl.exe -k -s -o NUL -w "%{http_code}" https://localhost:5001/ } }
    ($b1 + $b2) | Wait-Job -Timeout 120 | Out-Null
    $r = ($b1 + $b2) | ForEach-Object { Receive-Job -Job $_ }
    ($b1 + $b2) | Remove-Job -Force
    $sw2.Stop()
    ("Retry elapsed: {0} ms" -f $sw2.ElapsedMilliseconds) | Out-File $out -Append -Encoding ASCII
    $r | Group-Object | Sort-Object Name | ForEach-Object { ("  HTTP {0}: {1}" -f $_.Name, $_.Count) | Out-File $out -Append -Encoding ASCII }
}

"" | Out-File $out -Append -Encoding ASCII
"Waiting 65s for sliding window reset..." | Out-File $out -Append -Encoding ASCII
Start-Sleep -Seconds 65

"=== Step B: 80 parallel POSTs to /hubs/scada/negotiate ===" | Out-File $out -Append -Encoding ASCII
$jobs2 = 1..80 | ForEach-Object {
    Start-Job -ScriptBlock {
        & curl.exe -k -s -o NUL -w "%{http_code}" -X POST "https://localhost:5001/hubs/scada/negotiate?negotiateVersion=1"
    }
}
$jobs2 | Wait-Job -Timeout 60 | Out-Null
$results2 = $jobs2 | ForEach-Object { Receive-Job -Job $_ }
$jobs2 | Remove-Job -Force
$results2 | Group-Object | Sort-Object Name | ForEach-Object { ("  HTTP {0}: {1}" -f $_.Name, $_.Count) | Out-File $out -Append -Encoding ASCII }
"DONE" | Out-File $out -Append -Encoding ASCII
