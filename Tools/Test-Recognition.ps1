$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sources = @(
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/CellImageProcessing.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/CellDetectionStabilizer.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/PerspectiveCellSampler.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/BoardCellRecognition.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/Runtime/PaperMoveInput.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/GameLogic/GameSession.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/GameLogic/MoveCandidate.cs'),
    (Join-Path $projectRoot 'Assets/Marubatsu/GameLogic/BoardSnapshot.cs'),
    (Join-Path $PSScriptRoot 'RecognitionChecks.cs'),
    (Join-Path $PSScriptRoot 'GameLogicChecks.cs')
)
Add-Type -Path $sources
[RecognitionChecks]::Run()
[GameLogicChecks]::Run()
