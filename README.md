# Intelligent Programming Platform

An ASP.NET Core MVC learning platform where students practice C++ and Python, inspect test results, ask an AI tutor for hints, and follow their progress. Built as a five-phase university project with a single MVC application and SQL Server database.

## Features

- Published tasks with search, topic/difficulty filters and personal solved badges.
- Nine Kazakh Python lessons form an ordered learning path at `/Lessons?language=python`, with examples and existing topic-based practice links.
- Locally bundled Monaco editor with dynamic C++/Python highlighting and separate drafts per account, task and language in tab session storage.
- C++ 20 and Python 3 execution in isolated Docker images pinned by immutable digest. Python supports the standard library only; no package installation.
- Visible examples, protected hidden tests, saved submissions and execution results.
- Optional AI Tutor using the OpenAI Responses API; structured advice is saved and reused.
- Progress reports: solved tasks, compilation errors, successful attempt percentage, average solving time and rating score.
- Public leaderboard with deterministic ranking and linked developer profiles; account IDs appear only in profile URLs, never as visible identity text.
- Editable display names and normalized profile avatars, shared with the leaderboard and navbar; Admin Users provides paginated profiles, statistics and avatar removal.
- Identity registration/login, personal history/profile, and an authorized Admin area for topics, tasks, test cases and leaderboard rebuilds.
- Shared dark theme, responsive workspace, accessible form labels and safe error pages.
- Three UI languages: **Қазақша**, **Русский**, **English**, including Admin and validation.

## Technology

ASP.NET Core MVC · .NET 10 · C# · Entity Framework Core · SQL Server · ASP.NET Core Identity · Docker · Monaco Editor · OpenAI Responses API (official .NET SDK) · Bootstrap 5.3 · Razor, CSS and JavaScript.

## Community Features

- Public developer profiles at `/Users/{id}` reuse avatars and show safe learning totals, level and XP. Email, source code, favorites, detailed activity and private AI feedback remain private.
- `/Friends` supports display-name search, incoming/outgoing friend requests, explicit acceptance, cancellation, removal and directional blocking.
- `/Messages` provides private one-to-one text conversations between accepted friends, paged history, unread counts and friend-request badges.
- Authenticated, antiforgery-protected MVC POST requests save first; SignalR sends real-time notifications only to the two participants. HTTP sending and saved history remain available during a hub outage.
- Blocking/unfriending prevents new messages and preserves existing history. Unblocking does not restore friendship.

**Private messages are plain text stored in SQL Server; they are not end-to-end encrypted. Admins do not receive a UI to browse private message bodies.** Conversation access checks also apply to Admin accounts. Database operators with direct SQL access remain a separate trust boundary.

Apply the single `AddSocialFeatures` migration with `dotnet ef database update`. The official SignalR 10.0.11 client is bundled locally: `npm ci` then `npm run build:social` (editor build remains `npm run build:editor`). No CDN, new CORS allowance, paid AI call or CSP relaxation is required.

Verification: `dotnet run --project tests/Enhancement8Checks`, `node scripts/verify_social_ui.mjs`, and, with the HTTPS app running, `python scripts/verify_enhancement8.py`. These use disposable fixtures; the HTTP suite includes three real authenticated WebSocket clients. Architecture, test evidence, browser status and manual checks: [study-enhancement8.md](study-enhancement8.md).

## Architecture

```text
Browser (Razor pages + Monaco + culture cookie)
   |
Request Localization (kk-KZ / ru-RU / en-US resources)
   |
ASP.NET Core MVC
   +-- Identity -------------------------- Accounts / roles / cookies
   +-- EF Core --------------------------- SQL Server
   +-- SubmissionService -- Docker ------- C++ compiler + test runner
   +-- OpenAiTutorService ----------------- OpenAI Responses API
   +-- ProgressService ------------------- Statistics from submissions
   +-- LearningInsightsService ------------ Learning Map / Practice Next
   +-- LeaderboardService ----------------- Rebuildable ranking summary
   +-- Social services ------------------- Profiles / friends / blocks / messages
           +-- SQL commit -> SignalR ----- Private real-time notification groups
```

Docker results determine the submission status. AI is requested separately and never executes or changes code automatically. A completed submission updates the leaderboard after its result is saved.

Avatars use **SkiaSharp 4.153.1** to decode JPEG/PNG/WebP uploads (maximum 4 MiB), reject unsupported/invalid images, and create a metadata-free square WebP up to 512×512. Only a generated relative path is stored in `ApplicationUser.AvatarPath` (`AddUserAvatar` migration). Runtime files in `wwwroot/uploads/avatars/` are ignored by Git and excluded from publish assets: preserve this directory separately when deploying/backing up. Local storage suits this university deployment; multiple production instances need shared object storage such as S3 or Azure Blob. Linux hosting also needs the matching SkiaSharp Linux native-assets package. `System.Security.Cryptography.Pkcs` is pinned to patched 9.0.21 to override the older SQL client transitive dependency reported by NuGet audit.

Enhancement 6 details and verification: [study-enhancement6.md](study-enhancement6.md). Run `dotnet run --project tests/Enhancement6Checks/Enhancement6Checks.csproj`, then `python scripts/verify_enhancement6.py` against the running HTTPS development app. The service checks create/delete an isolated SQL database and generate benign image fixtures under ignored `obj/`; HTTP checks remove their UUID users, submissions and uploaded avatars. Real browser viewport checks remain manual; automated HTTP checks do not establish visual correctness.

## Languages

The default interface is Kazakh (`kk-KZ`); Russian (`ru-RU`) and English (`en-US`) are available in the navigation language selector. ASP.NET Core request localization and `IStringLocalizer` load `.resx` resources for shared, student and Admin text. The selector posts with antiforgery protection, accepts only these three cultures, sets a Secure/HttpOnly/SameSite=Lax culture cookie for one year and redirects only to a local URL. It preserves the account session.

Task/topic titles and descriptions, source code, compiler diagnostics, custom input/output and saved AI feedback remain as stored. New AI analyses use the active UI language selected by trusted server context; switching languages or revealing hints never regenerates saved advice. Roles, routes, enum values and database fields remain invariant. Dates and numbers use the display culture; timestamps remain UTC. Localization introduces no migration.

## Local setup

1. Install the .NET 10 SDK, SQL Server and Docker Desktop with the **Linux containers** engine. Trust the local ASP.NET HTTPS certificate:

   ```powershell
   dotnet dev-certs https --trust
   ```

2. Ensure SQL Server is running. The development connection in `appsettings.json` targets `localhost`, database `IntelligentProgrammingPlatformDb`, using Windows authentication. Override `ConnectionStrings:DefaultConnection` through User Secrets when using another instance; do not commit a connection password. The development `TrustServerCertificate` setting is for local SQL only; configure a validated SQL certificate for deployment.

3. Start Docker Desktop and pull both fixed runner images:

   ```powershell
   docker info
   docker pull gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
   docker pull python@sha256:5024f48ba9441d4b13a95d3945abc6365538e3a31109833367a1923523c6efed
   ```

   This digest was resolved from the actual local `gcc:14.3.0-bookworm` RepoDigests and pulled/verified on 2026-10-01. The runner uses canonical `gcc@sha256:…` because Docker Desktop did not consistently resolve the combined tag-plus-digest lookup. It never falls back to a mutable tag. Updating GCC requires reviewing a newly resolved digest, updating the server constant and rerunning the runner regressions.

   The Python digest was resolved by actually pulling the official `python:3.13-slim-bookworm` image on 2026-10-02. The restricted container reported **Python 3.13.16**. Execution uses the immutable reference above, defined in `RunnerLanguage`; database image/command strings are metadata only. Python syntax checks and student code run only in Docker, with no host fallback.

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

   Open **https://localhost:7115**. The HTTPS profile is the default for `dotnet run`. Sign up for a Student account, or sign in with the configured development Admin. Development startup adds missing demo records: nine tasks across Basics, Arrays and Algorithms, including Easy, Medium and Hard, with visible and hidden tests. These include **Count Even Numbers**, **Palindrome Check**, **Binary Search**, and two Kazakh tasks: **Массив элементтерінің қосындысы** (`array-sum`, Arrays, Easy) and **Жолдағы дауысты әріптер саны** (`count-vowels`, Basics, Easy). Each task can be solved in either C++ or Python. Existing tasks/tests are preserved. Production does not seed demo tasks or an Admin account.

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
python scripts/verify_enhancement1.py
python scripts/verify_enhancement2.py
python scripts/verify_enhancement3.py
python scripts/verify_resources.py
python scripts/verify_enhancement4.py
python scripts/verify_enhancement5a.py
python scripts/verify_enhancement5b.py
node scripts/verify_editor_csp.mjs
node scripts/verify_enhancement2_ui.mjs
dotnet run --project tests/Enhancement1Checks
dotnet run --project tests/Enhancement3Checks
dotnet run --project tests/Enhancement5bChecks
dotnet ef migrations has-pending-model-changes
git diff --check
```

These suites make **no paid OpenAI calls**. Run the database suites sequentially. Existing HTTP suites explicitly use English; Enhancement 4 tests the Kazakh default and all three languages, cookie/redirect protection, content preservation and the Kazakh workflow with clearly identified synthetic saved advice. C# SDK tests intercept the HTTP transport locally and verify the trusted AI language boundary. Live verification is a separate explicit operation:

```powershell
python scripts/verify_phase5_live.py --live
# Completes the demo using stored advice, then removes the temporary session:
python scripts/verify_phase5_live.py --finish
```

`--live` requests two paid analyses at most, with no automatic API retries. Do not run it repeatedly just to check UI changes. The temporary authentication session is stored only in ignored `obj/` until `--finish`; keep both stages on the same development machine. `--live-wrong-only` is a one-call recovery option when the compilation case has already been verified.

## Learning Journey Features

- **Custom Run:** enter your own input and select **Run** to see temporary stdout, diagnostics and execution time without leaving the task. It shares the pinned Docker sandbox and execution limit with Submit. Source/input limits are 64/32 KiB UTF-8.
- **Run vs Submit:** Run does not judge against official answers or save anything to submission history, AI feedback, Progress or Leaderboard. **Submit** checks official tests and records an attempt normally.
- **Attempt Journey:** the task page shows your latest five attempts with their runtime; `/Tasks/{slug}/Journey` provides the full chronological history, twenty per page. Saved AI feedback is marked factually; attempts link to existing result pages.
- **Code Diff:** **Compare with previous** selects the previous attempt in the same language. Both attempts must belong to you and the same task. Same-language comparisons use C++/Python highlighting; an explicit mixed comparison uses plaintext. The diff reuses the existing nonce/scoped Monaco CSP and switches to an inline view on narrow screens.

Run `python scripts/verify_enhancement2.py` and `node scripts/verify_enhancement2_ui.mjs` for these regressions. See [study-enhancement2.md](study-enhancement2.md), [manual-enhancement2-checklist.md](manual-enhancement2-checklist.md), and the current browser verification scope in [study-enhancement7.md](study-enhancement7.md).

## Advanced Learning Experience

- **Accepted AI Code Review:** the owner explicitly requests a concise quality review through the existing OpenAI SDK. One stored review per submission is reused, including after a language change. Hidden tests and account details are excluded. AI-estimated time/space complexity is clearly marked as an estimate that may be wrong; measured runtime remains separate and this is not a proof of algorithmic complexity.
- **XP / Levels:** distinct Accepted tasks award Easy **50**, Medium **100**, Hard **200** XP once across languages. Completing a published lesson awards **20** XP once. XP is derived from history; advancing from level `L` requires `250 × L`, and entering level `L` requires `250 × (L − 1) × L / 2` cumulative XP. Leaderboard and Learning Map formulas stay separate.
- **Task Favorites:** authenticated students bookmark published tasks from the catalog/workspace and visit `/Tasks/Favorites` through Tasks or Profile. Lesson completion and favorites use protected POST actions; completion is one-way and protects lesson history from deletion.
- **Submission Activity Heatmap:** Profile includes 365 UTC days of official attempts and Accepted counts, with SQL aggregation and accessible labels. Temporary Custom Runs do not contribute.
- **Solving workspace:** desktop panes use a 42/58 split with independent scrolling; smaller screens stack them. Monaco has language-specific draft status, confirmed starter reset, Run/Submit busy guards and editor-scoped Ctrl/Cmd+Enter / Ctrl/Cmd+Shift+Enter shortcuts. Existing avatar, journey, diff, hints and sandbox protections remain integrated.

Apply the single `AddLearningEngagementFeatures` migration with `dotnet ef database update`. Verify without paid AI calls using `dotnet run --project tests/Enhancement7Checks`, `python scripts/verify_enhancement7.py`, and the existing regression suites. The explicit `--live-review` mode permits one paid review and writes a local ledger to prevent accidental reruns. See [study-enhancement7.md](study-enhancement7.md) for the Kazakh guide, file inventory, verification evidence and remaining manual checks.

## Lessons / Learning Content (Enhancement 5A)

- `/Lessons` is an anonymous, published-only catalog with a topic filter. `/Lessons/{slug}` shows theory, display-only code examples, up to three published tasks in the same topic, and ordered previous/next lessons.
- `/Admin/Lessons` supports create, preview, edit, publish/unpublish and confirmed deletion. Slugs are unique lowercase URL identifiers; lesson order is positive and unique within its topic. Topics containing tasks or lessons cannot be deleted.
- Apply `AddLessons` with `dotnet ef database update`. Development startup seeds five Kazakh lessons: Programming Basics, C++ Basics, Python Basics, Arrays Basics and Algorithm Basics. Existing slugs and edited content are preserved. A deleted demo slug is recreated on the next Development startup; unpublish it to hide it persistently.
- UI supports Kazakh, Russian and English; database lesson text remains as authored. Text and `<pre><code>` examples use normal Razor encoding. No Markdown library or CSP relaxation was added.
- Lesson code blocks remain reading material; students select **Python 3** on a task page to Run or Submit. The untouched original Python lesson is upgraded to cover `input`, `print`, variables, `if`, `for` and lists. Admin-edited lesson bodies are preserved. Enhancement 7 adds one-way lesson completion and derived XP.

Run `python scripts/verify_enhancement5a.py` against the running HTTPS Development app. See [study-enhancement5a.md](study-enhancement5a.md) for the Kazakh guide, schema, security, verification and manual browser checklist. Real visual/keyboard/console checks at 1440, 1024 and 390 pixels remain pending because no browser was available.

Enhancement 5A verification (2026-10-02): the Lessons suite and all preceding regressions passed; database content was preserved. See [study-enhancement5b.md](study-enhancement5b.md) for the current Python architecture, three new tasks, actual verification results and manual browser checklist. Enhancement 5B requires no new migration and has **581 resource keys per language**.

## Intelligent Learning features (Enhancement 3)

- **Progressive AI hints:** one analysis generates three increasingly specific hints. Hint 1 appears immediately; later hints stay on the server until revealed. Revealing saves your progress and makes no additional AI request. Older feedback with fewer hints remains usable.
- **Learning Weakness Map:** `/Progress` shows topic strength and official error patterns from your submissions on published tasks. System errors and temporary Runs are excluded; AI-analyzed categories are shown separately and do not affect scores.
- **Practice Next:** a deterministic recommendation selects an unsolved published task by topic practice priority, strength, then difficulty and stable IDs. Leaderboard scoring remains unchanged.

Apply `AddProgressiveHintReveal` with `dotnet ef database update`. Verify with `dotnet run --project tests/Enhancement3Checks` and `python scripts/verify_enhancement3.py`. See [study-enhancement3.md](study-enhancement3.md) and [manual-enhancement3-checklist.md](manual-enhancement3-checklist.md); visual browser checks remain pending.

## Demo workflow

1. Select **Қазақша**, open the home page, register/login and choose **Есептер → Sum of Two Numbers**.
2. Write incorrect C++ that always prints zero, enter custom input and select **Run**. Explain that this temporary result saves no attempt.
3. Submit the solution; show **Қате жауап**, a failed visible test and hidden-test metadata only.
4. Request AI analysis when configured; show Hint 1, try reasoning, then reveal Hint 2. Explain that each later reveal is free of additional AI calls.
5. Edit the code to read both integers and calculate their sum. Submit and show **Қабылданды**.
6. Compare the two saved attempts, open Attempt Journey, then Progress, Learning Map and Practice Next.
7. Open Leaderboard; re-solving the same task adds no duplicate score. Switch to Russian, English and back to Kazakh; the account and saved content remain unchanged.

See [study-phase5.md](study-phase5.md) for the Kazakh defense script, detailed audit and verification record.
The final localization explanation and verification boundaries are in [study-enhancement4.md](study-enhancement4.md). Use [manual-enhancement4-checklist.md](manual-enhancement4-checklist.md) for the remaining real-browser review.

## Security and scope

- Global antiforgery validation covers state-changing MVC actions; only read-only error rendering is exempt. Admin authorization and submission ownership are enforced on the server.
- Identity cookies are HttpOnly, Secure and SameSite=Lax. HTTPS redirection and production HSTS remain enabled. Security headers include nosniff, frame denial, referrer policy and disabled unused device permissions.
- Hidden input, expected/actual output and diagnostics are excluded from student SQL projections and AI input. User-controlled content is Razor encoded.
- Containers use pinned images, no network, a non-root user, dropped capabilities, no-new-privileges, a read-only root, bounded mounts, CPU/memory/PID/time/output limits and cleanup. Student C++ and Python never execute on the host. Both languages share the same two-slot execution gate for Run and Submit.
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

On 2026-10-02, the final enhancement passed all seven existing HTTP suites, three C# suites, two Node suites, the new three-language HTTP/SQL/Docker suite and resource consistency checks. Both Docker-unavailable variants passed. Build finished with **0 errors and 0 warnings**; the database is current with **no localization migration**. Nine resource files contain **526 keys per language**. Language switching preserved every database-table hash; test fixtures and runner workspaces were cleaned. This enhancement used **no live OpenAI calls**. The upgrade-only Enhancement 3 migration test was not rerun against the already migrated database.

**The final enhancement's browser attempt also found no available browser; actual viewports/languages visually checked: 0.** Follow [manual-enhancement4-checklist.md](manual-enhancement4-checklist.md) at 1440 × 900, 1024 × 768 and 390 × 844 in all three languages. It covers main pages, Monaco input/worker/CSP console, drafts, submission, navigation and responsive layouts. HTTP, resource and simulated editor checks do not replace that review.
