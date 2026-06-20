#!/bin/sh
# Activation shim for the pre-commit hook. .git/hooks/ is not tracked by git, so this copy is the
# shareable source of truth. To activate in a fresh clone, run from the repo root:
#   cp .githooks/pre-commit-shim.sh .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
exec "$(git rev-parse --show-toplevel)/.githooks/check-razor-namespaces.sh"
