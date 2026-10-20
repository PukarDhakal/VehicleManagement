# VehicleManagement
A web application for vehicle management and dynamic weight based categorisation, being developed as part of the CreditWorks technical assessment.

## Current progress

- Domain objects, category rules, and domain tests are implemented.
- EF Core SQL Server mapping and the initial migration are implemented.
- The database setup below applies that migration and its starting categories and manufacturers.
- Repository implementations, application workflows, and the vehicle management UI are still to be built. The current web page is a shell and does not yet query the database.

## Set up SQL Server in GitHub Codespaces

Use a Linux x64 Codespace with Docker Compose and the .NET SDK specified in `global.json` (10.0.402).