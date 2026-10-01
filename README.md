# Intelligent Programming Platform

An ASP.NET Core MVC learning platform where students practice C++, inspect test results, ask an AI tutor for hints, and follow their progress. Built as a five-phase university project with a single MVC application and SQL Server database.

## Features

- Published tasks with search, topic/difficulty filters and personal solved badges.
- Locally bundled Monaco editor with a per-account, per-task draft in tab session storage.
- C++ 20 compilation and testing in isolated, resource-limited Docker containers.
- Visible examples, protected hidden tests, saved submissions and execution results.
- Optional AI Tutor using the OpenAI Responses API; structured advice is saved and reused.
- Progress reports: solved tasks, compilation errors, successful attempt percentage, average solving time and rating score.
- Public leaderboard with deterministic ranking and private account identifiers excluded.
- Identity registration/login, personal history/profile, and an authorized Admin area for topics, tasks, test cases and leaderboard rebuilds.
- Shared dark theme, responsive workspace, accessible form labels and safe error pages.

## Technology

ASP.NET Core MVC · .NET 10 · C# · Entity Framework Core · SQL Server · ASP.NET Core Identity · Docker · Monaco Editor · OpenAI Responses API (official .NET SDK) · Bootstrap 5.3 · Razor, CSS and JavaScript.

## Architecture

```text
Browser (Razor pages + Monaco)
   |
ASP.NET Core MVC
   +-- Identity -------------------------- Accounts / roles / cookies
   +-- EF Core --------------------------- SQL Server
   +-- SubmissionService -- Docker ------- C++ compiler + test runner
   +-- OpenAiTutorService ----------------- OpenAI Responses API
   +-- ProgressService ------------------- Statistics from submissions
   +-- LeaderboardService ----------------- Rebuildable ranking summary
```

Docker results determine the submission status. AI is requested separately and never executes or changes code automatically. A completed submission updates the leaderboard after its result is saved.

## Local setup

1. Install the .NET 10 SDK, SQL Server and Docker Desktop with the **Linux containers** engine. Trust the local ASP.NET HTTPS certificate:

   ```powershell
   dotnet dev-certs https --trust
   ```

2. Ensure SQL Server is running. The development connection in `appsettings.json` targets `localhost`, database `IntelligentProgrammingPlatformDb`, using Windows authentication. Override `ConnectionStrings:DefaultConnection` through User Secrets when using another instance; do not commit a connection password. The development `TrustServerCertificate` setting is for local SQL only; configure a validated SQL certificate for deployment.

3. Start Docker Desktop and pull the runner's fixed image:

   ```powershell
   docker info
   docker pull gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
   ```

   This digest was resolved from the actual local `gcc:14.3.0-bookworm` RepoDigests and pulled/verified on 2026-10-01. The runner uses canonical `gcc@sha256:…` because Docker Desktop did not consistently resolve the combined tag-plus-digest lookup. It never falls back to a mutable tag. Updating GCC requires reviewing a newly resolved digest, updating the server constant and rerunning the runner regressions.

   Docker Desktop must remain running while solutions are submitted. The runner uses known Docker installation paths and does not execute command/image text from the database. On Windows, the supported installations are Docker Desktop under Program Files or the current user's LocalAppData Programs directory.

4. Restore packages and the repository's EF tool:

   ```powershell
   dotnet restore
   dotnet tool restore
   ```

5. Apply the existing migrations:

   ```powershell
   dotnet ef database update
   ```

6. Configure development credentials using the existing project User Secrets ID. Use your own values in place of these placeholders:

   ```powershell
   dotnet user-secrets set "SeedAdmin:Email" "<ADMIN_EMAIL>"
   dotnet user-secrets set "SeedAdmin:Password" "<STRONG_ADMIN_PASSWORD>"
   dotnet user-secrets set "SeedAdmin:DisplayName" "Development Admin"
   dotnet user-secrets set "OpenAI:ApiKey" "<YOUR_API_KEY>"
   ```

   The model defaults to `gpt-6-luna`; `OpenAI:Model` can explicitly override it with an accessible Responses/structured-output model. AI remains unavailable until a key is configured; tasks, submissions and reports still work. Never print User Secrets in diagnostic output. User Secrets are for development; production must use protected server configuration. Admin seeding never promotes an existing Student just because its email matches.

7. Build and run:

   ```powershell
   dotnet build
   dotnet run
   # To select the HTTPS profile explicitly:
   dotnet run --launch-profile https
   ```

   Open **https://localhost:7115**. The HTTPS profile is the default for `dotnet run`. Sign up for a Student account, or sign in with the configured development Admin. Development startup adds missing demo records without overwriting existing ones: four tasks across Basics, Arrays and Algorithms, including Easy, Medium and Hard, with visible and hidden tests. Production does not seed demo tasks or an Admin account.

Monaco assets are intentionally committed under `wwwroot/js/editor`. Node.js is only needed to rebuild them after editor-source changes:

```powershell
npm ci
npm run build:editor
```

## Verification

With SQL Server, Docker Desktop and the HTTPS development app running, the scripts use isolated temporary accounts and clean up their fixture rows. Python 3 and `sqlcmd` with Windows authentication must be available on PATH.

```powershell
python scripts/verify_phase2.py
python scripts/verify_phase3.py
python scripts/verify_phase4.py
dotnet run --project tests/Phase4Checks/Phase4Checks.csproj
python scripts/verify_phase5.py
dotnet ef migrations has-pending-model-changes
git diff --check
```

These suites make **no paid OpenAI calls**. The C# SDK tests intercept the HTTP transport locally. Live verification is a separate explicit operation:

```powershell
python scripts/verify_phase5_live.py --live
# Completes the demo using stored advice, then removes the temporary session:
python scripts/verify_phase5_live.py --finish
```

`--live` requests two paid analyses at most, with no automatic API retries. Do not run it repeatedly just to check UI changes. The temporary authentication session is stored only in ignored `obj/` until `--finish`; keep both stages on the same development machine. `--live-wrong-only` is a one-call recovery option when the compilation case has already been verified.

## Demo workflow

1. Register/login, open **Tasks**, select **Sum of Two Numbers**.
2. Write incorrect C++ that always prints zero, then **Submit solution**.
3. Show **Wrong answer** and the failed visible test; hidden tests show metadata only.
4. Click **Analyze with AI** and discuss the explanation and hints.
5. Return to the editor, read the inputs and calculate the sum yourself; resubmit.
6. Show **Accepted**, then open **Progress** to show the updated reports.
7. Open **Leaderboard** to show the updated score/rank; re-solving the same task adds no duplicate score.

See [study-phase5.md](study-phase5.md) for the Kazakh defense script, detailed audit and verification record.

## Security and scope

- Global antiforgery validation covers state-changing MVC actions; only read-only error rendering is exempt. Admin authorization and submission ownership are enforced on the server.
- Identity cookies are HttpOnly, Secure and SameSite=Lax. HTTPS redirection and production HSTS remain enabled. Security headers include nosniff, frame denial, referrer policy and disabled unused device permissions.
- Hidden input, expected/actual output and diagnostics are excluded from student SQL projections and AI input. User-controlled content is Razor encoded.
- Containers use a fixed image, no network, a non-root user, dropped capabilities, no-new-privileges, a read-only root, bounded mounts, CPU/memory/PID/time/output limits and cleanup. Student C++ never runs directly in the web-server process.
- AI receives untrusted data separately from trusted instructions, returns validated JSON, and is limited by ownership, terminal status, concurrency, cooldown and one stored feedback per submission. Credentials and AI request bodies are not logged.
- CSP uses a random 256-bit nonce per request, local assets/workers, no script `unsafe-inline` or `unsafe-eval`, and denies framing, objects and base changes. Import maps, progress styles and Monaco-generated stylesheets carry the nonce. Only rendered editor pages permit style **attributes** (`style-src-attr 'unsafe-inline'`), required by Monaco's line layout; other pages deny them. `scripts/monaco-csp.mjs` adapts the two reviewed stylesheet factories and fails on an unreviewed Monaco upgrade. MVC's empty validation placeholder uses a CSS class. Browser confirmation remains pending.
- `AllowedHosts` defaults to `localhost;127.0.0.1`, keeping `https://localhost:7115` working. The ignored Development settings file inherits this safe default. For Production, explicitly set `AllowedHosts` through deployment environment/configuration to your real semicolon-separated host names (no schemes or ports); no production domain is assumed. Until configured, only loopback host names are accepted. Empty lists and wildcard entries fail startup in every environment; unexpected Host headers receive 400. Behind a proxy, retain the intended Host and configure proxy trust separately.
- Compilation OOM is reported as a memory failure when Docker confirms `OOMKilled`, even with exit 137. GNU timeout 124 or the host watchdog establishes a time failure; unexplained 137 gets a generic termination message. Unmeasured memory stays NULL. This remains a single-process educational application with process-local concurrency gates.

## Project phases and current verification

| Phase | Result |
|---|---|
| 1 | Database, models, relationships and migrations |
| 2 | Identity, roles, task catalog and Admin CRUD |
| 3 | Monaco, isolated C++ execution and submission history |
| 4 | AI feedback, progress and leaderboard |
| 5 | Consistent UI, security review, regression testing and defense documentation |

On 2026-10-01, Phase 2–4 regressions and Phase 5 HTTP/SQL checks passed. Two actual OpenAI calls succeeded; the injection case returned hints without hidden values or a ready-made program, and repeat analysis reused stored feedback. The final HTTPS workflow reached Accepted and updated progress/ranking. Production-mode headers, HTTPS redirect and error routes were also checked.

Enhancement 1 adds `python scripts/verify_enhancement1.py`, `node scripts/verify_editor_csp.mjs` and `dotnet run --project tests/Enhancement1Checks/Enhancement1Checks.csproj`. The last command runs safely bounded real compilation tests (including test-only 32 MB OOM and 10 ms timeout limits). These checks do not request paid AI analysis. See [study-enhancement1.md](study-enhancement1.md) for the Kazakh hardening guide and verification record.

**Browser visual testing was unavailable; actual viewports checked: 0.** Follow [manual-visual-checklist.md](manual-visual-checklist.md) at 1440 × 900, 1024 × 768 and 390 × 844. It covers all main pages, Monaco highlighting/input/worker/CSP console, drafts, submission, navigation and responsive layouts. HTTP, bundle and simulated editor checks do not replace that review.
