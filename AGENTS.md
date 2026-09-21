# AGENTS.md

## Database migrations

* Never run or generate database migrations (e.g. `dotnet ef migrations add`, `dotnet ef database update`) yourself. If your changes require a migration, notify the user to create and apply it instead.
