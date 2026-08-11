# Streams the trx and tallies failed tests by (test class, normalized first line of error).
param([string]$Path = 'D:\calcite-efcore\TestResults\functional\functional2.trx')

$reader = [System.Xml.XmlReader]::Create($Path)
$clusters = @{}
$classCounts = @{}
try {
    while ($reader.Read()) {
        if ($reader.NodeType -eq 'Element' -and $reader.Name -eq 'UnitTestResult' -and $reader.GetAttribute('outcome') -eq 'Failed') {
            $testName = $reader.GetAttribute('testName')
            $sub = $reader.ReadSubtree()
            $msg = ''
            while ($sub.Read()) {
                if ($sub.NodeType -eq 'Element' -and $sub.Name -eq 'Message') {
                    $msg = $sub.ReadElementContentAsString()
                    break
                }
            }
            # class = testName up to last '.segment(' boundary
            $cls = $testName
            $paren = $cls.IndexOf('(')
            if ($paren -gt 0) { $cls = $cls.Substring(0, $paren) }
            $lastDot = $cls.LastIndexOf('.')
            if ($lastDot -gt 0) { $cls = $cls.Substring(0, $lastDot) }
            $classCounts[$cls] = 1 + ($classCounts[$cls] ?? 0)

            # fingerprint: first line; for the opaque CalciteException wrapper, use the deepest
            # inner-exception line (the '---- ' prefixed lines) instead.
            $lines = $msg -split "`r?`n"
            $line = $lines[0]
            if ($line -like '*Failed to execute Calcite statement*') {
                $inner = $lines | Where-Object { $_ -match '^\s*-+ ' } | Select-Object -Last 1
                if ($inner) { $line = $inner.Trim() -replace '^-+ ', '' }
            }
            if ($line -match 'SqlParseException : Encountered "(?<tok>[^"]+)"') {
                # keep the offending token; note what came after it too when present
                $line = 'SqlParseException : Encountered <' + $Matches['tok'] + '>'
            }
            else {
                $line = $line -replace "'[^']*'", "'*'" -replace '"[^"]*"', '"*"' -replace '\d+', 'N'
            }
            if ($line.Length -gt 170) { $line = $line.Substring(0, 170) }
            $clusters[$line] = 1 + ($clusters[$line] ?? 0)
        }
    }
}
finally { $reader.Close() }

"=== TOP ERROR FINGERPRINTS ==="
$clusters.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 30 | ForEach-Object { "{0,6}  {1}" -f $_.Value, $_.Key }
""
"=== TOP FAILING TEST CLASSES ==="
$classCounts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 30 | ForEach-Object { "{0,6}  {1}" -f $_.Value, ($_.Key -replace 'Apache\.Calcite\.EntityFrameworkCore\.FunctionalTests\.', '') }
