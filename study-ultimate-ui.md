# Ultimate UI/UX — интерфейс архитектурасы

Бұл жаңарту IntelligentProgrammingPlatform / «Зияткерлік» жобасының бар мүмкіндіктерін жаңа визуалды жүйеге біріктіреді. Жаңа оқу, жетістік, онлайн мәртебе немесе хабар алмасу механизмі қосылған жоқ.

## 1. UI/UX redesign-де не өзгерді?

Беттер енді міндетіне қарай орналасады: басты бет — ықшам dashboard, сабақ — оқуға ыңғайлы мақала, есеп — кең редактор ортасы, хаттар — екі бағанды чат. Қонаққа платформа таныстырылады; кірген пайдаланушы өз көрсеткіштері мен келесі әрекеттерін көреді. Әр бөлімді бірдей үлкен карточкаға салу азайтылды.

Басты бетте Level/XP, шешілген есеп, ұпай, сәттілік пайызы, сабақтарға өту, бар алгоритм ұсынған келесі есеп және соңғы үш әрекет бар. Жеке статистика қайта есептелетін жаңа формула енгізілмеді.

## 2. Design System деген не?

Design System — түстер, мәтін өлшемдері, аралықтар, компоненттер және олардың әрекеттері туралы ортақ келісім. Бір батырма бір бетте үлкен, басқа бетте себепсіз кішкентай болмауы керек. Қолданбадағы негізгі, қосымша, бейтарап және қауіпті әрекеттер ортақ стильдерге сүйенеді.

CSS жүктеу реті анық:

```text
Bootstrap (жергілікті базалық grid/utilities)
  → site.css       — токендер және құжат негізі
  → layout.css     — navbar, dropdown, контейнер, Admin навигациясы
  → components.css — формалар, батырмалар, кестелер, мәртебелер
  → learning.css   — Home, сабақтар, XP, профиль, heatmap
  → workspace.css  — есеп, console, жіберілім, AI, journey, diff
  → social.css     — достар, ашық профиль, хаттар
```

Қосымша CSS build жүйесі енгізілмеді. `_Layout.cshtml` файлдарды `asp-append-version` арқылы тікелей жүктейді. Қате бетінің тәуелсіз `_ErrorLayout.cshtml` көрінісі тек негіз, layout және компоненттерді жүктейді; қате кезінде дерекқорға тәуелді navbar шақырылмайды.

## 3. CSS variables не үшін керек?

Токендер `wwwroot/css/site.css` ішіндегі `:root` бөлімінде сақталады. Қалған CSS файлдары сол айнымалыларды қолданады.

| Мақсаты | Токен | Мәні |
|---|---|---|
| Негізгі фон | `--bg-main` | `#0c111b` |
| Бет панелі | `--bg-surface` | `#131b28` |
| Dropdown/hover | `--bg-elevated` | `#1c2738` |
| Жұқа жиек | `--border-subtle` | `#283448` |
| Негізгі мәтін | `--text-primary` | `#edf1fa` |
| Қосымша мәтін | `--text-secondary` | `#b8c3d7` |
| Көмекші мәтін | `--text-muted` | `#98a8c0` |
| Негізгі акцент | `--accent` | `#ab9bff` |
| Сәттілік | `--success` | `#75dfb4` |
| Ескерту | `--warning` | `#f2ce80` |
| Қате | `--danger` | `#ffa3b7` |

Radius шкаласы — 8/12/16px, transition — 160ms. Аралықтар `.25rem`–`3rem` токендерінен құралады. Негізгі контейнер 1400px-пен шектелген. Редактор бетінде бұл шектеу алынады. Түстерді бөлек компоненттерге кездейсоқ hex мәндерімен көшірмеу керек.

Интерфейс жүйелік Segoe UI/system stack қолданады. Код, runtime және техникалық дерек үшін Cascadia Code/Consolas/monospace бар. Қаріптер сырттан жүктелмейді.

## 4. Navbar қалай өзгерді?

Сол жақта бұрынғы `</>` белгісі мен «Зияткерлік» бренді қалды. Негізгі оқу сілтемелері бір топта. «Қауымдастық» ішінде достар, хаттар және бұрынғы unread badges бар. Оң жақта avatar/DisplayName dropdown орналасады: профиль, таңдаулылар, Admin рөлі бар адамға Admin, соңында POST logout.

Белсенді сілтеме тек мәтін түсімен емес, фон және жиекпен ерекшеленеді. Ұзын DisplayName navbar-ды созбай қысқарады; профильдегі толық аты өзгермейді.

Dropdown үшін native `<details>/<summary>` қолданылған. Жергілікті `site.js` бір мәзір ашылғанда қалғанын жабады; Escape фокусты summary-ге қайтарады; ArrowUp/ArrowDown/Home/End элементтер арасында өтеді; сыртқа басқанда немесе фокус кеткенде мәзір жабылады. Tab қалыпты құжат тәртібін сақтайды.

## 5. Language switcher қалай түзетілді?

Тіл атауы мен globe SVG бір 40px control ішінде. Мобильде control 44px болады. Үш нұсқаның әрқайсысы — **сол POST формасындағы submit button**:

```text
Қазақша / Русский / English
  → POST /Culture/Set
  → __RequestVerificationToken
  → қолданыстағы culture allowlist
  → қолданыстағы local returnUrl тексеруі
  → culture cookie және бастапқы бетке қайту
```

Бөлек «Ауыстыру» батырмасы да, insecure GET ауыстыруы да жоқ. Бұл әрекет JavaScript-ке тәуелді емес. Қазіргі тіл `aria-current` және белгімен көрсетіледі. Тілдердің төл атаулары аударылмайды.

## 6. Responsive design қалай жасалды?

| Ені | Негізгі бейімделу |
|---|---|
| 1440 | Бір қатар navbar, төрт көрсеткіш, үш баған сабақтар, 40/60 есеп workspace |
| 1024 | Ашылатын топталған мәзір, есеп панельдері бірінің астында, екі баған каталог, 300px чат sidebar |
| 390 | Екі бағанды негізгі mobile nav, ықшам бренд, бір баған контент, чаттың таңдалған әңгімесі және «Артқа» сілтемесі |

Қалыпты беттер экранды толық жайламайды. Кесте, source code, heatmap және Monaco өз контейнерінің ішінде көлденең айнала алады. Бұл бүкіл беттің көлденең overflow жасауына себеп болмауы керек; нақты геометрия браузер checklist-інде тексеріледі.

## 7. Task Workspace қалай жақсарды?

Desktop-та сол жақта breadcrumb, есеп атауы, difficulty/XP/favorite, шектеулер, шарт және мысалдар бар. «Шарт/Мысалдар» жергілікті anchor сілтемелері арқылы ауысады. Сол панель үлкен экранда өз ішінде айналады.

Оң жақ бір IDE панелі ретінде жасалған: runtime/draft/reset toolbar, Monaco, console tabs және әрекеттер қатары. Editor биіктігі desktop-та 60vh, кіші экрандарда 56/52vh; minimum 360/320px. Run — қосымша, Submit — акцент батырмасы. Екі батырманың өлшемі бірдей. Бұрынғы Ctrl/Cmd+Enter және Ctrl/Cmd+Shift+Enter shortcut-тары сақталған.

`workspace.js` тек Custom Input/Run Result көрінісін ауыстырады. Network сұрауын жібермейді, source code-ты өңдемейді, busy/authorization жағдайын анықтамайды. Нәтижені бұрынғы `custom-run.js` алады. Алғашқы және қайталанған Run нәтиже tab-ын ашады; runtime ауысып нәтиже жасырылғанда input tab қайтады. JavaScript істемесе textarea мен бұрынғы форма қалады.

## 8. Monaco қалай сақталды?

`ClientScripts/code-editor.js`, build plugin, worker және compiled editor bundle өзгертілмеді. CSS тек `.code-editor`, `.code-diff`, `.source-fallback` сыртқы контейнерлерін безендіреді. `.monaco-editor` ішкі DOM элементтеріне жаңа global selector жазылған жоқ.

Draft, C++/Python модель ауыстыруы, textarea sync, reset confirmation, read-only diff және duplicate-submit guard сол күйінде. Reduced motion режимінде Custom Run-ның smooth scroll-ы `auto` болады; оның сұрауы мен нәтижені textContent арқылы көрсетуі өзгермеген.

## 9. Chat UI қалай өзгерді?

Desktop sidebar — 320px, laptop — 300px. Белсенді әңгіме фон/жиекпен ерекшеленеді; avatar, аты, соңғы мәтін, уақыт және unread badge сақталған. Header-де профильге анық сілтеме бар. Online presence ойдан қосылған жоқ: бар connection мәтіні тек realtime байланысының күйін білдіреді.

Хабарламалар шектелген ені бар bubble ретінде, өзінің хаты акцент фонмен көрсетіледі. Composer төменде, мәтіннің 2000 таңба шегі мен бұрынғы Enter/Shift+Enter ережелері сақталған. Мобильдегі textarea 16px қаріп және safe-area padding қолданады.

Хаттар бұрынғы MVC POST арқылы SQL Server-ге сақталады. SignalR тек жаңарту туралы хабарлайды. Hub арқылы жаңа send жолы, жаңа presence немесе қосымша chat іздеуі енгізілмеді.

## 10. Profile қалай өзгерді?

Жеке және ашық профильде бірдей avatar/identity стилі, акцент жиегі және анық статистика бар. Жеке профиль hero-сына бар Level қосылды. XP, heatmap және соңғы әрекеттердің деректері өзгермеді. Ашық профильге email, private code, favorites немесе AI feedback қосылған жоқ.

Friends карточкалары ықшамдалды; incoming/outgoing/search/blocked бөлімдері сақталды. Бос тізімнен пайдаланушы іздеуге CTA бар. Жаңа achievement жүйесі жасалған жоқ.

## 11. Leaderboard қалай өзгерді?

Кесте жіңішке бөлгіштер, hover, avatar және анық ұпай типографиясын қолданады. Алғашқы үш орын тыныш алтын/күміс/қола түстерімен ерекшеленеді. Ағымдағы пайдаланушы қатарындағы белгі мен сол жақ сызық сақталды. Ұпай, сұрыптау, pagination және success rate өзгерген жоқ.

## 12. Admin дизайн қалай біріздендірілді?

Admin top navigation айқын контекст береді. Users/Topics/Lessons/Tasks/TestCases басқару карталары, кестелер, edit/create/delete формалары ортақ токендерді алады. Бар роль тексерістері сол күйінде. Жаңа санақ есептеу сервисі қосылмағандықтан dashboard-та жалған сандық көрсеткіштер көрсетілмейді; бар бөлімдерге өту карталары қолданылады.

## 13. Accessibility қалай тексерілді?

Skip link, `focus-visible`, semantic label, table header scope, `aria-current`, progress label және chat `role="log"` сақталады. Dropdown-дар native disclosure, ал editor console tab-тары roving tabindex және Arrow/Home/End басқаруын пайдаланады. Submit батырмалары Enter арқылы да жұмыс істейді. Жаңа мәтін HTML ретінде интерпретацияланбайды.

`verify_ultimate_ui.mjs` нақты frontend script-терін DOM doubles-пен тексереді: mobile toggle, Escape фокусын қайтару, dropdown шектері, сыртқа басу, console result/reset/repeated Run және BFcache. Он мәтін/фон жұбының есептелген contrast ratio-сы 4.5:1-ден төмен емес. Бұл тексеру нақты экрандағы толық accessibility аудитінің орнына жүрмейді.

## 14. CSP қалай сақталды?

`ContentSecurityPolicy`, `SecurityHeadersMiddleware` және Program өзгермеді. Жаңа script-тер жергілікті файлдар; inline onclick/style, CDN немесе unsafe-eval қосылмады. Navbar JS тек class/attribute ауыстырады, Popper inline positioning қажет емес. Monaco беттерінің бұрыннан шектелген style рұқсаты мен request nonce сол күйінде.

Ескі CSP тексерістері әр жауаптағы nonce-ты, script/style директиваларын және authored inline event/style жоқтығын тексереді. Жаңа HTTP тексерісі негізгі беттердің CSS жүктеу ретін және нақты served asset мазмұнын да тексереді.

## 15. Localization қалай сақталды?

Алты жаңа `Ui_*` кілті SharedResource-тың үш нұсқасына қосылды. Қазір әр тілде 760 кілт бар: Shared 292, Student 319, Admin 149. Placeholder parity тексерісі өтеді. KZ/RU/EN Razor жауаптары тексеріледі. Source code, user message, DisplayName және дерекқордағы сабақ/есеп мәтіні автоматты аударылған жоқ.

## 16. Mobile UI қалай бейімделді?

Navbar үлкен бір тізімге айналмайды: оқу сілтемелері екі баған, қауымдастық бөлек disclosure, аккаунт/тіл controls жеке utility қатары. JavaScript жоқ кезде негізгі nav қолжетімді болып қалады. Ұзын аттар қысқарады, карточка мәтіні тасымалданады, таблицалар тек өз контейнерінде айналады.

390×844 нақты телефон өлшемі, виртуалды keyboard, scroll/focus және menu шекаралары [manual-ultimate-ui-checklist.md](manual-ultimate-ui-checklist.md) бойынша қолмен тексерілуі тиіс.

## 17. Не үшін backend/database өзгертілмеді?

Бұл — көрініс қабатының жаңартуы. Тек Home/Progress controller-лері бар read-only қызметтерден дайын көрсеткіштерді алып, шағын view-model өрістерін толтырады. Home бұрынғы `ProgressService.GetAsync`, `ExperienceService`, `LearningInsightsService` нәтижелерін пайдаланады; есептеу формулалары сол қызметтерде өзгеріссіз қалды. Жеке dashboard қонаққа көрсетілмейді.

`Services`, `Models`, `Data`, `Migrations`, `Program.cs`, authentication, Docker, AI және social authorization файлдары өзгермеген. Жаңа migration жоқ. `has-pending-model-changes` — өзгеріс жоқ; `database update` — database current.

## Тексеру дәлелі және шектеуі

Бастапқы build: 0 error / 0 warning. Бұрын іске қосылған қолданбаның apphost файлы бұғаттауын жойғаннан кейін таза build орындалды. Бастапқы resource/editor/social logic және Phase 2 HTTP/security тексерістері өтті.

**27 verification suite және 7 test project build өтті.** Толық нәтижелер `obj/ultimate-ui-final-results.json` файлына жазылды; әр suite үшін жеке log бар. Docker бастапқыда өшірулі болған; орнатылған Desktop іске қосылған соң барлық runner тексерістері өтті. Соңғы қолданба build-і де 0 error / 0 warning берді (`obj/ultimate-ui-build-final.log`).

| Топ | Өткен тексерістер |
|---|---|
| Frontend | resources, editor-csp, editor-ui, social-ui, ultimate-ui behavior/contrast |
| HTTP/SQL/Docker | phase2, phase3, phase4, phase5, enhancement1, enhancement2, enhancement3, enhancement4, enhancement5a, enhancement5b, extra_tasks, enhancement6, enhancement7, enhancement8, ultimate_ui |
| Service/SQL | Phase4Checks, Enhancement1Checks, Enhancement3Checks, Enhancement5bChecks, Enhancement6Checks, Enhancement7Checks, Enhancement8Checks |

Enhancement 8 HTTP suite нақты үш WebSocket клиентімен SignalR жеткізілуін тексерді; бұл браузердің визуалды тексерісі емес. Ақылы OpenAI сұраулары: **0**. Existing suites-тің assertion-дары дизайнға бейімдеу үшін әлсіретілген жоқ.

**Осы redesign сессиясында нақты браузер және screenshot қолжетімсіз болды.** `cua` inventory бос, IAB `Browser is not available: iab` қатесін, native helper `native pipe is unavailable` қатесін қайтарды. Сондықтан 1440×900, 1024×768, 390×844 үшін нақты visual pass жарияланбайды. Алдыңғы enhancement кезіндегі браузер дәлелі жаңа дизайнға дәлел ретінде қолданылмайды.

Ақылы AI режимдері іске қосылмайды. Бұрынғы `verify_phase5_live.py` / `--live-review` ақылы тексерістері және тарихи migration-ды қайта қолдануды талап ететін `verify_enhancement3_migration.py --prepare` бұл UI регрессиясына кірмейді. Қалыпты offline/service/HTTP режимдері қолданылады; schema-ны downgrade жасау қажет емес.

Қайта тексеру командалары:

```powershell
dotnet build
dotnet ef migrations has-pending-model-changes
dotnet ef database update
python scripts/verify_resources.py
node scripts/verify_ultimate_ui.mjs
python scripts/verify_ultimate_ui.py
git diff --check
```

HTTP suite үшін Development HTTPS қолданбасы `https://localhost:7115` мекенжайында іске қосулы болуы керек. Қалған қолданыстағы suite командалары README және enhancement нұсқаулықтарында сақталған. Тесттердегі credentials тек жадта оқылады; логтарға шығарылмайды.

## Файлдар картасы

Жаңа файлдар:

- `wwwroot/css/layout.css`, `components.css`, `learning.css`, `workspace.css` — модульдік дизайн.
- `wwwroot/js/workspace.js` — presentation console tabs.
- `scripts/verify_ultimate_ui.mjs`, `scripts/verify_ultimate_ui.py` — UI behavior/contrast және нақты HTTP тексерісі.
- `study-ultimate-ui.md`, `manual-ultimate-ui-checklist.md` — қазақша түсіндірме мен visual checklist.

Өзгерген файлдар:

- `wwwroot/css/site.css`, `social.css`; `wwwroot/js/site.js`, `custom-run.js`.
- `Views/Shared/_Layout.cshtml`, `_Layout.cshtml.css`, `_ErrorLayout.cshtml`, `_LoginPartial.cshtml`, `_CultureSelector.cshtml`, `_ProfileOverview.cshtml`, `Components/SocialNavigation/Default.cshtml`.
- `Views/Home/Index.cshtml`, `Lessons/Index.cshtml`, `Tasks/Index.cshtml`, `Tasks/Details.cshtml`, `Tasks/_SubmissionForm.cshtml`.
- `Views/Submissions/Details.cshtml`, `Submissions/Compare.cshtml`, `Progress/Index.cshtml`, `Leaderboard/Index.cshtml`, `Friends/Index.cshtml`, `Messages/Index.cshtml`.
- `Controllers/HomeController.cs`, `Controllers/ProgressController.cs`; `ViewModels/Home/HomeViewModel.cs`, `ViewModels/Progress/ProgressViewModel.cs` — бар көрсеткіштерді көрсетуге арналған композиция.
- Үш `Resources/SharedResource.*.resx` және `README.md`.

Lesson Details, AI Review, Login/Register, Favorites, Admin және басқа бар Razor беттері shared CSS арқылы жаңарды; олардың әрқайсысының markup файлын өзгерту міндетті болған жоқ.
