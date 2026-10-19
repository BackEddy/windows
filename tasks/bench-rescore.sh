#!/bin/bash
# Reavalia cópias já geradas pelo bench.sh com outro gabarito, sem chamar o modelo (não gasta quota).
# Uso:  REF=tasks/bench/ReferenceTests-B2.cs tasks/bench-rescore.sh ~/oc-bench/opencode-go-xxx-165449 [outra pasta ...]
set -u
cd "$(dirname "$0")/.." || exit 1
REF="${REF:-tasks/bench/ReferenceTests-B2.cs}"
[ -f "$REF" ] || { echo "não achei $REF"; exit 2; }
[ $# -gt 0 ] || { echo "uso: tasks/bench-rescore.sh pasta [pasta ...]"; exit 2; }
REFABS="$(cd "$(dirname "$REF")" && pwd)/$(basename "$REF")"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
printf '%-52s %-14s %s\n' pasta testes_proprios gabarito
for W in "$@"; do
  W="${W%/}"
  [ -d "$W/tests/Backeddy.Core.Tests" ] || { echo "$W: não parece uma cópia do bench"; continue; }
  rm -rf "$W/TestResults"
  cp "$REFABS" "$W/tests/Backeddy.Core.Tests/BenchReferenceTests.cs"
  ( cd "$W" && DOTNET_CLI_UI_LANGUAGE=en dotnet test tests/Backeddy.Core.Tests --logger "trx;LogFileName=bench.trx" --results-directory "$W/TestResults" > "$W/dotnet-test.log" 2>&1 )
  TRX=$(ls "$W"/TestResults/*.trx 2>/dev/null | head -1)
  if [ -z "$TRX" ]; then OWN="NAO COMPILA"; REFS="-"; else
    RES=$(grep -o '<UnitTestResult [^>]*>' "$TRX")
    OWN="$(echo "$RES" | grep -v 'BenchReferenceTests' | grep -c 'outcome="Passed"')ok/$(echo "$RES" | grep -v 'BenchReferenceTests' | grep -vc 'outcome="Passed"')falha"
    REFS="$(echo "$RES" | grep 'BenchReferenceTests' | grep -c 'outcome="Passed"')/$(echo "$RES" | grep -c 'BenchReferenceTests')"
  fi
  rm -f "$W/tests/Backeddy.Core.Tests/BenchReferenceTests.cs"
  printf '%-52s %-14s %s\n' "$(basename "$W")" "$OWN" "$REFS"
done
