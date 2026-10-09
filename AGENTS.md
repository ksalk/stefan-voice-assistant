# AGENTS.md

* Use ASD-STE100 as a guide for your responses. Write short sentences, use active voice, and keep terminology consistent. Relax the vocabulary rules when they make explanations awkward. Preserve technical precision and uncertainty.

## Tests

* Write unit tests only if they test behaviour and not implementation
* Prefer integration or e2e tests that treat the app as black-box
* If you want to run integrations tests after making changes to code, make sure to rebuild test docker images with mise run build-node-test-image or build-server-test-images.

## Database migrations

* Never run or generate database migrations (e.g. `dotnet ef migrations add`, `dotnet ef database update`) yourself. If your changes require a migration, notify the user to create and apply it instead.
