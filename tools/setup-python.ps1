# setup-python.ps1
# Installs an embeddable Python with optiland into python-embed\ at the repository's root
# (gitignored), for the cross-check against Optiland (docs/verification.md). It runs
# AberrationCalculator's own script, carried in external/, with this repository's target.
#
#   .\tools\setup-python.ps1
#
# ABCALC_PYTHON_HOME points the tests at another Python that has optiland installed. Without
# either, the Optiland tests are reported as skipped.

param(
    [string]$PythonVersion = "3.12.8",
    [string]$TargetDir = (Join-Path $PSScriptRoot "..\python-embed")
)

$ErrorActionPreference = "Stop"
$script = Join-Path $PSScriptRoot "..\external\AberrationCalculator\tools\setup-python.ps1"
& $script -PythonVersion $PythonVersion -TargetDir ([System.IO.Path]::GetFullPath($TargetDir))
