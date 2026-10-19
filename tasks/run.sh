#!/bin/bash
# Roda uma tarefa no opencode e mostra o resumo para o Claude revisar.
# Uso:  tasks/run.sh 001                      executa a spec tasks/001-*.md
#       tasks/run.sh 001 -m opencode-go/kimi-k2.7-code
#       tasks/run.sh 001 --ci log-do-ci.txt   manda o log de um CI que falhou para o opencode corrigir
set -u
cd "$(dirname "$0")/.." || exit 1

N="${1:-}"
if [ -z "$N" ]; then
  echo "uso: tasks/run.sh 001 [-m modelo] [--ci arquivo-do-log]"
  exit 2
fi
shift
MODEL="opencode-go/deepseek-v4.1-flash"
CI_LOG=""
while [ $# -gt 0 ]; do
  case "$1" in
    -m) MODEL="${2:-}"; shift 2 ;;
    --ci) CI_LOG="${2:-}"; shift 2 ;;
    *) echo "opção desconhecida: $1"; exit 2 ;;
  esac
done

# tudo que aparece na tela também vai para tasks/NNN.log.txt (o Claude lê esse arquivo)
exec > >(tee -a "tasks/$N.log.txt") 2>&1

SPEC=$(ls tasks/"$N"-*.md 2>/dev/null | head -1)
if [ -z "$SPEC" ]; then echo "não achei tasks/$N-*.md"; exit 2; fi
if ! command -v opencode >/dev/null 2>&1; then echo "opencode não está no PATH"; exit 2; fi
if [ -n "$CI_LOG" ] && [ ! -f "$CI_LOG" ]; then echo "não achei o log: $CI_LOG"; exit 2; fi

# o diff só diz alguma coisa se o ponto de partida estiver limpo
if ! git diff --quiet HEAD; then
  echo "Há mudanças não commitadas em arquivos já versionados. Faça commit (sem push) e rode de novo."
  git status --short
  exit 3
fi

REPORT="tasks/$N.relatorio.md"
COUNTER="tasks/.$N.ci-tentativas"

refs_hash() { { git rev-parse HEAD; git for-each-ref --format='%(refname) %(objectname)'; } | shasum | cut -d' ' -f1; }
guard_hash() {
  { git ls-files -- .github installer VERSION Directory.Build.props README.md README.pt-BR.md README.es.md
    ls contexto.md AGENTS.md opencode.json tasks/_modelo.md tasks/[0-9]*-*.md 2>/dev/null
  } | sort -u | while read -r f; do [ -f "$f" ] && shasum "$f"; done | shasum | cut -d' ' -f1
}

REFS_BEFORE=$(refs_hash)
GUARD_BEFORE=$(guard_hash)

if [ -n "$CI_LOG" ]; then
  K=$(( $(cat "$COUNTER" 2>/dev/null || echo 0) + 1 ))
  echo "$K" > "$COUNTER"
  PROMPT="Siga as regras do AGENTS.md. O CI do GitHub falhou depois da tarefa descrita em $SPEC; o log está anexado. Corrija SOMENTE o que o log mostra, sem mudar o resto. Depois acrescente a $REPORT a seção '## Correção do CI (tentativa $K)' com a causa e o que mudou. Não faça commit nem push."
  echo ">> Correção do CI, tentativa $K, modelo $MODEL"
  START=$SECONDS
  opencode run --auto -m "$MODEL" -f "$CI_LOG" "$PROMPT"
else
  rm -f "$COUNTER"
  PROMPT="Siga as regras do AGENTS.md e execute a tarefa descrita em $SPEC. Ao terminar, escreva o relatório em $REPORT no formato do AGENTS.md. Não faça commit nem push."
  echo ">> Tarefa $N, modelo $MODEL"
  START=$SECONDS
  opencode run --auto -m "$MODEL" "$PROMPT"
fi
CODE=$?
ELAPSED=$(( SECONDS - START ))

echo
echo "================ RESUMO (${ELAPSED}s, opencode saiu com $CODE) ================"
PROBLEM=0
if [ "$(refs_hash)" != "$REFS_BEFORE" ]; then
  echo "!! ALERTA: houve commit, tag ou mudança de branch durante a tarefa. Não publique; chame o Claude."
  PROBLEM=1
fi
if [ "$(guard_hash)" != "$GUARD_BEFORE" ]; then
  echo "!! ALERTA: o opencode mexeu em arquivo protegido (.github, installer, VERSION, READMEs, contexto.md, AGENTS.md, opencode.json ou specs)."
  PROBLEM=1
fi
echo
echo "---- relatório ($REPORT) ----"
if [ -f "$REPORT" ]; then cat "$REPORT"; else echo "(o opencode não escreveu o relatório)"; PROBLEM=1; fi
echo
echo "---- git status ----"
git status --short
echo
echo "---- git diff --stat ----"
git diff --stat
git ls-files --others --exclude-standard | grep -v '^tasks/' | while read -r f; do echo " novo: $f ($(wc -l < "$f" | tr -d ' ') linhas)"; done
echo
if [ -n "$CI_LOG" ]; then
  echo "Tentativa $K de 2. Se o CI falhar de novo depois desta, chame o Claude."
fi
if [ "$PROBLEM" -ne 0 ]; then exit 4; fi
exit "$CODE"
