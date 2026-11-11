#!/usr/bin/env bash
# Regenerate docs/badges/coverage.svg from the NUnit suite's line coverage.
# Uses the same Coverlet collector and ReportGenerator version as CI, which fails
# the run if the committed badge no longer matches what the tests measure.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RG_VERSION="5.3.11"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

dotnet tool install dotnet-reportgenerator-globaltool --version "$RG_VERSION" --tool-path "$WORK/tools" >/dev/null

dotnet test "$ROOT/src/NUnit_Tests" --configuration Release --nologo \
  --collect:"XPlat Code Coverage" --results-directory "$WORK/results"

"$WORK/tools/reportgenerator" \
  "-reports:$WORK/results/**/coverage.cobertura.xml" \
  "-targetdir:$WORK/report" \
  "-reporttypes:Badges;TextSummary"

mkdir -p "$ROOT/docs/badges"
cp "$WORK/report/badge_linecoverage.svg" "$ROOT/docs/badges/coverage.svg"
grep -E 'Line coverage|Covered lines|Coverable lines' "$WORK/report/Summary.txt"
echo "Updated docs/badges/coverage.svg"
