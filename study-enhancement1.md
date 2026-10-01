# Enhancement 1 — қауіпсіздік пен орындау ортасын нығайту

Тексеру күні: 2026-10-01. Бұл жаңа кезең немесе архитектураны қайта жазу емес. Identity, EF Core, SQL Server, Monaco, Docker runner, жіберілімдер, AI Tutor, Progress, Leaderboard және Admin мүмкіндіктері сақталды. Дерекқор схемасы өзгермеді.

## 1. Нені жақсарттық?

Компиляция қатесі нақты Docker дерегіне сүйеніп жіктеледі. Әр HTTP сұрауына криптографиялық nonce жасалып, Content-Security-Policy қосылды. Рұқсат етілетін Host атаулары шектелді. GCC image нақты алынған digest арқылы бекітілді. Бұрынғы регрессиялар және жаңа шектеулі тестілер орындалды.

Phase 3 тарих тестіндегі қате болжам да түзетілді: қатар орындалған сұрауларда SQL identity нөмірі жасалу уақытымен міндетті түрде бір ретпен өспейді. Тест енді нақты талапты — `CreatedAt DESC, Id DESC` ретін — SQL нәтижесімен салыстырады. Қосымшаның тарих реттеу логикасы өзгерген жоқ.

## 2. ExitCode 137 деген не?

137 әдетте SIGKILL арқылы тоқтауды білдіреді (`128 + 9`). Бірақ санның өзі тоқтау себебін дәлелдемейді: процесс жад тапшылығынан, сыртқы тоқтатудан немесе timeout-тың мәжбүрлі аяқтауынан өлуі мүмкін. Сондықтан 137-ні бірден «уақыт немесе жад шегі» деп атамаймыз. GNU timeout құжаттамасы да осы екіұштылықты түсіндіреді: [ресми нұсқаулық](https://www.gnu.org/software/coreutils/manual/html_node/timeout-invocation.html).

## 3. OOMKilled деген не?

Runner контейнер аяқталғаннан кейін `docker inspect` арқылы `State.OOMKilled` мәнін оқиды. `true` болса, `Compilation memory limit exceeded.` беріледі. Бұл тексеру timeout пен output-limit белгілерінен бұрын орындалады. Inspect сәтсіз болса, runner себебін болжамай, инфрақұрылым қатесін қауіпсіз өңдейді.

Нақты тестте 32 MB шегі бар компилятор OOM күйіне түсті, бірақ сыртқы GCC процесі **1** кодын қайтарды. Бұл OOM-ды тек 137 кодынан іздеудің жеткіліксіз екенін көрсетеді. 137 + OOM және 137 + белгісіз себеп жағдайлары жеке жіктеу тестімен тексерілді. Нақты компиляция OOM-ының 137 қайтарғанын мәлімдемейміз.

## 4. Time limit пен memory limit айырмашылығы қандай?

Уақыт шегі орындалу ұзақтығын, жад шегі контейнердің жад тұтынуын шектейді. Компиляцияда GNU timeout-тың 124 коды немесе host watchdog-тың `TimedOut` белгісі уақыт шегінің дәлелі болады. OOM дерегі басым. Дәлелі жоқ 137 үшін `Compilation was terminated before it completed.` деген жалпы хабар беріледі.

Өндірістегі компиляция лимиттері өзгерген жоқ: 15 секунд, 512 MB. Регрессияда қауіпсіз әрі жылдам нәтиже алу үшін тек тест контейнеріне 10 ms timeout немесе 32 MB жад шегі берілді. Өлшенбеген `MemoryUsedKb` бұрынғыдай NULL; жалған сан сақталмайды. Қалыпты syntax error диагностикасы тазартылып көрсетіледі.

## 5. CSP деген не?

Content-Security-Policy браузерге қандай ресурстарды жүктеуге және қандай inline кодты орындауға болатынын айтады. Бұл Razor encoding пен CSRF қорғанысын алмастырмайтын қосымша шектеу. Саясат бір middleware-де бір рет жазылады; nosniff, frame denial, Referrer-Policy және Permissions-Policy сақталған.

| Директива | Қолданылған ереже |
|---|---|
| `default-src` | `'none'` |
| `script-src` | `'self'` және сұрау nonce-ы |
| `script-src-attr` | `'none'` |
| `style-src` | `'self'` және сұрау nonce-ы |
| `style-src-attr` | Редактор бар бетте `'unsafe-inline'`, қалғанында `'none'` |
| `img-src` | `'self' data:`; Bootstrap-тағы кірістірілген SVG белгілері үшін |
| `font-src`, `connect-src`, `worker-src` | `'self'` |
| `object-src`, `frame-src`, `frame-ancestors`, `base-uri` | `'none'` |
| `form-action` | `'self'` |

OpenAI браузерден шақырылмайды, сондықтан CSP-ге оның доменін қосудың қажеті жоқ. Саясаттың анықтамасы: [W3C CSP Level 3](https://www.w3.org/TR/CSP3/).

## 6. Nonce деген не?

Nonce — әр сұрауға жаңадан жасалатын кездейсоқ мән. `RandomNumberGenerator.GetBytes(32)` 256 бит береді, одан Base64 жол жасалады. Scoped `ContentSecurityPolicy` қызметін Razor DI арқылы алады. Бір жауаптағы header, import map және сенімді style блоктары бір nonce қолданады; келесі сұрауда мән өзгереді. HTML жауаптары `no-store` алады.

Monaco nonce-ты layout ішіндегі `meta[name="csp-nonce"]` арқылы оқиды. Бұл құпия API кілті емес: nonce браузерге сол жауаптың сенімді блоктарын тануға қажет. Қолданушы мәтіні осы блоктарға шикі HTML ретінде салынбайды. Progress ендері тек шектелген бүтін пайыздардан құрылады.

## 7. Неге unsafe-inline қауіпті?

Script үшін кең inline рұқсаты енгізілген зиянды кодтың орындалуын жеңілдетеді. Сондықтан script `unsafe-inline`, event-handler атрибуттары және `unsafe-eval` рұқсат етілмейді. Стиль атрибуттарына nonce қолданылмайды; редактордың нақты қажеттілігі жеке `style-src-attr` директивасымен шектелді. Бұл script рұқсатын кеңейтпейді.

MVC validation summary бос `<li>` элементіне inline hiding style қосатыны HTTP тексерісінен анықталды. `CspValidationSummaryTagHelper` framework жасаған дәл сол бос элементті CSS класына ауыстырады. Қате мәтінінің HTML encoding-і мен клиенттік validation құрылымы сақталады. Қараңыз: [ASP.NET Core бастапқы коды](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.TagHelpers/src/ValidationSummaryTagHelper.cs).

## 8. Monaco Editor мен CSP қалай бірге жұмыс істейді?

Орнатылған Monaco 0.57.0 кодында stylesheet nonce параметрі жоқ. Оның екі style factory-і (`domStylesheets.js`, `contextview.js`) build кезінде ғана бейімделеді: style DOM-ға қосылмай тұрып nonce беріледі. `node_modules` өзгертілмейді, жаһандық DOM interception қолданылмайды. `scripts/monaco-csp.mjs` версия мен нақты код үлгісін тексереді; жаңарту сәйкес келмесе build тоқтайды.

Monaco жол рендерері символ ені мен bidi оқшаулау үшін HTML style атрибуттарын жасайды. Сондықтан тек `_SubmissionForm` орындалған сұрауда атрибуттарға рұқсат қосылады. Қонақтың task беті, Login, Progress және Admin бұл рұқсатты алмайды. Worker бұрынғыдай жергілікті module worker; blob worker немесе eval қосылмады.

Node тексерісі екі factory-дің дайын bundle-де nonce қолдануын, бейімдеу guard-ын және editor интеграциясының DOM/Monaco алмастырғыштарымен textarea sync, submit, draft, validation басымдығы мен worker URL-ын тексерді. HTTPS тесті assets/chunks жүктелуін және nonce сәйкестігін тексерді. **Бұл нақты браузердегі highlighting, typing, worker startup немесе CSP console тексерісі емес.** Оларды қолмен растау қажет.

## 9. AllowedHosts деген не?

Host filtering HTTP сұрауының `Host` атауын allowlist-пен салыстырады. Негізгі конфигурацияда `localhost;127.0.0.1` қалдырылды. HTTPS localhost жұмыс істейді; бөтен Host 400 алады. Бос тізім мен wildcard мәндер options validation арқылы startup кезінде қабылданбайды. Бұл сервердің қай желілік интерфейске bind жасайтынын анықтамайды: Host тексерісі мен тыңдау адресі бөлек ұғымдар. [Microsoft құжаттамасы](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering?view=aspnetcore-10.0).

## 10. Development пен Production айырмашылығы қандай?

Development-та жергілікті қауіпсіз default және User Secrets сақталады. Git-ке кірмейтін `appsettings.Development.json` өзгертілген жоқ; ол негізгі Host тізімін мұралайды. Production-да нақты домендерді deployment environment/configuration арқылы `AllowedHosts` ретінде ашық көрсету керек. Домен ойдан қосылған жоқ; конфигурация жасалмайынша сыртқы Host атауы өтпейді. Production HSTS қолданады.

Production тексерісі бөлек loopback порттарында `enhancement1.invalid` деген **тек тестке арналған** allowlist-пен жүргізілді. Бұл deployment домені емес. HTTPS, HSTS, CSP, HTTP redirect және 400/404/500 жауаптары тексерілді; wildcard startup тесті де өтті. Тексеру процестері аяқталған соң тоқтатылды.

## 11. Docker image tag деген не?

`gcc:14.3.0-bookworm` — repository ішіндегі адам оқитын нұсқа атауы. Tag кейін басқа image-ке бағытталуы мүмкін. Сондықтан атауда нұсқа көрсетілгенінің өзі толық өзгермейтін кепілдік бермейді.

## 12. Docker image digest деген не?

Digest image мазмұнына қатысты өзгермейтін идентификаторды береді. Нақты орнатылған GCC RepoDigests мәні Docker-ден оқылып, қайта pull/inspect арқылы расталды:

```text
gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
```

Бұл мән ойдан шығарылған жоқ. [Docker түсіндірмесі](https://docs.docker.com/dhi/explore/security-concepts/digests/).

## 13. Digest pinning не үшін керек?

Pinning бірдей бекітілген compiler image қолдануды қамтамасыз етеді. Docker Desktop-та combined `tag@digest` lookup тұрақты болмады; repository digest lookup тұрақты өтті. Сондықтан сервер canonical `gcc@sha256:…` сілтемесін қолданады. Mutable tag fallback жоқ. Image жоқ болса, қауіпсіз unavailable нәтижесі шығады.

Runner digest-ті inspect жасап, контейнерге тек тексерілген image ID береді. Студент image, compiler немесе аргументтерді таңдамайды. Runtime кестесіндегі command/image мәтіні орындалмайды; бұрынғы белгілі seed metadata ғана жаңартылады. Жаңа digest-ке көшу — саналы серверлік өзгеріс және қайта регрессияны талап етеді; pinning осалдықтарды автоматты жөндемейді.

## 14. Docker security қайта қалай тексерілді?

Phase 3 нақты compiler/run контейнерлерін inspect арқылы тексерді: желі жоқ, барлық capability түсірілген, no-new-privileges, PID/CPU/memory/swap лимиттері, read-only root, 65534 пайдаланушысы, noexec tmpfs және тек GUID workspace mount-тары сақталған. Docker socket, жоба/root/home mount, privileged немесе host networking қолданылмайды.

13 нақты жіберілім Accepted, WrongAnswer, CompilationError, TimeLimitExceeded, RuntimeError және MemoryLimitExceeded жағдайларын қамтыды. Шексіз stdout 64 KiB шегінде тоқтады. Үш қатар сұрауда белсенді контейнер саны екіден аспады. Request cancellation, host/container timeout, container/workspace cleanup және сервердің жауап беруі тексерілді. Runtime metadata-ны бұрмалау runner таңдауын өзгерте алмады.

Жеке unreachable-Docker процесі алғашқы және кэштелген сәтсіздікте қауіпсіз InternalError қайтарды; host compiler іске қосылмады. Нақты OOM тесті бір 32 MB контейнермен шектелді. Барлық тест контейнерлері мен өз workspace-тері тазартылды.

## 15. Hidden tests қалай тексерілді?

Phase 2 көрінетін мысалдарды, unpublished task және Admin рөлін тексерді. Phase 3 толық студент HTML-інен hidden input, expected/actual output пен diagnostics жоқ екенін және басқа студенттің жіберіліміне 404 берілетінін тексерді. Phase 4 offline DTO тексерісі AI-ға тек hidden metadata жіберілетінін растады. Авторизация тек UI батырмаларына сүйенбейді; SQL ownership сүзгісі сақталған.

## 16. XSS/CSRF қалай тексерілді?

Source, task description, AI feedback және leaderboard display name Razor encoding арқылы көрсетіледі. POST әрекеттері antiforgery token талап етеді; read-only error rendering ғана босатылған. Phase 2–5 нақты cookies және токендермен осы жолдарды тексерді. `Html.Raw`, shell concatenation, privileged/network/socket үлгілері қайта қаралды. Жалғыз production `Process.Start` — бекітілген Docker CLI, `ArgumentList`, `UseShellExecute=false` және bounded I/O арқылы қолданылады.

Git-tracked файлдардағы API key, Bearer, password, connection-password және secret-file үлгілері құпия мәнді баспай сканерленді. Нақты credential табылған жоқ; ықтимал сәйкестіктер configuration references, қате-login fixture-лері және Monaco идентификаторлары болды. User Secrets мазмұны Git-ке көшірілмеді. AI authentication/network/timeout қателері offline transport арқылы тексерілді; техникалық мәлімет пайдаланушыға шықпайды. **Бұл enhancement ақылы AI сұрауын жіберген жоқ.**

## 17. Responsive design қалай тексерілді?

CSS пен HTML статикалық қаралды: solve/result grid-де `minmax(0, …)` және `min-width: 0`, editor-де 100% ен мен automatic layout, code/pre ішінде scroll, кестелерде responsive wrapper, navigation collapse және mobile breakpoint бар. Phase 5 markup пен түс контрастын тексерді.

Браузер жоқ болғандықтан **нақты тексерілген viewport саны — 0**. 1440, 1024 немесе 390 px-та screenshot түсірілді деп айтылмайды. Расталмаған layout проблемасы ойдан түзетілген жоқ; шағын CSS өзгерісі CSP-мен үйлесімді validation placeholder үшін ғана жасалды. Progress bar inline attribute орнына nonce бар style блогын қолданады; оның визуалдық көрінісін қолмен салыстыру керек.

## 18. Қандай нәрселерді қолмен тексеру қажет?

[manual-visual-checklist.md](manual-visual-checklist.md) барлық негізгі беттерді және нақты 1440 × 900, 1024 × 768, 390 × 844 өлшемдерін қамтиды. Monaco highlighting/typing, hidden textarea sync, submit payload, draft restoration, нақты worker startup, CSP console, menu collapse, table scroll, form validation, AI мәтіні, Progress bars және Admin layout-ты браузерде растаңыз. Checklist ұяшықтары әзірше Pending.

## Орындалған тексерулер

| Тексеру | Нәтиже |
|---|---|
| Phase 2 HTTP/SQL | Өтті |
| Phase 3, 13 нақты Docker submissions | Өтті; тарих тестінің қатар орындалу болжамы түзетілді |
| Docker unavailable / cached failure | Өтті |
| Phase 4 HTTP және offline C# | Өтті; ақылы сұрау жоқ |
| Phase 4 missing-key HTTP тармағы | Кілт бар Development процесінде skipped; offline missing-key тесті өтті |
| Phase 5 HTTP/SQL/contrast | Өтті |
| Enhancement 1 C# classifier және нақты bounded compile | Өтті; OOM exit 1, 137 комбинациялары unit деңгейінде |
| Enhancement 1 HTTPS nonce/CSP/Host/assets | Өтті |
| Monaco build/adapter және integration doubles | Өтті; нақты browser емес |
| Production explicit Host/HSTS/redirect/error және wildcard startup | Өтті |
| Browser visual / screenshots / CSP console | Қолжетімсіз, қолмен тексеру қажет |
| Соңғы `dotnet build` | 0 қате, 0 warning |
| EF pending model / database update | Өзгеріс жоқ; база өзекті; migration жасалған жоқ |
| `git diff --check` | Өтті |

## Файлдар есебі

Жасалған 11 файл:

- `Services/CodeExecution/CompilationFailureClassifier.cs`
- `Services/Security/ContentSecurityPolicy.cs`
- `Services/Security/SecurityHeadersMiddleware.cs`
- `TagHelpers/CspValidationSummaryTagHelper.cs`
- `scripts/monaco-csp.mjs`
- `scripts/verify_editor_csp.mjs`
- `scripts/verify_enhancement1.py`
- `tests/Enhancement1Checks/Enhancement1Checks.csproj`
- `tests/Enhancement1Checks/Program.cs`
- `manual-visual-checklist.md`
- `study-enhancement1.md`

Өзгертілген 15 файл:

- `Program.cs`, `appsettings.json`, `Data/DbSeeder.cs`
- `Services/CodeExecution/CodeRunnerOptions.cs`, `Services/CodeExecution/DockerCodeRunner.cs`
- `Views/_ViewImports.cshtml`, `Areas/Admin/Views/_ViewImports.cshtml`
- `Views/Shared/_Layout.cshtml`, `Views/Tasks/_SubmissionForm.cshtml`, `Views/Progress/Index.cshtml`
- `wwwroot/css/site.css`, `wwwroot/js/editor/code-editor.js`
- `scripts/build-editor.mjs`, `scripts/verify_phase3.py`, `README.md`

`Data/ApplicationDbContext.cs` бастапқы жұмыс күйінде өзгертілген болып көрінген; оның байттары мен SHA-256 мәні сақталды. Ол осы enhancement арқылы өңделген жоқ. Authored C# әдістері мен конструкторларында тікелей алдында қысқа қазақша мақсат түсіндірмесі бар; generated migration файлдары өзгерген жоқ.

Негізгі қайталанатын командалар (HTTP скрипттері үшін Development app іске қосулы болуы керек):

```powershell
dotnet build
dotnet ef migrations has-pending-model-changes --no-build
dotnet ef database update --no-build
docker --version
docker info
docker image inspect gcc:14.3.0-bookworm --format '{{json .RepoDigests}} {{.Id}}'
docker pull gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
docker image inspect gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15 --format '{{.Id}}'
npm.cmd run build:editor
node scripts/verify_editor_csp.mjs
dotnet run --project tests/Enhancement1Checks/Enhancement1Checks.csproj --no-restore
dotnet run --project tests/Phase4Checks/Phase4Checks.csproj --no-restore
dotnet run --no-build --launch-profile https -- --Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command=Warning
python scripts/verify_enhancement1.py
python scripts/verify_phase2.py
python scripts/verify_phase3.py
python scripts/verify_phase4.py
python scripts/verify_phase5.py
git diff --check
```

Unavailable тексерісі үшін қалыпты app-ты тоқтатып, **тек уақытша app процесінің** `DOCKER_HOST` мәнін `npipe:////./pipe/ipp-unavailable-verification` етіп іске қосамыз; содан кейін `python scripts/verify_phase3.py --unavailable` орындалады. Docker Desktop тоқтатылмайды және machine/user environment тұрақты өзгертілмейді.

Custom input Run, attempt timeline, code diff, progressive AI hints, weakness map және localization қосылған жоқ. Бұрынғы мүмкіндік әдейі жойылған жоқ.
