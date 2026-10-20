# Vehicle Management

A .NET 10 Blazor app for registering vehicles and assigning categories from their weight. The Categories page lets you add, edit, reorder, and delete weight categories.

## Run in GitHub Codespaces

Use a Linux x64 Codespace with Docker Compose and the .NET SDK in `global.json`.

```bash
./scripts/setup-database.sh
```

This starts SQL Server, applies the migrations, and creates a local `.env` for the VS Code launch configuration. The password is generated on first setup and saved in ignored, private local files. If the default host port `14330` is busy, run `MSSQL_HOST_PORT=14331 ./scripts/setup-database.sh`.

Start **Vehicle Management: Web** from VS Code's Run and Debug panel. The vehicle page is at `/`; category management is at `/categories`.

## Run the full test suite

```bash
./scripts/test-all.sh
```

This starts the local database if needed, applies migrations, then runs the Release test suite with the SQL Server tests enabled. The run should report **0 failed and 0 skipped**. SQL tests create and remove separate temporary databases.

## Rules and scope

Each category's minimum weight is inclusive; the next minimum is its exclusive upper boundary. Initial categories are Light `[0, 500)`, Medium `[500, 2500)`, and Heavy `[2500, +∞)` kilograms. Vehicles store weight, not a category ID, so their category reflects the current boundaries.

Saving category changes validates the complete proposal and uses a transaction plus row version to protect concurrent edits. When more than 100 vehicles would change category identity, the app asks for confirmation. The displayed count is an estimate at the time of checking.

