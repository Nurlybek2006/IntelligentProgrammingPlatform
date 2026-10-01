# Enhancement 2 — оқу жолы, Custom Run және Code Diff

Бұл өзгеріс Phase 1–5 пен Enhancement 1 негізінде жасалды. Қолданыстағы Identity, SQL Server, Docker judge, AI Tutor, Progress, Leaderboard, Admin және CSP сақталды. Жаңа дерекқор кестесі, бағаны немесе migration қажет болған жоқ.

## 1. Enhancement 2-де не жасадық?

Есеп бетіне өз input-ымен уақытша Run, ресми әрекеттердің Attempt Journey тізбегі және екі әрекет кодын салыстыратын Monaco Diff Editor қосылды. Студент кодын тексеріп, ресми жіберілім жасап, AI кеңесін оқып, келесі өзгерісін алдыңғы нұсқамен салыстыра алады. Жаңа архитектура немесе бөлек орындау сервері жасалған жоқ.

```text
Task Page
   |
   +---- Run (authenticated POST + antiforgery)
   |       |
   |       v
   |   CustomRunService
   |       |
   |       v
   |   Docker Sandbox ---- temporary JSON result
   |
   +---- Submit
           |
           v
       Submission
           |
           v
      Attempt Journey
           |
           +---- AI Hint (saved feedback exists)
           |
           +---- Compare with Previous
                     |
                     v
               Monaco Diff Editor
```

Run мен Submit бір `SubmissionExecutionGate` және бір `DockerCodeRunner` қолданады. Төменгі sandbox жолы ортақ; тек Submit базаға ресми нәтиже жазады.

## 2. Custom Run деген не?

Custom Run — ағымдағы редактор кодын студент енгізген input-пен уақытша орындау. Мысалы, екі сан қосатын бағдарламаға `2 3` берсек, stdout-та `5` шығады. Сервер нәтиженің дұрыстығын hidden немесе visible тестпен салыстырмайды. Бағдарлама сәтті аяқталса, күйі `Success`; бұл ресми `Accepted` деген сөз емес.

`CustomRunsController.Run` POST әрекеті аутентификацияны, antiforgery token-ді, жарияланған есепті және enabled C++ runtime-ды тексереді. `CustomRunViewModel` тек task ID, runtime ID, source және custom input қабылдайды. Browser жіберген UserId, expected output, status немесе execution time нәтиженің иесін не бағасын өзгерте алмайды.

## 3. Run пен Submit айырмашылығы қандай?

| Run | Submit |
|---|---|
| Студенттің custom input-ын қолданады | Есептің ресми тесттерін қолданады |
| Expected output-пен салыстырмайды | Expected output-пен салыстырады |
| Беттен шықпай JSON нәтижесін көрсетеді | Submission Details бетіне өткізеді |
| Тарихқа әрекет қоспайды | Submission және ExecutionResult сақтайды |
| Прогресс пен рейтингке әсер етпейді | Аяқталған нәтиже статистикаға қатысады |
| AI талдауын шақырмайды | Сақталған әрекетке AI-ды жеке батырмамен сұрауға болады |

Run орындалып жатқанда Run және Submit батырмалары уақытша өшеді. Нәтиже, желі қатесі немесе үзілістен кейін қайта қолжетімді болады. Редактордағы соңғы мәтін сұрау жіберілмей тұрып hidden textarea-мен синхрондалады.

## 4. Custom Run неге database-ке сақталмайды?

Бұл еркін тәжірибе жасауға арналған уақытша әрекет. Әр шағын input сынағын ресми әрекетке айналдыру статистиканы бұрмалап, негізгі оқу жолын қажетсіз жазбалармен толтырар еді.

`CustomRunService` конструкторында DbContext, LeaderboardService немесе AI service жоқ. Контроллер task/runtime деректерін `AsNoTracking()` және шағын projection арқылы ғана оқиды. Run жолында `SaveChanges`, Submission немесе ExecutionResult жасау жоқ. DTO enum-ы ViewModels ішінде орналасқан, EF моделі емес.

## 5. Custom input қалай STDIN арқылы беріледі?

Source `SubmissionWorkspace.WriteSourceAsync` арқылы UTF-8 файлға жазылады. Input Docker CLI процесінің STDIN ағынына беріледі, ол контейнер бағдарламасына жалғанады. Input командалық аргументке немесе shell жолына біріктірілмейді. STDOUT пен STDERR бөлек оқылады, нәтиже JSON арқылы қайтады.

Source ең көбі 64 KiB, input ең көбі 32 KiB UTF-8 байт болуы керек. `StringLength` қана жеткіліксіз: көпбайтты әріптер де серверде `Encoding.UTF8.GetByteCount` арқылы тексеріледі. Бос custom input рұқсат етіледі. HTTP body-де бөлек 512 KiB шек бар.

## 6. Custom Run Docker security қалай сақтайды?

`CompileAsync` ортақ қалды. `RunTestAsync` пен `RunCustomAsync` бір private `RunProgramAsync` арқылы орындалады; output салыстыру тек judge wrapper-де жасалады. Контейнер жасау, mount тексеру, STDIN, timeout және cleanup логикасы көшіріліп қайталанған жоқ.

Бекітілген image өзгермеді:

```text
gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
```

Network none, non-root 65534, cap-drop ALL, no-new-privileges, read-only root, PID 64, CPU, memory/swap тең шегі, bounded stdout/stderr және қауіпсіз tmpfs сақталған. Тек GUID workspace бумалары mount болады; Docker socket, жоба, root немесе home mount жоқ. Host-та студент коды немесе shell орындалмайды.

Task лимиттері бұрынғыдай time 100–30000 ms және memory 16–1024 MB аралығына clamp жасалады. Компиляция шегі 15 s / 512 MB болып қалды. Custom Run-ның жалпы deadline-ы 90 s, кезек күтуі 10 s. Run және Submit бірге ең көбі екі орындау орнын пайдаланады. Бұл бір процесс ішіндегі шек; жаңа distributed rate limiter қосылған жоқ.

Инфрақұрылым қатесі студентке жалпы unavailable хабарын береді, техникалық exception сервер журналында қалады. Workspace пен контейнер cancellation кезінде де finally арқылы тазаланады. Custom Run сәтсіз бағдарламаның тазартылған stderr мәтінін де көрсете алады; judge-дің бұрынғы failure diagnostics тәртібі сақталған.

## 7. Attempt Journey деген не?

Attempt Journey — бір студенттің бір есеп бойынша ресми жіберілімдер тізбегі. Оның дереккөзі бұрынғы `Submissions` және `AiFeedbacks` кестелері; жаңа timeline кестесі жоқ.

Есеп бетінде соңғы бес әрекет, `/Tasks/{slug}/Journey` бетінде жиырмадан беттелген толық жол бар. Әр элемент status, passed/total, UTC CreatedAt, test execution time, Accepted белгісі және feedback бар-жоғын көрсетеді. Әрекетті басқанда бұрынғы Submission Details ашылады. Жариялаудан алынған есептің тарихы тек өз әрекеті бар пайдаланушыға сақталады.

## 8. Attempt number қалай анықталады?

Сұрау `CreatedAt ASC`, содан кейін `Id ASC` бойынша реттеледі. Әрекет нөмірі осы реттегі орнынан есептеледі; жаңа DB бағанына жазылмайды. Бірдей timestamp кезінде Id тұрақты ретті береді. Compact preview-де де толық тарихтағы нөмір сақталады: мысалы, 25 әрекет болса, 21–25 көрсетіледі.

Әр бетке қажет metadata ғана алынады. Алдыңғы әрекет сілтемесін жасау үшін offset алдында бір қосымша metadata жолы оқылады. Сондықтан екінші беттің бірінші әрекеті бірінші беттің соңғы әрекетімен салыстырыла алады. Бет нөмірі жарамды аралыққа clamp жасалады.

## 9. AiFeedback timeline-да қалай көрсетіледі?

SQL projection feedback байланысы мен оның owner мәнін тексеріп, тек boolean қайтарады. Feedback бар әрекетке **AI Hint Used** белгісі қойылады. Оның мағынасы — сол әрекетке кеңес сақталған. Бұл кеңес міндетті түрде оқылды немесе кейінгі Accepted-ке себеп болды деген қорытынды жасалмайды.

Model атауы, token usage, prompt, AI ішкі metadata немесе hidden test мазмұны timeline-ға берілмейді. Timeline ашылғанда жаңа AI сұрауы жіберілмейді. Тестте белгі нақты сақталған, анық synthetic деп белгіленген feedback fixture арқылы тексерілді; ақылы модель шақырылған жоқ.

## 10. Code Diff деген не?

Code Diff екі ресми әрекеттің source code мәтіндеріндегі өзгерістерді көрсетеді. Ол студентке syntax қатесін түзету, output форматын өзгерту немесе алгоритм қадамын қосу сияқты нақты код айырмасын көруге көмектеседі. Өзгеріс пайдалы болды деген автоматты бағалау жасалмайды.

Маршрут: `/Submissions/Compare?olderId=123&newerId=456`. Ол аутентификацияны талап етеді. Жеке күрделі diff алгоритмі жасалған жоқ.

## 11. Monaco Diff Editor қалай жұмыс істейді?

Қолданыстағы жергілікті Monaco bundle `createDiffEditor` және екі C++ model қолданады. Theme — бұрынғы `vs-dark`. Екі жағы да read-only (`readOnly=true`, `originalEditable=false`); көрсету сақталған source-ты өзгертпейді.

Кең экранда side-by-side view бар. Редактор ені 800 px-тан төмен болса, Monaco-ның `useInlineViewWhenSpaceIsLimited` параметрі inline view-ге ауыстырады. `automaticLayout` өлшем өзгерісіне жауап береді. Diff есептеуіне 5000 ms шек қойылды. JavaScript іске қосылмаса, read-only textarea fallback қалады. Бұл параметрлер техникалық тексерілді; нақты көрінісін браузерде растау керек.

## 12. Previous және Current attempt қалай таңдалады?

Journey-дегі **Compare with previous** осы пайдаланушының осы есеп бойынша тікелей алдыңғы ресми әрекетін және ағымдағы әрекетін таңдайды. Run бұл қатарға кірмейді. Бірінші әрекетте салыстыру сілтемесі болмайды.

Compare action иесінің екі ID-сін SQL-да шектеп оқиды және нәтижені CreatedAt/Id бойынша қайта реттейді. Query string-дегі ID орындары ауыстырылса да Previous — хронологиялық ертерек, Current — кейінгі әрекет. Бір ID-ні екі рет беру 404 қайтарады. Бетте екі status, UTC уақыт және execution time бар; journey/result беттеріне қайту сілтемелері берілген.

## 13. Неге екі submission бір task-қа тиесілі болуы керек?

Бұл интерфейс бір есептің шешімін жетілдіру жолын түсіндіреді. Басқа есептің коды басқа input/output талаптарына бағынады; оны «алдыңғы әрекет» деп көрсету жаңылыстырады. Сондықтан controller екі `ProgrammingTaskId` тең екенін тексереді. Бір owner болса да, әртүрлі task 404 алады.

## 14. Ownership authorization не үшін маңызды?

SourceCode — пайдаланушының жеке жұмысы. URL-де ID-ді өзгерту басқа студенттің кодын ашпауы тиіс. Journey UserId-ді query string-нен емес, Identity claim-нен алады. Compare SQL сүзгісінде екі submission үшін де сол owner талап етіледі. Біреуі бөтен немесе жоқ болса, жалпы 404 шығады.

Admin рөліне арнайы айналып өту жолы қосылған жоқ. `[Authorize]` жалғыз өзі жеткіліксіз: ол тек кіргенін тексереді, ал нақты дерек иесі SQL сүзгісімен тексеріледі. Жеке беттер ортақ cache-те сақталмайды.

## 15. SourceCode XSS-тан қалай қорғалды?

Source Razor textarea tag helper арқылы HTML-encode етіледі. External JS тек `textarea.value` оқып, Monaco model жасайды. Source script блогына біріктірілмейді және `innerHTML` арқылы салынбайды. Textarea helper бастапқы newline-ды браузердің өңдеуінен де дұрыс қорғайды.

Custom stdout, stderr және compiler output әрқашан `textContent` арқылы көрсетіледі. `<img onerror=...>`, `</script>` және `</textarea>` мәтіндері HTML ретінде орындалмайды. HTTP тест encoded markup пен қайта оқылған source мәнін, JS integration doubles қауіпті HTML sink қолданылмайтынын тексерді.

## 16. CSP Diff Editor-де қалай сақталды?

Compare view бұрынғы `ContentSecurityPolicy.EnableMonacoStyles()` механизмін ғана қолданады. Бұл сол жауапқа Monaco қажет ететін style-attribute рұқсатын береді. Full Journey сияқты қарапайым беттер `style-src-attr 'none'` күйінде қалады. CSP policy, nonce генераторы мен header middleware өзгермеді.

Diff қолданыстағы nonce бейімделген stylesheet factory-лерін және жергілікті module worker-ді пайдаланады. Жаңа CDN, script unsafe-inline немесе unsafe-eval қосылған жоқ. Нақты браузердегі worker startup пен CSP console тексерісі қолжетімсіз болды; олар checklist-та Pending.

## 17. Custom Run неге Progress-ке әсер етпейді?

Progress ресми submission history-ден есептеледі. Custom Run ешбір Submission/ExecutionResult жасамайтындықтан, attempts, solved tasks, Accepted саны және success rate өзгермейді. Жаңа уақытша Success күйі статистикадағы Accepted ретінде сақталмайды.

Тесттерде Run алдында және кейін төрт кестенің толық жолдары тұрақты Id ретімен JSON-ға айналдырылып, SQL ішінде SHA-256 hash салыстырылды. Source/feedback мәтіндері консольге шығарылған жоқ. Сонымен қатар Progress бетінің көрсеткіштері салыстырылды. Бос тарихта да, ресми әрекеттері мен AI feedback-і бар тарихта да теңдік расталды.

## 18. Custom Run неге Leaderboard-қа әсер етпейді?

CustomRunService LeaderboardService шақырмайды. Leaderboard жолдарын құру, жаңарту немесе score есептеу Run ағынына кірмейді. Оның толық DB state hash-і де өзгермегені тексерілді.

Submit бұрынғы қызметімен аяқталып, leaderboard-ты бұрынғы тәртіппен жаңартады. Mixed concurrency тестінде екі Custom Run және бір ресми Submit бірге орындалды: жалпы runner контейнер саны екіден аспады, ал тек ресми әрекет тарихқа кірді.

## 19. Қандай regression тесттері орындалды?

Өзгеріс алдында build, модель, database update, Docker/image және Phase 2–5/Enhancement 1 baseline тесттері өтті. Өзгерістен кейінгі нәтижелер:

| Тексеру | Нәтиже |
|---|---|
| Phase 2 HTTP/SQL | Өтті: Identity, roles, Admin, CSRF, hidden tests |
| Phase 3 нақты Docker | Өтті: 13 жіберілім, statuses, шектер, cleanup, ownership |
| Phase 4 HTTP және offline C# | Өтті: AI қауіпсіздігі, Progress, Leaderboard; ақылы сұрау жоқ |
| Phase 5 HTTP/SQL/contrast | Өтті; нақты visual testing емес |
| Enhancement 1 HTTP, C#, editor CSP doubles | Өтті: nonce, Host, pinning, compile OOM/timeout |
| Custom Run | Success, empty input, UTF-8 шектері, compile/runtime/time/memory/output failure өтті |
| DB side effects | Төрт кестенің толық state hash-тері және Progress өзгермеді |
| Journey | Нақты CompilationError → WrongAnswer → Accepted; timestamp ties; AI marker; 25 әрекеттің pagination/preview реті өтті |
| Diff | Owner/same-task/invalid-ID қорғанысы, reversed IDs, encoded source, CSP және hidden audit өтті |
| Аралас Run/Submit | Ортақ екі орын, нақты container inspect, pinned image және responsiveness өтті |
| Cancellation және unavailable Docker | Run өзгеріссіз cleanup жасады; Submit бұрынғы safe InternalError сақтады |
| JS integration doubles | Run payload/token/sync, loading/reset, failures/abort, text sinks және read-only diff options өтті |
| Соңғы build | 0 error, 0 warning |
| EF модель және database update | Pending model changes жоқ; database бұрыннан current; migration жасалмады |
| Git whitespace | `git diff --check` өтті; generated Monaco жолдарындағы string мәніне кіретін бос орындарға ғана `.gitattributes` ережесі бар |
| Cleanup | Runner контейнерлері және уақытша workspace қалдықтары: 0 |
| Нақты браузер | Қолжетімсіз: 0 viewport, screenshot жоқ |

Phase 4 missing-key HTTP тармағы Development кілті бар болғандықтан skipped; offline missing-key тексерісі өтті. Enhancement 2 барысында live OpenAI сұрауы орындалған жоқ. Барлық жаңа feature fixture-лері finally ішінде тек өз GUID/email/slug-тары бойынша жойылды.

Негізгі командалар:

```powershell
dotnet build
dotnet ef migrations has-pending-model-changes --no-build
dotnet ef database update --no-build
docker info --format '{{.ServerVersion}} {{.OSType}}'
docker image inspect gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15 --format '{{.Id}}'
npm.cmd run build:editor
node scripts/verify_editor_csp.mjs
node scripts/verify_enhancement2_ui.mjs
dotnet run --project tests/Phase4Checks/Phase4Checks.csproj --no-restore
dotnet run --project tests/Enhancement1Checks/Enhancement1Checks.csproj --no-restore
dotnet run --no-build --launch-profile https -- --Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command=Warning
python scripts/verify_phase2.py
python scripts/verify_phase3.py
python scripts/verify_phase4.py
python scripts/verify_phase5.py
python scripts/verify_enhancement1.py
python scripts/verify_enhancement2.py
git diff --check
```

Unavailable тесті үшін тек уақытша app процесіне `DOCKER_HOST=npipe:////./pipe/ipp-unavailable-verification` берілді. Сол процесс іске қосылғанда `python scripts/verify_enhancement2.py --unavailable` және `python scripts/verify_phase3.py --unavailable` орындалды. Docker Desktop пен machine/user environment тұрақты өзгертілген жоқ.

## 20. Enhancement 3-те не жасалады?

Progressive AI hint ladder, learning weakness map, topic strength classification, localization/KZ–RU–EN және жаңа тілдер бұл өзгеріске кірмейді. Оларды келесі жеке талап бойынша жобалау қажет. Қазір ешқандай жаңа hint деңгейі, weakness талдауы немесе locale жүйесі жасалған жоқ.

Қолмен тексерілетін қадамдар [manual-enhancement2-checklist.md](manual-enhancement2-checklist.md) ішінде: 1440 × 900, 1024 × 768 және 390 × 844. Custom panel, loading күйі, timeline, compare links, diff highlighting, read-only behavior, mobile overflow, worker startup, CSP console және JavaScript errors тексерілуі керек. Автоматты HTTP немесе mocked-DOM нәтижесі visual success ретінде көрсетілмейді.

## Файлдар есебі

Жасалған 17 файл:

- `.gitattributes` — generated Monaco string literal-дарындағы маңызды бос орындарды сақтайтын тар ереже
- `Controllers/CustomRunsController.cs`, `Controllers/JourneysController.cs`
- `Services/Submissions/CustomRunService.cs`, `Services/Submissions/AttemptJourneyService.cs`
- `ViewModels/Submissions/CustomRunViewModel.cs`, `ViewModels/Submissions/CustomRunResult.cs`
- `ViewModels/Submissions/AttemptJourneyViewModel.cs`, `ViewModels/Submissions/SubmissionComparisonViewModel.cs`
- `Views/Journeys/Index.cshtml`, `Views/Shared/_AttemptJourney.cshtml`, `Views/Submissions/Compare.cshtml`
- `wwwroot/js/custom-run.js`
- `scripts/verify_enhancement2.py`, `scripts/verify_enhancement2_ui.mjs`
- `manual-enhancement2-checklist.md`, `study-enhancement2.md`

Өзгертілген 16 файл:

- `Program.cs`, `README.md`
- `Controllers/TasksController.cs`, `Controllers/SubmissionsController.cs`
- `Services/CodeExecution/CodeRunnerOptions.cs`, `Services/CodeExecution/DockerCodeRunner.cs`, `Services/CodeExecution/SubmissionWorkspace.cs`
- `Services/Submissions/SubmissionService.cs`, `Services/Submissions/TaskPageService.cs`
- `ViewModels/Tasks/TaskDetailsViewModel.cs`
- `Views/Tasks/Details.cshtml`, `Views/Tasks/_SubmissionForm.cshtml`, `Views/Submissions/Details.cshtml`
- `ClientScripts/code-editor.js`, `wwwroot/js/editor/code-editor.js`, `wwwroot/css/site.css`

Entity, DbContext, migration, CSP policy, AllowedHosts және image digest өзгертілмеді. Authored C# әдістері мен конструкторларының алдында қысқа қазақша мақсат комментарийлері бар.
