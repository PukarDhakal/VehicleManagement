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

portFile="$PWD/.sqlserver-port"
if [[ -L "$portFile" ]]; then
    echo "The local SQL Server port file must not be a symbolic link." >&2
    exit 1
fi

if [[ -z "${MSSQL_HOST_PORT:-}" ]]; then
    if [[ -f "$portFile" ]]; then
        MSSQL_HOST_PORT="$(cat "$portFile")"
    else
        MSSQL_HOST_PORT=14330
    fi
fi

if [[ ! "$MSSQL_HOST_PORT" =~ ^[1-9][0-9]{0,4}$ ]] || (( 10#$MSSQL_HOST_PORT > 65535 )); then
    echo "MSSQL_HOST_PORT must be a number from 1 to 65535." >&2
    exit 1
fi

printf '%s' "$MSSQL_HOST_PORT" > "$portFile"
chmod 600 "$portFile"
export MSSQL_HOST_PORT

# The connection string quotes its password so delimiters remain part of the password.
escapedPassword="${MSSQL_SA_PASSWORD//\"/\"\"}"
export ConnectionStrings__VehicleManagement="Server=localhost,${MSSQL_HOST_PORT};Database=VehicleManagement;User Id=sa;Password=\"${escapedPassword}\";Encrypt=True;TrustServerCertificate=True"

if ! docker compose up -d --wait --wait-timeout 180 sqlserver; then
    echo "SQL Server did not start. If port ${MSSQL_HOST_PORT} is in use, choose another with MSSQL_HOST_PORT=14331 ./scripts/setup-database.sh." >&2
    exit 1
fi
dotnet tool restore
dotnet restore VehicleManagement.sln
dotnet tool run dotnet-ef database update \
    --project src/VehicleManagement.Infrastructure \
    --startup-project src/VehicleManagement.Infrastructure

# VS Code starts the web app in a new process, so it needs its own saved environment.
envFile="$PWD/.env"
if [[ -L "$envFile" ]]; then
    echo "The local .env file must not be a symbolic link." >&2
    exit 1
fi
if [[ -e "$envFile" && ! -f "$envFile" ]]; then
    echo "The local .env path must be a regular file." >&2
    exit 1
fi
if [[ "$ConnectionStrings__VehicleManagement" == *$'\n'* || "$ConnectionStrings__VehicleManagement" == *$'\r'* ]]; then
    echo "The SQL Server password cannot contain a newline when used with VS Code's .env file." >&2
    exit 1
fi

envTemp="$(mktemp "$PWD/.env.XXXXXX")"
trap 'rm -f "$envTemp"' EXIT
if [[ -f "$envFile" ]]; then
    while IFS= read -r line || [[ -n "$line" ]]; do
        if [[ "$line" =~ ^[[:space:]]*(export[[:space:]]+)?(ConnectionStrings__VehicleManagement|MSSQL_HOST_PORT)[[:space:]]*= ]]; then
            continue
        fi
        printf '%s\n' "$line" >> "$envTemp"
    done < "$envFile"
fi

printf 'MSSQL_HOST_PORT=%s\n' "$MSSQL_HOST_PORT" >> "$envTemp"
if [[ "$ConnectionStrings__VehicleManagement" == *'#'* ]]; then
    if [[ "$ConnectionStrings__VehicleManagement" == *"'"* ]]; then
        escapedEnvConnection="${ConnectionStrings__VehicleManagement//\\/\\\\}"
        escapedEnvConnection="${escapedEnvConnection//\"/\\\"}"
        printf 'ConnectionStrings__VehicleManagement="%s"\n' "$escapedEnvConnection" >> "$envTemp"
    else
        printf "ConnectionStrings__VehicleManagement='%s'\n" "$ConnectionStrings__VehicleManagement" >> "$envTemp"
    fi
else
    printf 'ConnectionStrings__VehicleManagement=%s\n' "$ConnectionStrings__VehicleManagement" >> "$envTemp"
fi
chmod 600 "$envTemp"
mv "$envTemp" "$envFile"
trap - EXIT

docker compose ps sqlserver
echo "VehicleManagement database is ready. VS Code can now load its connection from .env."
