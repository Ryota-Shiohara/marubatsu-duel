$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sources = @(
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/CellImageProcessing.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/CellDetectionStabilizer.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/PerspectiveCellSampler.cs'),
    (Join-Path $PSScriptRoot 'RecognitionChecks.cs')
)
Add-Type -Path $sources
[RecognitionChecks]::Run()
