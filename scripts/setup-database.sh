#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

passwordFile="$PWD/.sqlserver-password"
umask 077

if [[ -L "$passwordFile" ]]; then
    echo "The local SQL Server password file must not be a symbolic link." >&2
    exit 1
fi

if [[ -f "$passwordFile" ]]; then
    storedPassword="$(cat "$passwordFile")"
    if [[ -z "$storedPassword" ]]; then
        echo "The local SQL Server password file is empty." >&2
        exit 1
    fi

    if [[ -n "${MSSQL_SA_PASSWORD:-}" && "$MSSQL_SA_PASSWORD" != "$storedPassword" ]]; then
        echo "MSSQL_SA_PASSWORD differs from the saved local password. Unset it or restore the matching password file." >&2
        exit 1
    fi

    MSSQL_SA_PASSWORD="$storedPassword"
else
    if [[ -z "${MSSQL_SA_PASSWORD:-}" ]]; then
        if ! command -v openssl >/dev/null 2>&1; then
            echo "OpenSSL is required to generate a local SQL Server password." >&2
            exit 1
        fi

        MSSQL_SA_PASSWORD="Vm1!$(openssl rand -hex 24)"
    fi

    printf '%s' "$MSSQL_SA_PASSWORD" > "$passwordFile"
fi

chmod 600 "$passwordFile"
export MSSQL_SA_PASSWORD

# The connection string quotes its password so delimiters remain part of the password.
escapedPassword="${MSSQL_SA_PASSWORD//\"/\"\"}"
export ConnectionStrings__VehicleManagement="Server=localhost,1433;Database=VehicleManagement;User Id=sa;Password=\"${escapedPassword}\";Encrypt=True;TrustServerCertificate=True"

docker compose up -d --wait --wait-timeout 180 sqlserver
dotnet tool restore
dotnet restore VehicleManagement.sln
dotnet tool run dotnet-ef database update \
    --project src/VehicleManagement.Infrastructure \
    --startup-project src/VehicleManagement.Infrastructure

docker compose ps sqlserver
echo "VehicleManagement database is ready."
