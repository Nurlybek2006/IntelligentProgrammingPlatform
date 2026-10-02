# IntelligentProgrammingPlatform: жоба картасы

Бұл карта 2026-10-02 күнгі нақты файлдарға сүйенеді. Толық түсіндірме — [DEFENSE-PREP.md](DEFENSE-PREP.md), соңғы қайталау — [DEFENSE-CHEATSHEET.md](DEFENSE-CHEATSHEET.md). Оқу бағыты: іске қосылу → деректер → сұрау → қызмет → сыртқы жүйе → экран.

## Жалпы байланыс

```text
Program.cs: DI + Identity + localization + middleware + routing + seeding
                                  |
Browser -> Controllers -----------+-> Services -> ApplicationDbContext -> SQL Server
                |                 |       +-----> Docker CLI -> Linux containers
                |                 |       +-----> OpenAI Responses API
                +-> ViewModels -> Razor Views -> HTML + local JavaScript/CSS

Areas/Admin/Controllers -----------------------> ApplicationDbContext
LessonsController -----------------------------> ApplicationDbContext
```

Бұл MVC қолданбасы қызметтермен толықтырылған. Барлық controller міндетті түрде service арқылы өтпейді: Lessons және Admin CRUD деректерге EF Core арқылы тікелей қатынайды. Docker мен OpenAI бір-біріне тәуелсіз: runner баға береді, AI түсіндіреді.

## Түбірдегі іске қосу және конфигурация

| Файл | Міндеті және тәуелділігі | Алдымен нені қарау керек? |
|---|---|---|
| [Program.cs](Program.cs) | Қызметтерді тіркейді, cookie/Identity/localization/security баптайды, маршрут пен seed іске қосады | `AddDbContext`, `AddIdentity`, қызметтердің lifetime-ы, middleware реті |
| [IntelligentProgrammingPlatform.csproj](IntelligentProgrammingPlatform.csproj) | `net10.0`, EF/Identity SQL Server 10.0.12, OpenAI 2.14.0; `tests` C# файлдарын негізгі web build-тан бөледі | `TargetFramework`, `PackageReference` |
| [appsettings.json](appsettings.json) | Connection string, AllowedHosts, AI-дың модель/уақыт/token параметрлері | Құпия key осы файлда емес; Development-та User Secrets пайдаланылады |
| [Properties/launchSettings.json](Properties/launchSettings.json) | Development іске қосу профильдері мен порттар | `https` профилі: HTTPS 7115, HTTP 5191 |
| [dotnet-tools.json](dotnet-tools.json) | Репозиторийдің жергілікті `dotnet-ef` құралы | `dotnet tool restore` не үшін керегін түсіну |
| [package.json](package.json), [package-lock.json](package-lock.json) | Monaco 0.57.0 және esbuild 0.28.2; editor build тәуелділіктері | `npm run build:editor`; Node негізгі web-сервер емес |
| [.gitignore](.gitignore) | `bin/obj`, жергілікті файлдар және басқа артефактілерді Git-тен шығарады | Қандай файл source, қайсысы build нәтижесі екенін ажырату |

## Controllers/

**Міндеті:** HTTP сұрауын қабылдау, пайдаланушы/форма шарттарын тексеру, қызметті шақыру, View/JSON/redirect қайтару. Тәуелділіктері — Identity, қызметтер, DbContext және ViewModels.

| Файл | Негізгі жауапкершілігі |
|---|---|
| [AccountController.cs](Controllers/AccountController.cs) | Register, Login, Logout, Profile; `UserManager` және `SignInManager` |
| [TasksController.cs](Controllers/TasksController.cs) | Жарияланған есептер каталогы, сүзгілер, есеп беті |
| [CustomRunsController.cs](Controllers/CustomRunsController.cs) | Авторизацияланған Run POST; нәтижені JSON-ға айналдыру |
| [SubmissionsController.cs](Controllers/SubmissionsController.cs) | Submit, My, Details, Compare, Analyze; жеке submission рұқсаттары |
| [HintRevealsController.cs](Controllers/HintRevealsController.cs) | Кеңестің келесі сатысын ашатын POST |
| [JourneysController.cs](Controllers/JourneysController.cs) | Бір есеп бойынша өз әрекеттерінің беттелген тарихы |
| [ProgressController.cs](Controllers/ProgressController.cs) | Жеке статистика, оқу картасы және келесі есеп ұсынысы |
| [LeaderboardController.cs](Controllers/LeaderboardController.cs) | Жалпы рейтинг беті |
| [LessonsController.cs](Controllers/LessonsController.cs) | Жарияланған сабақтар, көрші сабақтар, байланысты есептер |
| [CultureController.cs](Controllers/CultureController.cs) | Рұқсат етілген мәдениетті cookie-ге жазу, жергілікті бетке қайтару |
| [HomeController.cs](Controllers/HomeController.cs) | Басты бет, соңғы нәтижелер, Privacy/Error |

**Алдымен:** `TasksController.Details` → `SubmissionsController.Submit`. Кейін Run, Analyze және Compare әрекеттеріндегі рұқсатты салыстырыңыз. GET бет ашуды, POST күй өзгертуді қалай бөліп тұрғанына назар аударыңыз.

## Data/

**Міндеті:** EF моделі, байланыстар мен бастапқы деректер. `Models`, EF Core, SQL Server және seed кезінде Identity-ге тәуелді.

- [ApplicationDbContext.cs](Data/ApplicationDbContext.cs): `IdentityDbContext<ApplicationUser>`, DbSet, кілттер, unique index, FK және өшіру ережелері.
- [DbSeeder.cs](Data/DbSeeder.cs): рөлдер; Development-та runtime, бастапқы есептер және конфигурация берілгендегі admin дайындауы.
- [LessonSeeder.cs](Data/LessonSeeder.cs): бастапқы жарияланған сабақтарды қайталамай қосу.
- [PythonLessonContent.cs](Data/PythonLessonContent.cs): Python сабағының мәтіні мен мысалы.
- [MultiLanguageTaskSeeds.cs](Data/MultiLanguageTaskSeeds.cs): C++/Python-ға ортақ қосымша есептер мен тесттер деректері.
- [DbUpdateErrors.cs](Data/DbUpdateErrors.cs): SQL unique/FK қателерін қолданба деңгейінде ажырату.

**Алдымен:** `ApplicationDbContext.OnModelCreating`. Модельдегі өріс пен базадағы шектеудің айырмасын түсініңіз. Seed — migration емес: біріншісі дерек дайындайды, екіншісі schema нұсқасын басқарады.

## Models/ және Models/Enums/

**Міндеті:** домен мен сақталатын деректер құрылымы. Identity базалық типтері мен validation атрибуттарына тәуелді; EF mapping `Data/` ішінде толықтырылады.

| Топ | Нақты файлдар | Байланыс |
|---|---|---|
| Аккаунт | [ApplicationUser.cs](Models/ApplicationUser.cs), [RoleNames.cs](Models/RoleNames.cs) | Identity пайдаланушысы және Student/Admin тұрақтылары |
| Оқу мазмұны | [Topic.cs](Models/Topic.cs), [Lesson.cs](Models/Lesson.cs), [ProgrammingTask.cs](Models/ProgrammingTask.cs), [TestCase.cs](Models/TestCase.cs) | Topic → Lessons/Tasks; Task → TestCases |
| Орындау | [Runtime.cs](Models/Runtime.cs), [Submission.cs](Models/Submission.cs), [ExecutionResult.cs](Models/ExecutionResult.cs) | User+Task+Runtime → Submission → әр тест нәтижесі |
| Түсіндіру және қорытынды | [AiFeedback.cs](Models/AiFeedback.cs), [Leaderboard.cs](Models/Leaderboard.cs) | Бір submission-ға ең көбі бір AI жазба; бір user-ге бір рейтинг қорытындысы |
| Күйлер | [SubmissionStatus.cs](Models/Enums/SubmissionStatus.cs), [ExecutionStatus.cs](Models/Enums/ExecutionStatus.cs), [Difficulty.cs](Models/Enums/Difficulty.cs) | Ішкі enum мәндері сақталады, UI атауы RESX арқылы беріледі |

`ErrorViewModel` — қате бетіндегі дерек, жеке DB кестесі емес. `RoleNames` те entity емес.

**Алдымен:** `ProgrammingTask`, `Submission`, `ExecutionResult`; кейін олардың DbContext-тағы FK және index анықтамалары. `Runtime` жолындағы command/image метадерегі студентке еркін shell командасын бермейтінін есте сақтаңыз.

## Services/ және Services/Submissions/

**Міндеті:** controller-ден бөлінген қолданба логикасы. Ішкі бумалар міндет бойынша бөлінген; бәрі жеке микросервис емес, бір ASP.NET процесінде орындалады.

| Файл | Міндеті | Тәуелділігі |
|---|---|---|
| [TaskPageService.cs](Services/Submissions/TaskPageService.cs) | Есеп бетін, visible тесттерді, қолдау бар runtime нұсқаларын жинау | DbContext, RunnerLanguage, ViewModels |
| [CustomRunService.cs](Services/Submissions/CustomRunService.cs) | Өз кірісімен уақытша орындау; Submission сақтамайды | Орындау gate-і, Docker runner |
| [SubmissionService.cs](Services/Submissions/SubmissionService.cs) | Pending жазбалар → compile → тесттер → қорытынды сақтау | DbContext, Docker runner, gate, LeaderboardService |
| [SubmissionExecutionGate.cs](Services/Submissions/SubmissionExecutionGate.cs) | Бір web процесіндегі қатар орындалатын жұмыстарды шектеу | `SemaphoreSlim`; Run/Submit ортақ шектеу |
| [AttemptJourneyService.cs](Services/Submissions/AttemptJourneyService.cs) | Өз тарихын және осы runtime-дағы алдыңғы әрекетті табу | DbContext, ViewModels; бөлек Journey кестесі жоқ |

**Алдымен:** `SubmissionService.SubmitAsync` пен `CustomRunService.RunAsync` айырмасы. Екеуі де runner қолданады, бірақ тек Submit ресми тарих пен рейтингке қатысады. Бұл durable background queue емес: HTTP сұрауы қызметтің аяқталуын күтеді.

## Services/CodeExecution/

**Міндеті:** сенімсіз кодты Docker ішінде шектеулі орындау. Docker daemon, жергілікті workspace және бекітілген language анықтамаларына тәуелді.

- [RunnerLanguage.cs](Services/CodeExecution/RunnerLanguage.cs): дәл `cpp`/`python` кілттері, файл атауы, starter, version, image және командалар.
- [CodeRunnerOptions.cs](Services/CodeExecution/CodeRunnerOptions.cs): сенімді C++ image, source/output/compile/job/resource шектері.
- [DockerCodeRunner.cs](Services/CodeExecution/DockerCodeRunner.cs): trusted image тексеру, compile, тест контейнері, output салыстыру, status, cleanup.
- [DockerCli.cs](Services/CodeExecution/DockerCli.cs): `Process.Start` арқылы **Docker CLI** шақыру, аргументтер, bounded stdout/stderr, timeout. Студент executable-і host-та іске қосылмайды.
- [SubmissionWorkspace.cs](Services/CodeExecution/SubmissionWorkspace.cs): кездейсоқ каталог, source жазу, қауіпсіз нысаналы тазалау.
- [CompilationFailureClassifier.cs](Services/CodeExecution/CompilationFailureClassifier.cs): compile қатесінің себебін анықтау.
- [CompileResult.cs](Services/CodeExecution/CompileResult.cs), [TestRunResult.cs](Services/CodeExecution/TestRunResult.cs): орындау қызметінің нәтижелері; DB entity емес.

**Алдымен:** `RunnerLanguage` → `DockerCodeRunner.CompileAsync` → `RunTestAsync` → `DockerCli`. C++ compiled executable береді; Python `py_compile` арқылы syntax тексеріп, әр тестте interpreter іске қосады. Екеуі де Linux контейнерлерінде орындалады.

## Services/AI/

**Міндеті:** аяқталған өз submission-ы үшін шектелген AI түсіндірмесін алу және кеңестерді кезекпен көрсету. DbContext, OpenAI SDK, локализация және ViewModels-ке тәуелді.

| Файл | Міндеті |
|---|---|
| [OpenAiTutorService.cs](Services/AI/OpenAiTutorService.cs) | Ownership, terminal status, existing feedback, DTO дайындау, жауапты тексеру және сақтау |
| [OpenAiFeedbackClient.cs](Services/AI/OpenAiFeedbackClient.cs) | Responses API сұрауы, trusted instructions, strict JSON Schema |
| [AiTutorInput.cs](Services/AI/AiTutorInput.cs) | Модельге берілетін шектелген деректер құрылымы; hidden input/expected жоқ |
| [AiTutorOptions.cs](Services/AI/AiTutorOptions.cs) | Key, model, timeout, token конфигурациясы |
| [AiRequestGate.cs](Services/AI/AiRequestGate.cs) | Бір процесстегі AI concurrency, duplicate request және user cooldown |
| [AiTextSanitizer.cs](Services/AI/AiTextSanitizer.cs) | Белгілі құпия/path үлгілерін алып тастау, мәтін ұзындығын шектеу |
| [AiFeedbackResult.cs](Services/AI/AiFeedbackResult.cs) | Қызметтің success/existing/unavailable тәрізді нәтижесін тасымалдау |
| [StoredHintReader.cs](Services/AI/StoredHintReader.cs) | Сақталған hints JSON-ды шектеулермен оқу |
| [HintRevealService.cs](Services/AI/HintRevealService.cs) | Сервердегі санды шартты түрде бір саты арттыру; OpenAI қайта шақырылмайды |

**Алдымен:** `OpenAiTutorService.AnalyzeAsync` → `AiTutorInput` → `OpenAiFeedbackClient` → `HintRevealService`. API key авторизацияға керек, prompt-қа кірмейді. Structured Outputs пішімді шектейді; кеңестің мағынасына толық кепілдік бермейді.

## Services/Progress/, Leaderboards/, Security/, Localization/

| Файл/бума | Міндеті мен тәуелділігі | Алдымен оқу |
|---|---|---|
| [ProgressService.cs](Services/Progress/ProgressService.cs) | Өз Submissions тарихынан есептер, пайыз, уақыт және rating; EF + ViewModels | Аяқталған күйлер және алғашқы Accepted уақыты |
| [LearningInsightsService.cs](Services/Progress/LearningInsightsService.cs) | Жарияланған есептерден strength және Practice Next; EF + ViewModels | `0.70/0.30`, level реті, unsolved task таңдау |
| [LeaderboardService.cs](Services/Leaderboards/LeaderboardService.cs) | Submission дерегінен сақталатын summary мен рейтинг беті | Бір user/task үшін бір рет ұпай есептеу |
| [LeaderboardUpdateGate.cs](Services/Leaderboards/LeaderboardUpdateGate.cs) | Summary жаңартуды бір процессте кезектестіру | Semaphore-дың бірнеше серверге таралмайтыны |
| [ContentSecurityPolicy.cs](Services/Security/ContentSecurityPolicy.cs) | Бір сұрауға nonce және editor қажеттілігінің белгісі | Nonce-тың script/style-ға берілуі |
| [SecurityHeadersMiddleware.cs](Services/Security/SecurityHeadersMiddleware.cs) | CSP және басқа response headers | `OnStarting`, editor бар беттегі style ерекшелігі |
| [SupportedCultures.cs](Services/Localization/SupportedCultures.cs) | kk-KZ, ru-RU, en-US тізімі мен тексеруі | Әдепкі қазақша мәдениет, cookie provider |

**Алдымен:** Progress пен Learning Map-тың есепке алатын submission жиындары бірдей емес екенін қараңыз: жалпы Progress `InternalError`-ды қосады, Map қоспайды және тек published есептерді алады.

## ViewModels/

**Міндеті:** нақты форма/экранға қажет өрістерді ғана тасымалдау. Controller/service оларды толтырады, Razor оқиды. DB schema жасамайды.

Маңызды файлдар: [SubmitViewModel.cs](ViewModels/Submissions/SubmitViewModel.cs), [TaskDetailsViewModel.cs](ViewModels/Tasks/TaskDetailsViewModel.cs), [SubmissionDetailsViewModel.cs](ViewModels/Submissions/SubmissionDetailsViewModel.cs), [AiFeedbackViewModel.cs](ViewModels/Submissions/AiFeedbackViewModel.cs), [SubmissionComparisonViewModel.cs](ViewModels/Submissions/SubmissionComparisonViewModel.cs), [LearningInsightsViewModel.cs](ViewModels/Progress/LearningInsightsViewModel.cs). `Account/` және `Admin/` ішіндегі form model-дер validation және рұқсат етілген input өрістерін береді.

**Алдымен:** Submit input пен SubmissionDetails output-ты салыстырыңыз. Entity-ді түгел браузерге жіберудің орнына projection/view model пайдалану hidden тесттер мен ашылмаған hints-ті бермеуге көмектеседі.

## Views/ және TagHelpers/

**Міндеті:** Razor арқылы серверде HTML құру. ViewModels, localizer, TagHelpers және жергілікті CSS/JS-ке тәуелді.

- [Views/Shared/_Layout.cshtml](Views/Shared/_Layout.cshtml): навигация, динамикалық `html lang`, nonce, CSS/JS, мәдениет таңдауы.
- [Views/Tasks/Details.cshtml](Views/Tasks/Details.cshtml), [_SubmissionForm.cshtml](Views/Tasks/_SubmissionForm.cshtml): есеп, source textarea, runtime, Run/Submit.
- [Views/Submissions/Details.cshtml](Views/Submissions/Details.cshtml): compile/test нәтижесі, AI, тек ашылған hints.
- [Views/Submissions/Compare.cshtml](Views/Submissions/Compare.cshtml): рұқсат тексерілген екі кодты Monaco Diff-ке беру.
- [Views/Shared/_AttemptJourney.cshtml](Views/Shared/_AttemptJourney.cshtml), [Views/Journeys/Index.cshtml](Views/Journeys/Index.cshtml): қысқа және толық әрекет тарихы.
- [Views/Progress/Index.cshtml](Views/Progress/Index.cshtml), [_LearningMap.cshtml](Views/Progress/_LearningMap.cshtml): статистика мен ұсыныс.
- [Views/Lessons/Details.cshtml](Views/Lessons/Details.cshtml): жай мәтін ретінде қауіпсіз көрсетілетін сабақ пен code example.
- [CspValidationSummaryTagHelper.cs](TagHelpers/CspValidationSummaryTagHelper.cs): validation summary-ді CSP талаптарына сәйкестендіреді.

**Алдымен:** layout → task form → submission details. Razor encoding мен `asp-for`/`asp-action` рөлін түсініңіз; студент source-ын HTML ретінде орындатуға болмайды.

## Areas/Admin/

**Міндеті:** әкімшілік маршруттар, controller және Razor беттері. `[Area("Admin")]`, Admin рөлі, antiforgery, DbContext, Admin ViewModels/RESX-ке тәуелді.

Маңызды файлдар — [TopicsController.cs](Areas/Admin/Controllers/TopicsController.cs), [LessonsController.cs](Areas/Admin/Controllers/LessonsController.cs), [ProgrammingTasksController.cs](Areas/Admin/Controllers/ProgrammingTasksController.cs), [TestCasesController.cs](Areas/Admin/Controllers/TestCasesController.cs). [LeaderboardController.cs](Areas/Admin/Controllers/LeaderboardController.cs) summary-ді қайта есептеу POST-ын басқарады. Views ішінде әр CRUD үшін Index/Create/Edit/Delete және ортақ `_Form` бар.

**Алдымен:** `LessonsController`-дің authorization, Create/Edit mapping және delete логикасы. Сосын Topic/Task/TestCase-ті байланысқан дерек барда өшіру неге шектелгенін оқыңыз. Admin батырмасын жасыру қауіпсіздіктің өзі емес; сервер әр сұрауды тексереді. Runtime CRUD бұл панельде жоқ.

## Resources/

**Міндеті:** үш мәдениеттің UI мәтіні. `SharedResource`, `StudentResource`, `AdminResource` маркер кластары, `IStringLocalizer` және Program localization баптауына тәуелді.

Файл топтары: `SharedResource.*.resx` — ортақ UI/аккаунт; `StudentResource.*.resx` — есеп/нәтиже/AI/статистика; `AdminResource.*.resx` — әкімшілік экрандар. Әр мәдениетте **581 кілт: 133 + 317 + 131**. DB-дағы есеп шарты автоматты аударылмайды.

**Алдымен:** [StudentResource.kk-KZ.resx](Resources/StudentResource.kk-KZ.resx) ішінен `Run_Button` тауып, оның Razor-дағы қолданылуын қараңыз. Кейін `CultureController` → cookie → `RequestLocalizationMiddleware` → localizer жолын қадағалаңыз.

## Migrations/

**Міндеті:** EF моделі мен SQL schema өзгеріс тарихы. `ApplicationDbContext`, model snapshot және SQL Server provider-ге тәуелді.

Қазіргі төрт migration: `20260930082441_InitialCreate`, `20260930185948_AddAiFeedback`, `20261002071740_AddProgressiveHintReveal`, `20261002095510_AddLessons`. `ApplicationDbContextModelSnapshot.cs` соңғы модельді көрсетеді; `.Designer.cs` файлдары migration метадерегі.

**Алдымен:** `AddLessons` ішіндегі `Up/Down`, сосын snapshot-тағы Lesson relation/index. `has-pending-model-changes` модельді snapshot-пен салыстырады; `database update` бар migration-дарды қолданады. Осы оқу тапсырмасында жаңа migration жасалмады.

## ClientScripts/

**Міндеті:** браузер редакторының бастапқы JavaScript коды. [code-editor.js](ClientScripts/code-editor.js) Monaco, DOM form/textarea, server берген runtime option-дар және `sessionStorage`-ға тәуелді.

Бір файл editor/diff құруды, C++/Python тілін, starter мен жеке draft-тарды, textarea синхрондауын, сақтау мүмкін болмаса жадтағы fallback-ты басқарады.

**Алдымен:** тіл таңдау мен form submit кезінде source-тың textarea-ға өтуі. Тілді ауыстыру кодты Submit етпейді. Editor жоқ болса да textarea формасы маңызды қосалқы жол болып қалады.

## wwwroot/

**Міндеті:** браузерге берілетін статикалық файлдар. Razor layout және беттер осы ресурстарды жүктейді; C# service емес.

- [js/custom-run.js](wwwroot/js/custom-run.js): Run POST, antiforgery, timeout, UI күйі, нәтиженің `textContent` арқылы шығуы.
- [js/site.js](wwwroot/js/site.js): жалпы JavaScript файлы.
- [css/site.css](wwwroot/css/site.css): беттер, editor, нәтиже, мобильді орналасу стильдері.
- `js/editor/`: esbuild жасаған editor, worker, тіл chunk-тары, CSS және лицензиялар; бастапқы логика `ClientScripts/` ішінде.
- `lib/`: жергілікті Bootstrap, jQuery және validation ресурстары; CDN міндетті емес.

**Алдымен:** `custom-run.js`, содан кейін `site.css`. Генерацияланған minified bundle-ді жаттаудың қажеті жоқ; оны жасайтын source пен build script-ті оқыңыз.

## scripts/

**Міндеті:** editor жинау, dev admin дайындау және тексеру құралдары. Кейбірі Node/Monaco-ға, кейбірі Python, іске қосылған HTTPS сайт, SQL Server немесе Docker-ге тәуелді.

| Файл/топ | Не үшін керек? |
|---|---|
| [build-editor.mjs](scripts/build-editor.mjs), [monaco-csp.mjs](scripts/monaco-csp.mjs) | Жергілікті bundle/worker, Monaco style nonce бейімдеуі |
| [verify_editor_csp.mjs](scripts/verify_editor_csp.mjs) | Editor жинағының CSP-ге қатысты статикалық тексеруі |
| [verify_resources.py](scripts/verify_resources.py) | Үш тілдегі resource кілттері мен аударма құрылымын тексеру |
| `verify_phase2.py` … `verify_phase5.py` | Аккаунт, runner, AI және кейінгі негізгі кезең тексерулері |
| `verify_enhancement1.py` … `verify_enhancement5b.py` | Run, Diff/Journey, hints, localization, Lessons, Python кеңейтулерінің тексерулері |
| [verify_enhancement2_ui.mjs](scripts/verify_enhancement2_ui.mjs) | Journey/Diff үшін браузер тексеруі |
| [verify_phase5_live.py](scripts/verify_phase5_live.py) | Арнайы live AI сценарийі; `--live` ақылы сұрау жасауы мүмкін |
| [Set-DevelopmentAdmin.ps1](scripts/Set-DevelopmentAdmin.ps1) | Development admin конфигурациясын дайындау |

**Алдымен:** `verify_resources.py`, кейін қажет feature-дің verifier-і. Integration script-тер fixture деректерін құруы/тазалауы мүмкін; оларды тек оқыған кезде автоматты іске қосу қажет емес. Осы құжат дайындауда live AI script орындалмады.

## tests/

**Міндеті:** жеке C# console check жобалары; негізгі MVC `.csproj` оларды өз компиляциясына қоспайды. Әрқайсысының `.csproj` және `Program.cs` файлы бар.

- [Phase4Checks/Program.cs](tests/Phase4Checks/Program.cs): AI қызметінің сценарийлері мен жалған client арқылы тексеру.
- [Enhancement1Checks/Program.cs](tests/Enhancement1Checks/Program.cs): Custom Run/орындау тексерулері.
- [Enhancement3Checks/Program.cs](tests/Enhancement3Checks/Program.cs): progressive hints және оған қатысты қызмет тексерулері.
- [Enhancement5bChecks/Program.cs](tests/Enhancement5bChecks/Program.cs): language registry, нақты Docker Python syntax/оқшаулау/output және ақылы емес AI сценарийлері.

**Алдымен:** `Enhancement5bChecks.RunAsync` шақыратын әдістерді қараңыз. Бұлар түгел тек жадтағы unit test емес: кейбірі SQL/Docker қажет етеді. «AI client жалған» деген «барлық сыртқы тәуелділік жалған» деген сөз емес.

## Құжаттар және build нәтижелері

`README.md`, `study-phase*.md`, `study-enhancement*.md` даму тарихын түсіндіреді. Кейбір ерте мәтіндерде Python runner жоқ, Diff тек C++, resource саны 526/579 деп жазылған; бүгінгі мінез-құлықты source анықтайды. Нақты айырмалар негізгі нұсқаулықтың соңында берілген. `study-phase5.md` қосымшасында бұрыннан бұзылған мәтін де бар.

`bin/`, `obj/`, `node_modules/` — дайындалатын артефактілер мен орнатылған тәуелділіктер. Мысалы, IDE-дегі `obj/*.nuget.dgspec.json` жобаның NuGet restore нәтижесі, қолмен өзгертілетін негізгі source емес.

## Жобаны оқу реті

1. **Program.cs + csproj + launchSettings:** процесс қалай басталады, қандай қызмет тіркелген, қай порт ашылады?
2. **Models/Enums → Models:** Task, TestCase, Submission және ExecutionResult нені білдіреді?
3. **ApplicationDbContext → migrations:** байланыс, index, delete ережесі қайда бекітілген?
4. **AccountController → Identity:** user, роль, cookie және жеке дерек рұқсаты қалай жұмыс істейді?
5. **TasksController → TaskPageService → TaskDetailsViewModel → task View:** бір қарапайым GET-ті толық қадағалаңыз.
6. **ClientScripts/code-editor.js → _SubmissionForm → custom-run.js:** браузерден source пен runtime қалай шығады?
7. **SubmissionsController → SubmissionService және CustomRunService:** Run мен Submit-тің айырмасын өз сөзіңізбен айтыңыз.
8. **RunnerLanguage → DockerCodeRunner → DockerCli → SubmissionWorkspace:** сенім шекарасы, екі тіл, шектеу және cleanup.
9. **SubmissionDetails → OpenAiTutorService → OpenAiFeedbackClient → HintRevealService:** judge нәтижесінен tutor кеңесіне дейін.
10. **AttemptJourneyService → Compare action → Compare View:** ownership, алдыңғы runtime және mixed-language diff.
11. **ProgressService → LearningInsightsService → LeaderboardService:** үш формуланы және есепке кіретін деректерді салыстырыңыз.
12. **Lesson → LessonsController → Admin/LessonsController:** жариялау, кезек, байланысты есеп және CRUD.
13. **Resources → CultureController → layout → Security middleware:** тіл, nonce, encoding және antiforgery.
14. **scripts/tests → DEFENSE-PREP:** талаптың қалай тексерілгенін түсініп, 60 сұрақ пен демоны дауыстап қайталаңыз.
