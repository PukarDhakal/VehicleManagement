#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [[ -z "${MSSQL_SA_PASSWORD:-}" ]]; then
    echo "Set MSSQL_SA_PASSWORD to a strong development password before running this script." >&2
    exit 1
fi

# The connection string quotes its password so delimiters remain part of the password.
escapedPassword="${MSSQL_SA_PASSWORD//\"/\"\"}"
export ConnectionStrings__VehicleManagement="Server=localhost,1433;Database=VehicleManagement;User Id=sa;Password=\"${escapedPassword}\";Encrypt=True;TrustServerCertificate=True"

docker compose up -d --wait --wait-timeout 180 sqlserver
dotnet tool restore
dotnet restore VehicleManagement.sln
dotnet tool run dotnet-ef database update \
    --project src/VehicleManagement.Infrastructure \
    --startup-project src/VehicleManagement.Infrastructure

echo "VehicleManagement database is ready."
