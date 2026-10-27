#!/usr/bin/env bash
# Adds a migration to every provider-specific migration set for a context, then verifies
# that no provider is left with pending model changes.
#
# Usage: scripts/add-migration.sh <MigrationName> [v2|keys]
#   v2   (default) OosDbContext            -> Migrations/V2/<Provider>
#   keys           DataProtectionKeyContext -> Migrations/DataProtectionKey/<Provider>
#
# The legacy ApplicationDbContext is SQL Server only and is not handled here.
set -euo pipefail

name="${1:?usage: scripts/add-migration.sh <MigrationName> [v2|keys]}"
target="${2:-v2}"

case "$target" in
  v2)   suffix=OosDbContext;            dir=Migrations/V2 ;;
  keys) suffix=DataProtectionKeyContext; dir=Migrations/DataProtectionKey ;;
  *)    echo "unknown target '$target' (expected v2 or keys)" >&2; exit 1 ;;
esac

providers=(SqlServer Sqlite Npgsql)

cd "$(dirname "$0")/.."
dotnet tool restore >/dev/null
dotnet build -nologo -v q

for p in "${providers[@]}"; do
  echo "== ${p}${suffix}"
  dotnet ef migrations add "$name" --context "${p}${suffix}" -o "$dir/$p" --no-build
done

dotnet build -nologo -v q
for p in "${providers[@]}"; do
  dotnet ef migrations has-pending-model-changes --context "${p}${suffix}" --no-build
done
