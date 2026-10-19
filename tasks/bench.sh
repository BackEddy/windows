#!/bin/bash
# Compara modelos do opencode na mesma tarefa, em cópias limpas do repositório (o seu não é tocado).
# Uso:  tasks/bench.sh modelo1 [modelo2 ...]      (ex.: tasks/bench.sh opencode-go/glm-5.3 opencode-go/deepseek-v4-pro)
# Opções por variável:  BASE=<commit de partida, padrão fae3b2c>   SPEC=<spec, padrão tasks/bench/B1-atraso-core.md>
# Resultado: uma linha por modelo (tela e ~/oc-bench/resultados.txt). Cada cópia fica em ~/oc-bench/ (apague quando quiser).
set -u
cd "$(dirname "$0")/.." || exit 1
REPO=$(pwd)
BASE="${BASE:-fae3b2c}"
SPEC="${SPEC:-tasks/bench/B1-atraso-core.md}"
REF="tasks/bench/ReferenceTests.cs"
OUT="$HOME/oc-bench"
DB="$HOME/.local/share/opencode/opencode.db"
TIMEOUT=900

if [ $# -eq 0 ]; then echo "uso: tasks/bench.sh opencode-go/modelo [outro ...]"; exit 2; fi
for f in "$SPEC" "$REF" AGENTS.md opencode.json; do [ -f "$f" ] || { echo "não achei $f"; exit 2; }; done
git rev-parse --verify -q "$BASE^{commit}" >/dev/null || { echo "commit de partida inválido: $BASE"; exit 2; }
command -v opencode >/dev/null 2>&1 || { echo "opencode não está no PATH"; exit 2; }
command -v dotnet >/dev/null 2>&1 || { echo "dotnet não está no PATH"; exit 2; }
mkdir -p "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

HEADER=$(printf '%-34s %6s %8s %9s %8s %10s %-12s %-10s %s' modelo tempo custo tok_saida tok_cache testes_proprios gabarito arquivos observacao)
echo "$HEADER"
[ -f "$OUT/resultados.txt" ] || echo "$HEADER" > "$OUT/resultados.txt"

for M in "$@"; do
  SAFE=$(echo "$M" | tr '/:' '--')
  W="$OUT/$SAFE-$(date +%H%M%S)"
  rm -rf "$W"
  git clone -q --no-checkout "$REPO" "$W" && git -C "$W" checkout -q "$BASE" || { echo "falha ao clonar para $M"; continue; }
  git -C "$W" remote remove origin
  cp AGENTS.md opencode.json "$W/"
  mkdir -p "$W/tasks"
  cp "$SPEC" "$W/tasks/bench.md"
  git -C "$W" add -A
  git -C "$W" -c user.name=bench -c user.email=bench@local commit -qm "partida do benchmark"
  START=$(git -C "$W" rev-parse HEAD)

  T0=$(date +%s)
  PROMPT="Siga as regras do AGENTS.md e execute a tarefa descrita em tasks/bench.md. Ao terminar, escreva o relatório em tasks/bench.relatorio.md no formato do AGENTS.md. Não faça commit nem push."
  ( cd "$W" && perl -e 'alarm shift; exec @ARGV' "$TIMEOUT" opencode run --auto -m "$M" "$PROMPT" ) 2>&1 | tee "$W/bench.log"
  CODE=${PIPESTATUS[0]}
  SECS=$(( $(date +%s) - T0 ))
  OBS=""
  [ "$CODE" -ne 0 ] && OBS="opencode saiu com $CODE; "

  # o gabarito entra DEPOIS, para o modelo não enxergar
  cp "$REF" "$W/tests/Backeddy.Core.Tests/BenchReferenceTests.cs"
  ( cd "$W" && DOTNET_CLI_UI_LANGUAGE=en dotnet test tests/Backeddy.Core.Tests --logger "trx;LogFileName=bench.trx" --results-directory "$W/TestResults" > "$W/dotnet-test.log" 2>&1 )
  TRX=$(ls "$W"/TestResults/*.trx 2>/dev/null | head -1)
  if [ -z "$TRX" ]; then
    OWN="NAO COMPILA"; REFS="-"
  else
    RES=$(grep -o '<UnitTestResult [^>]*>' "$TRX")
    OWN_OK=$(echo "$RES" | grep -v 'BenchReferenceTests' | grep -c 'outcome="Passed"')
    OWN_BAD=$(echo "$RES" | grep -v 'BenchReferenceTests' | grep -vc 'outcome="Passed"')
    REF_ALL=$(echo "$RES" | grep -c 'BenchReferenceTests')
    REF_OK=$(echo "$RES" | grep 'BenchReferenceTests' | grep -c 'outcome="Passed"')
    OWN="${OWN_OK}ok/${OWN_BAD}falha"; REFS="${REF_OK}/${REF_ALL}"
  fi
  rm -f "$W/tests/Backeddy.Core.Tests/BenchReferenceTests.cs"

  # custo e tokens, lidos do banco do opencode (cost = preço de tabela calculado pelo opencode)
  COST="n/d"; TOUT="n/d"; TCACHE="n/d"
  if [ -f "$DB" ] && command -v sqlite3 >/dev/null 2>&1; then
    ROW=$(sqlite3 -readonly "$DB" "select printf('%.3f',coalesce(sum(cost),0)),coalesce(sum(tokens_output),0),coalesce(sum(tokens_cache_read),0) from session_v2 where directory='$W';" 2>/dev/null)
    if [ -n "$ROW" ]; then COST="\$$(echo "$ROW" | cut -d'|' -f1)"; TOUT=$(echo "$ROW" | cut -d'|' -f2); TCACHE=$(echo "$ROW" | cut -d'|' -f3); fi
  fi

  # o que mudou, e se mexeu onde não devia
  NFILES=$(git -C "$W" status --porcelain | grep -v 'TestResults\|/bin/\|/obj/\|bench.log\|dotnet-test.log\|bench.relatorio.md' | wc -l | tr -d ' ')
  [ "$(git -C "$W" rev-parse HEAD)" != "$START" ] && OBS="${OBS}FEZ COMMIT; "
  if git -C "$W" status --porcelain | grep -q -E ' (AGENTS.md|opencode.json|tasks/bench.md)$'; then OBS="${OBS}MEXEU EM PROTEGIDO; "; fi
  if git -C "$W" status --porcelain | grep -q -E 'src/Backeddy\.(App|Service)'; then OBS="${OBS}FORA DO ESCOPO; "; fi
  [ -f "$W/tasks/bench.relatorio.md" ] || OBS="${OBS}sem relatorio; "

  LINE=$(printf '%-34s %5ss %8s %9s %8s %10s %-12s %-10s %s' "$M" "$SECS" "$COST" "$TOUT" "$TCACHE" "$OWN" "$REFS" "$NFILES" "$OBS")
  echo "$LINE"
  echo "$LINE" >> "$OUT/resultados.txt"
done
echo
echo "Resultados também em $OUT/resultados.txt. Cada cópia (código, log, relatório) fica em $OUT/."
