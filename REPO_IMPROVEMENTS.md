# Non-Functional Improvements

Improvements to make this a polished open-source/portfolio project. Focused on project hygiene, developer experience, CI/CD, and documentation — not feature changes.

---

## 1. Developer Environment & Configuration

- [ ] **`.editorconfig`** — Enforce consistent indent style, charset, line endings across Python and C#
- [ ] **Secrets documentation** — Add `dotnet user-secrets` instructions to server README; document required env vars
- [ ] **`pyproject.toml`** — Replace bare `requirements.txt` with `pyproject.toml` (project metadata, version, license, Python version requirement)

## 2. Code Quality Tooling

- [ ] **Ruff (Python)** — Add as dev dependency, configure in `pyproject.toml` `[tool.ruff]` section (linting + formatting)
- [X] **`Directory.Build.props` (.NET)** — Shared analyzer settings at solution root; `TreatWarningsAsErrors` for release builds
- [ ] **Pre-commit hooks** — `.pre-commit-config.yaml` with Ruff, `dotnet format`, trailing whitespace fixer, end-of-file fixer

## 3. CI/CD (GitHub Actions)

- [ ] **`.github/workflows/build.yml`**
  - Python job: install deps, `ruff check`, `ruff format --check`
  - .NET job: `dotnet restore`, `dotnet build`, `dotnet format --verify-no-changes`
  - Trigger on push and PR to `main`
- [ ] Add test jobs once tests exist

## 4. Containerization

- [ ] **`apps/server/Dockerfile`** — Multi-stage build (SDK → runtime), Whisper model mount
- [ ] **`apps/node/Dockerfile`** — Python slim + portaudio; note `--device` flags for audio access
- [ ] **`docker-compose.yml`** — Orchestrate both services, shared network, model volume mounts, device passthrough docs

## 5. Documentation

- [ ] **README.md** — Add badges (build status, license, .NET version, Python version), project structure tree, roadmap section
- [ ] **`CONTRIBUTING.md`** — Dev setup, code style guidelines (ruff, dotnet format), PR process, commit message format
- [ ] **`.github/ISSUE_TEMPLATE/`** — Bug report and feature request templates
- [ ] **`.github/PULL_REQUEST_TEMPLATE.md`** — PR checklist
- [ ] **`CHANGELOG.md`** — Document existing versions using Keep a Changelog format

## 6. Git Hygiene

- [X] **Conventional commits** — Document format in CONTRIBUTING.md (`feat:`, `fix:`, `docs:`, `chore:`, `refactor:`)
- [ ] **Root `.gitignore`** — IDE files, OS files (.DS_Store, Thumbs.db); keep component-specific ignores in their directories

## 7. Testing Foundation

- [ ] **`Stefan.Server.Tests`** — xUnit project in the solution with a placeholder test (establishes pattern + CI integration)
- [X] **`apps/node/tests/`** — Add `pytest` to dev deps, placeholder test file

## 8. OpenAPI / Swagger UI

- [ ] **Swagger UI** — `Microsoft.AspNetCore.OpenApi` is already included; add Scalar or Swagger UI in dev mode for API exploration
