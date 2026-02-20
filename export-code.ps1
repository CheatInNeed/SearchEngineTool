$root = "."
$outputFile = "project_dump.txt"

if (Test-Path $outputFile) {
    Remove-Item $outputFile
}

$files = Get-ChildItem -Path $root -Recurse -Include *.cs,*.axaml,*.xaml,*.txt,*.md -File |
Where-Object { $_.FullName -notmatch "bin|obj|.git" }

foreach ($file in $files) {

    Add-Content $outputFile "=================================================="
    Add-Content $outputFile "FILE: $($file.FullName)"
    Add-Content $outputFile "=================================================="
    Add-Content $outputFile ""

    Get-Content $file.FullName | Add-Content $outputFile

    Add-Content $outputFile ""
    Add-Content $outputFile ""
}

Write-Host "Done! Output written to $outputFile"
