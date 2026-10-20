#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

# Set up the same local SQL Server used by the app before running SQL tests.
./scripts/setup-database.sh

passwordFile="$PWD/.sqlserver-password"
portFile="$PWD/.sqlserver-port"
if [[ ! -s "$passwordFile" || ! -s "$portFile" ]]; then
    echo "SQL Server setup did not create its local password and port files." >&2
    exit 1
fi

MSSQL_SA_PASSWORD="$(cat "$passwordFile")"
MSSQL_HOST_PORT="$(cat "$portFile")"
escapedPassword="${MSSQL_SA_PASSWORD//\"/\"\"}"
export ConnectionStrings__VehicleManagement="Server=localhost,${MSSQL_HOST_PORT};Database=VehicleManagement;User Id=sa;Password=\"${escapedPassword}\";Encrypt=True;TrustServerCertificate=True"

dotnet test VehicleManagement.sln --no-restore --configuration Release -m:1 -p:UseSharedCompilation=false
