# Ninjago Dragon Rising - Unity CLI helpers (zsh/bash). Companion to Docs/BossBattle/research/cli-playtest.md.
#   source /Users/oscar/gitfolder/ninjago/ninjago-dragon-rising/Tools/unity-cli-helpers.sh
# Every helper targets $UPP explicitly (several Editors may be running). Requires: unity, jq, python3.
# Exit codes: 0 ok | 1 transport/hard failure | 2 bad args | 3 no test matched | 7 soft failure (result.success == false)
#             8 tests ran and some failed | 124 timed out.
# Derived from Docs/BilliardRogue/research/unity-cli-helpers.sh (same helpers, paths fixed for this repo, Ninjago extras).

if [ -n "${BASH_VERSION:-}" ]; then
  _UH_SELF="${BASH_SOURCE[0]}"
elif [ -n "${ZSH_VERSION:-}" ]; then
  eval '_UH_SELF="${(%):-%x}"'
fi
if [ -n "${_UH_SELF:-}" ] && [ -f "$_UH_SELF" ]; then
  UH_REPO="$(cd "$(dirname "$_UH_SELF")/.." && pwd)"
  UH_HELPERS="$UH_REPO/Tools/unity-cli-helpers.sh"
else
  UH_REPO="/Users/oscar/gitfolder/ninjago/ninjago-dragon-rising"
  UH_HELPERS="$UH_REPO/Tools/unity-cli-helpers.sh"
fi
: "${UPP:=$UH_REPO/ninjago-unity}"
UH_PLAYTEST="$UH_REPO/Tools/cli/NinjagoPlaytest.cs"
export UPP UH_REPO UNITY_NO_PAGER=1 UNITY_NO_BANNER=1

# ucmd <command> [args...] -> prints .data.result (pretty JSON) on success.
ucmd() {
  local out rc soft
  out=$(unity command "$@" --project-path "$UPP" --json 2>/dev/null); rc=$?
  if [ "$(printf '%s' "$out" | jq -r '.success' 2>/dev/null)" != "true" ]; then
    printf '%s\n' "$out" | jq -c '.errors' >&2 2>/dev/null || printf '%s\n' "$out" >&2
    [ "$rc" -eq 0 ] && rc=1
    return "$rc"
  fi
  printf '%s\n' "$out" | jq '.data.result'
  soft=$(printf '%s' "$out" | jq -r '.data.result | if type=="object" then ((if has("success") then .success else .Success end) | tostring) else "null" end')
  [ "$soft" = "false" ] && return 7
  return 0
}

# ueval '<C# statements; must `return` a value>'   |   ueval - <<'EOF' ... EOF
# Optional 2nd arg: eval timeout in ms (the CLI's --timeout is seconds and is added automatically).
ueval() {
  local code ms out rc
  if [ "$1" = "-" ]; then code=$(cat); else code="$1"; fi
  ms="${2:-5000}"
  out=$(ucmd eval "$code" "$ms" --timeout $(( ms / 1000 + 10 ))); rc=$?
  [ -n "$out" ] && printf '%s\n' "$out" | jq '.result'
  return "$rc"
}

# urun <file.cs> [Entry.Point] [json-args-array] [timeout_s]  -> run_script (ephemeral, no domain reload)
urun() {
  local file="$1" entry="$2" args="$3" t="${4:-120}" out rc
  set -- run_script --file "$file" --timeout_ms $(( t * 1000 )) --timeout $(( t + 10 ))
  [ -n "$entry" ] && set -- "$@" --entry "$entry"
  [ -n "$args" ] && set -- "$@" --args "$args"
  out=$(ucmd "$@"); rc=$?
  if [ "$rc" -eq 7 ]; then
    printf '%s\n' "$out" | jq '{error, errorDetails, diagnostics}' >&2
  else
    printf '%s\n' "$out" | jq '{result, compileMs, executeMs, warnings: [.diagnostics[]? | select(.severity!="error")]}'
  fi
  return "$rc"
}

# uwait_ready [timeout_s=120] -> waits for status==ready, !compiling, !domainReloadInProgress, playMode==stopped
# Tolerates the transient 401/COMMAND_FAILED errors a domain reload produces.
uwait_ready() {
  local t="${1:-120}" start r
  start=$(date +%s)
  while :; do
    r=$(unity command editor_status --project-path "$UPP" --json 2>/dev/null \
        | jq -r '.data.result | "\(.status) \(.compiling) \(.domainReloadInProgress) \(.playMode)"' 2>/dev/null)
    [ "$r" = "ready false false stopped" ] && return 0
    [ $(( $(date +%s) - start )) -ge "$t" ] && { echo "uwait_ready: timeout (last: $r)" >&2; return 124; }
    sleep 1
  done
}

# urecompile [timeout_s=180] -> AssetDatabase.Refresh + compile + domain reload; prints compiler errors, returns 1 on failure.
urecompile() {
  local t="${1:-180}" start r st
  start=$(date +%s)
  unity command recompile --project-path "$UPP" --json >/dev/null 2>&1
  while :; do
    r=$(unity command recompile_status --project-path "$UPP" --json 2>/dev/null | jq -c '.data.result' 2>/dev/null)
    st=$(printf '%s' "$r" | jq -r '.status' 2>/dev/null)
    case "$st" in completed|up_to_date) break ;; esac
    [ $(( $(date +%s) - start )) -ge "$t" ] && { echo "urecompile: timeout (last: $r)" >&2; return 124; }
    sleep 1
  done
  if [ "$(printf '%s' "$r" | jq -r '.failed or .compilationFailed')" = "true" ]; then
    printf '%s\n' "$r" | jq -r '.errors[]' >&2
    return 1
  fi
  uwait_ready "$t"
}

# uplay [timeout_s=60] -> enter play mode and wait until playing (fails fast on compile errors).
uplay() {
  local t="${1:-60}" start r
  if [ "$(unity command console_status --project-path "$UPP" --json 2>/dev/null | jq -r '.data.result.groundTruth.compilationFailed')" = "true" ]; then
    echo "uplay: scripts have compile errors; Unity will silently refuse play mode" >&2; return 1
  fi
  unity command editor_play --project-path "$UPP" --json >/dev/null 2>&1
  start=$(date +%s)
  while :; do
    r=$(unity command editor_status --project-path "$UPP" --json 2>/dev/null | jq -r '.data.result.playMode' 2>/dev/null)
    [ "$r" = "playing" ] && return 0
    [ $(( $(date +%s) - start )) -ge "$t" ] && { echo "uplay: timeout (playMode=$r)" >&2; return 124; }
    sleep 0.5
  done
}

# ustop [timeout_s=30] -> exit play mode and wait until the Editor is idle again.
ustop() { unity command editor_stop --project-path "$UPP" --json >/dev/null 2>&1; uwait_ready "${1:-30}"; }

# ushot <out.png> [width=1920] [height=1080] -> Game view incl. Screen Space-Overlay UI, any output path.
# Never use capture_game_view --save_path: it is resolved under Assets/ and leaves assets + .meta files behind.
ushot() {
  local out="$1" w="${2:-1920}" h="${3:-1080}" json
  json=$(unity command capture_game_view --width "$w" --height "$h" --project-path "$UPP" --json 2>/dev/null)
  if [ "$(printf '%s' "$json" | jq -r '.success')" != "true" ]; then printf '%s\n' "$json" | jq -c '.errors' >&2; return 1; fi
  printf '%s' "$json" | jq -r '.data.result.base64' | base64 -d > "$out" && echo "$out"
}

# ulog [tail=30] [level=log|warn|error] -> recent Console entries (seq, level, first 300 chars).
ulog() {
  unity command console --tail "${1:-30}" --level "${2:-log}" --project-path "$UPP" --json 2>/dev/null \
    | jq -r '.data.result.entries[] | "\(.seq) [\(.level)] \(.message | .[0:300])"'
}

# ucursor -> "<cursor> <session>" of the Console buffer; pass to ulog_since to read only what came after.
ucursor() {
  unity command console_status --project-path "$UPP" --json 2>/dev/null | jq -r '.data.result | "\(.cursor) \(.session)"'
}

# ulog_since <cursor> <session> [level=log] -> Console entries after a ucursor mark.
ulog_since() {
  unity command console --since "$1" --since_session "$2" --level "${3:-log}" --project-path "$UPP" --json 2>/dev/null \
    | jq -r '.data.result.entries[] | "\(.seq) [\(.level)] \(.message | .[0:300])"'
}

# utests <filter> [mode=editor] [timeout_s=300] [filter_type=testName|assembly|category] -> run tests synchronously.
# filter = case-insensitive substring of the test FullName (testName) or assembly name (assembly).
# Returns 8 when any test failed, 3 when the filter matched no test (run_tests itself exits 0 in both cases).
utests() {
  local out failed total t="${3:-300}"
  # All positional: run_tests' own `timeout` (s) collides with the CLI's --timeout, and positionals bind
  # in declaration order (mode, filter, filter_type, include_explicit, async_tests, timeout).
  out=$(unity command run_tests "${2:-editor}" "$1" "${4:-testName}" false false "$t" --timeout $(( t + 30 )) \
        --project-path "$UPP" --json 2>/dev/null)
  printf '%s\n' "$out" | jq '.data.result | {Summary, failures: [.Results[]? | select(.Status=="Failed") | {FullName, Message, StackTrace}]}'
  total=$(printf '%s' "$out" | jq -r '.data.result.Summary.Total // 0')
  failed=$(printf '%s' "$out" | jq -r '.data.result.Summary.Failed // 1')
  [ "$total" = "0" ] && { echo "utests: no test matched '$1'" >&2; return 3; }
  [ "$failed" = "0" ] || return 8
}

# ueditorlog -> path of the log file THIS Editor process is writing (Editor.log rotates when another Editor starts).
ueditorlog() {
  local pid
  pid=$(unity pipeline list --json 2>/dev/null | jq -r --arg p "$UPP" '.data.instances[] | select(.projectPath==$p) | .pid')
  [ -n "$pid" ] && lsof -a -p "$pid" -d 1 -Fn 2>/dev/null | sed -n 's/^n//p'
}

# ---------------------------------------------------------------------------------------------------------------
# Ninjago extras
# ---------------------------------------------------------------------------------------------------------------

# ucheck [compile_check args...] -> compile Assembly-CSharp(+Editor) outside the Editor (no reload, ~3-5 s).
ucheck() { python3 "$UH_REPO/Tools/compile_check.py" "$@"; }

# upreflight -> Editor reachable, autotick on, idle; prints the editor_status line.
upreflight() {
  unity pipeline list --json 2>/dev/null | jq -c --arg p "$UPP" '.data.instances[] | select(.projectPath==$p) | {pid, reachable: .pipelineServer.isReachable, safeMode}'
  ucmd set_autotick --enable true >/dev/null
  uwait_ready "${1:-600}" && ucmd editor_status | jq -c .
}

# ulock <owner-label> <helper|command> [args...] -> run it while holding the shared Editor lock (Tools/editor_lock.sh).
ulock() {
  local owner="$1"; shift
  "$UH_REPO/Tools/editor_lock.sh" "$owner" bash -c 'source "$0"; "$@"' "$UH_HELPERS" "$@"
}

# umainscene -> make MainScene the open scene (edit mode only; refuses when the open scene has unsaved changes).
umainscene() {
  local dirty
  dirty=$(ucmd list_open_scenes | jq -r '[.scenes[] | select(.isDirty)] | length')
  [ "$dirty" != "0" ] && { echo "umainscene: an open scene has unsaved changes; save or ask first" >&2; return 1; }
  ucmd open_scene --path Scenes/MainScene >/dev/null
}

# uplaymode <GameModeType> [players=1] [timeout_s=90] -> MainScene, play, wait for the menu, Simulated Body on, load GameScene.
uplaymode() {
  local mode="$1" players="${2:-1}" t="${3:-90}"
  [ -z "$mode" ] && { echo "usage: uplaymode <GameModeType> [players] [timeout_s]" >&2; return 2; }
  umainscene || return 1
  uplay "$t" || return $?
  urun "$UH_PLAYTEST" NinjagoPlaytest.PlayMode "[\"$mode\", $players]" "$t"
}

# uwaitview <ViewIdentifier> [timeout_s=60] -> wait until that view is on top (e.g. NinjagoSetup, Runner, NinjagoResult).
uwaitview() { urun "$UH_PLAYTEST" NinjagoPlaytest.WaitForView "[\"$1\", ${2:-60}]" $(( ${2:-60} + 10 )); }

# ustate -> play state, scene, top view, selected mode, simulated body, players.
ustate() { urun "$UH_PLAYTEST" NinjagoPlaytest.Status | jq -c '.result'; }

# ulean <player> <x_in> [y_in=0] [storm=false] -> simulated chest lean in body inches (x right, y up).
ulean() { urun "$UH_PLAYTEST" NinjagoPlaytest.Lean "[$1, $2, ${3:-0}, ${4:-false}]" | jq -c '.result'; }

# upress <buttonField> -> click a Button field of the top view (e.g. retryButton / menuButton on the result view).
upress() { urun "$UH_PLAYTEST" NinjagoPlaytest.PressButton "[\"$1\"]" | jq -c '.result'; }

# uchase -> Chest Chase readout (vehicle, time left, dodges, hits, vehicle position).
uchase() { urun "$UH_PLAYTEST" NinjagoPlaytest.Chase | jq -c '.result'; }

# uframes [frames=120] -> frame-time / PlayerLoop / batches / SetPass / tris / GC summary over the next N frames.
uframes() { urun "$UH_PLAYTEST" NinjagoPlaytest.FrameStats "[${1:-120}]" $(( ${1:-120} / 5 + 60 )) | jq '.result'; }

# uperf -> one-shot render / memory / frame-timing stats from the Editor (get_performance_stats).
uperf() { ucmd get_performance_stats | jq -c .; }
