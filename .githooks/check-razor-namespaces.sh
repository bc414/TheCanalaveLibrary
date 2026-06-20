#!/bin/sh
# Enforces the flat-namespace convention on staged .razor files: every component must declare
# @namespace TheCanalaveLibrary.{Core,Server,SharedUI,Client} matching its project, regardless of
# folder depth. See .claude/skills/canalave-conventions/SKILL.md,
# "Enforcing the Flat Namespace on Razor Files".
#
# _Imports.razor files are exempt (they hold @using directives, not a namespace).
#
# This checks for a missing/incorrect @namespace directive only. It does not catch stale
# fully-qualified references left over from a folder rename — when moving/renaming a folder that
# contains .razor files, grep the repo for the old dotted-path namespace string by hand.

violations=0

files=$(git diff --cached --name-only --diff-filter=ACMR -- '*.razor')

for file in $files; do
    base=$(basename "$file")
    [ "$base" = "_Imports.razor" ] && continue

    case "$file" in
        TheCanalaveLibrary.Core/*)     expected="TheCanalaveLibrary.Core" ;;
        TheCanalaveLibrary.Server/*)   expected="TheCanalaveLibrary.Server" ;;
        TheCanalaveLibrary.SharedUI/*) expected="TheCanalaveLibrary.SharedUI" ;;
        TheCanalaveLibrary.Client/*)   expected="TheCanalaveLibrary.Client" ;;
        *) continue ;;
    esac

    if ! git show ":$file" | head -n 3 | grep -qE "^@namespace[[:space:]]+${expected}[[:space:]]*\$"; then
        echo "ERROR: $file must declare '@namespace $expected' (first line, or second line right after @page)."
        violations=1
    fi
done

if [ "$violations" -ne 0 ]; then
    echo ""
    echo "See .claude/skills/canalave-conventions/SKILL.md > 'Enforcing the Flat Namespace on Razor Files'."
    exit 1
fi

exit 0
