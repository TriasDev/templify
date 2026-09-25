#!/usr/bin/env bash
# Fails if the TRX file is missing or reports zero executed tests.
# Guards against release pipelines that silently run no tests.
set -euo pipefail

trx="${1:?usage: verify-test-results.sh <results.trx>}"

if [ ! -f "$trx" ]; then
  echo "::error::Test results file not found: $trx"
  exit 1
fi

executed=$(grep -o '<Counters [^>]*' "$trx" | sed -n 's/.*executed="\([0-9]*\)".*/\1/p' | head -n1)
failed=$(grep -o '<Counters [^>]*' "$trx" | sed -n 's/.*failed="\([0-9]*\)".*/\1/p' | head -n1)

echo "Executed tests: ${executed:-0}, failed: ${failed:-0}"

if [ -z "$executed" ] || [ "$executed" -eq 0 ]; then
  echo "::error::No tests were executed"
  exit 1
fi

if [ -n "$failed" ] && [ "$failed" -ne 0 ]; then
  echo "::error::$failed test(s) failed"
  exit 1
fi
