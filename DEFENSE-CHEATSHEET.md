# Қорғау алдындағы қысқа шпаргалка

2026-10-02 күнгі source бойынша. Толық жауаптар: [DEFENSE-PREP.md](DEFENSE-PREP.md). Файлдар: [PROJECT-MAP.md](PROJECT-MAP.md).

## Жоба туралы 30 секунд

> Менің жобам — C++ және Python үйренуге арналған веб-платформа. Студент сабақ оқып, есеп шешеді, кодын Docker ішінде қауіпсіздік шектеулерімен тексереді. Дұрыстықты нақты тесттер анықтайды, AI қатені түсіндіріп, кезеңмен ашылатын кеңес береді. Әрекеттер тарихы, кодтарды салыстыру және оқу картасы студентке келесі қадамын таңдауға көмектеседі. Әкімші оқу мазмұнын басқарады. Интерфейс үш тілде жұмыс істейді.

## Архитектураны бір сызбамен айту

```text
Browser -> Controller -> Service -> EF Core -> SQL Server
                 |          +----> Docker CLI -> C++/Python containers
                 |          +----> OpenAI Responses API
                 +-> ViewModel -> Razor -> HTML/CSS/JavaScript
Lessons/Admin Controller -------> EF Core (тікелей CRUD/оқу)
```

**Model** — дерек; **View** — экран; **Controller** — HTTP сұрауы; **Service** — қолданба логикасы; **ViewModel** — экранға/формаға қажет дерек. `Program.cs` бәрін DI арқылы байланыстырады. Бұл бір MVC қолданбасы; services бумасы жеке микросервистер емес.

| Технология | Осы жобадағы нақты міндеті |
|---|---|
| C# / .NET 10 / ASP.NET Core MVC | Controller, service, async HTTP және web pipeline |
| Razor | ViewModel-ден HTML; source/AI мәтінін encoding арқылы көрсету |
| EF Core 10.0.12 / SQL Server | LINQ сұраулары, FK/index, migrations, тұрақты тарих |
| Identity | UserManager, SignInManager, пароль hash, cookie, Student/Admin |
| Docker | Екі тілдің сенімсіз кодын Linux контейнерлерінде шектеу |
| Monaco 0.57.0 / JavaScript | C++/Python editor, тілдік draft, Diff, Custom Run UI |
| Bootstrap / CSS | Навигация, формалар, responsive беттер |
| OpenAI SDK 2.14.0 / Responses API | Structured Outputs арқылы tutor JSON жауабы |
| RESX / localization | kk-KZ, ru-RU, en-US; әдепкі қазақша; culture cookie |
| Git / GitHub | Source нұсқалары мен remote репозиторий; deployment автоматты деген сөз емес |

## Негізгі кестелер

| Entity / кесте | Не сақталады? |
|---|---|
| ApplicationUser / AspNetUsers | Identity аккаунты, DisplayName; пароль орнына PasswordHash |
| Topics | Оқу тақырыбы |
| Lessons | Тақырыптың сабағы, мәтін, display-only мысал, реті, жариялануы |
| ProgrammingTasks | Шарт, difficulty, topic, уақыт/жад, жариялануы |
| TestCases | Input, ExpectedOutput, hidden белгісі, есеп ішіндегі рет |
| Runtimes | Тіл метадерегі және қосулы күйі; trusted команда source-та бекітіледі |
| Submissions | User+Task+Runtime+SourceCode, жалпы status және уақыттар |
| ExecutionResults | Әр submission/test үшін output, status, exit code, уақыт |
| AiFeedbacks | Бір submission-ға бір feedback, hints JSON, RevealedHintCount |
| Leaderboards | Бір user-ге сақталатын рейтинг қорытындысы; негізі Submissions |

`Topic 1→N Task 1→N TestCase`; `Task/User/Runtime 1→N Submission`; `Submission 1→N ExecutionResult`; `Submission 1→0..1 AiFeedback`. Identity рөлдер мен user-role байланыстарын өз кестелерінде сақтайды.

## Run, Submit және екі тіл

| Сұрақ | Custom Run | Submit |
|---|---|---|
| Controller / service | CustomRunsController / CustomRunService | SubmissionsController / SubmissionService |
| Кіріс | Студент енгізеді | TestCases-тен алынады |
| Дұрыстықты тексеру | ExpectedOutput жоқ; output/status көреді | Әр тесттің expected output-ымен салыстырады |
| DB тарихы | Submission жасалмайды | Submission және ExecutionResults сақталады |
| Progress/рейтинг | Әсер етпейді | Аяқталған нәтиже есепке алынады |
| Ортақ орындау | Docker, trusted runtime, ресурс шектері | Docker, trusted runtime, ресурс шектері |

**C++:** `main.cpp` → контейнердегі `g++ -std=c++20 -O2 -pipe` → executable → әр тестке жаңа контейнер.

**Python:** `main.py` → контейнердегі `py_compile` → әр тестке жаңа Python interpreter. SyntaxError = `CompilationError`. `-I -S -B`, орындауда `-u`; `PYTHONDONTWRITEBYTECODE` environment айнымалысы бұл runner-де қолданылмайды. `-X pycache_prefix=/tmp/pycache` compile cache-ті уақытша аймаққа бағыттайды.

Python стандартты кітапханасы қолданылады; pip орнату ағыны жоқ, network өшірулі, root filesystem тек оқылады. Бұл әр standard-library модуліне жеке allowlist қойылған деген сөз емес.

**Тексеру:** `STDIN = Input`; `STDOUT = ActualOutput`; `STDERR = диагностика`; `ExitCode = процестің аяқталу коды`.

**Салыстыру:** CRLF/CR → LF, содан кейін бүкіл мәтінге `TrimEnd()`. Бастапқы бос орын мен ішкі жолдардың бос орындары сақталады. Мәтін сәйкес емес → WrongAnswer; бәрі Passed → Accepted. Compile кезеңінің timeout/OOM жағдайлары да жалпы `CompilationError` ретінде қайтады.

## Docker қауіпсіздігін жаттау

- `--network none`: сыртқы желі жоқ.
- `--cap-drop ALL`, `no-new-privileges`, UID/GID `65534:65534`: артық құқықты азайтады.
- `--read-only`: контейнердің түбірлік filesystem-ін өзгертпейді; шектелген `/tmp` бар.
- `--pids-limit 64`: процесс санын шектейді.
- `--memory` және тең `--memory-swap`: берілген жад бюджеті, қосымша swap жоқ.
- CPU: compile `1`, тест `0.5`; compile 15 секунд/512 MB.
- Source 64 KiB; custom input 32 KiB; stdout және stderr әрқайсысы 64 KiB.
- Task лимиті 100–30000 ms және 16–1024 MB аралығында қабылданады.
- Жалпы job: Run 90 секунд, Submit 180 секунд; ортақ gate 2 жұмыс, күту 10 секунд.
- Image digest-пен бекітіледі; студент image/command/mount бермейді; `--pull never`.
- Әр тест жаңа контейнерде; соңында контейнер мен кездейсоқ workspace тазаланады.

`DockerCli` **Process.Start қолданады**, бірақ тек Docker CLI үшін. Студент коды ASP.NET host-та тікелей орындалмайды. Docker 100% қауіпсіздік кепілі емес; daemon/host тоқтаса cleanup толық аяқталмауы мүмкін.

## AI және hints

```text
Өзінің аяқталған Submission-ы
 -> OpenAiTutorService: ownership + existing feedback + gate
 -> шектелген AiTutorInput
 -> OpenAiFeedbackClient: Responses API + strict JSON Schema
 -> parse/validation/sanitization -> AiFeedback сақтау
 -> бастапқы Hint 1 -> HintRevealService -> Hint 2 -> Hint 3
```

AI-ға: есеп шарты, source, тіл, status, шектелген compiler диагностикасы, ең көбі үш visible failed test мысалы; hidden тесттің тек нөмір/status/time метадерегі беріледі.

AI prompt-қа **hidden input/expected/actual, пароль, security stamp және API key** қосылмайды. API key OpenAI сұрауын авторизациялау үшін қолданылады. SourceCode — сенімсіз user data; жүйелік нұсқау бөлек беріледі.

AI судья емес: Accepted-ті runner анықтайды. Жауап schema-сы summary/explanation/3 hints/errorCategory пішімін шектейді; мағыналық мінсіздікке кепілдік бермейді. `StoredOutputEnabled=false` бүкіл API инфрақұрылымында ешбір retention жоқ дегенді білдірмейді.

Үш hint DB-да сақталады. Браузер тек `Take(RevealedHintCount)` бөлігін алады; бастапқы сан — 1. Reveal POST келесі санды серверде шартты түрде жаңартады, **OpenAI шақырмайды**. Батырманы қатар басу бір ескі саннан екі саты секіртпеуі үшін шартты update қолданылады; міндетті ойлану таймері жоқ.

## Формулалар және нақты айырмалар

**Progress:** аяқталған күйлер Accepted…InternalError; жалпы есепте published сүзгісі жоқ. Pending/Compiling/Running кірмейді.

```text
SolvedTasks = кемінде бір Accepted бар бірегей Task саны
CompilationErrors = CompilationError аяқталған әрекеттер саны
SuccessRate = AcceptedSubmissions / TotalCompletedSubmissions × 100
             (бөлім 0 болса 0; 1 ондық таңбаға дөңгелектеу)
SolveTime(task) = алғашқы Accepted.CreatedAt − алғашқы Submission.CreatedAt
AverageSolveTime = жарамды шешілген есептердің SolveTime орташа мәні
                   (жарамды есеп жоқ болса мән жоқ)
Rating = 100 × EasySolved + 200 × MediumSolved + 300 × HardSolved
```

`ExecutionTimeMs` — код орындалу өлшемі; AverageSolveTime оқу әрекеттері арасындағы уақытты өлшейді. Бір есептің қайталанған Accepted-і ұпайды көбейтпейді. Difficulty өзгерсе, қайта есептеу ағымдағы difficulty-ді пайдаланады.

**Learning Map:** тек published есептер және Accepted…MemoryLimitExceeded нәтижелері; InternalError кірмейді. Әр тақырып үшін:

```text
CompletionRate = SolvedPublishedTasks / PublishedTasks
SubmissionSuccessRate = AcceptedSubmissions / CompletedSubmissions
StrengthScore = (CompletionRate × 0.70 + SubmissionSuccessRate × 0.30) × 100
```

Бөлім 0 болса тиісті үлес 0. Үлестер 0…1; score 0…100. Мысал: 2/4 есеп шешілген, 3/5 әрекет Accepted → `(0.5×0.70 + 0.6×0.30)×100 = 53`.

Level-ді **ретімен** тексеру: әрекет 0 → NotStarted; әрекет <3 → Exploring; әйтпесе score ≥75 → Strong, ≥45 → Developing, қалғаны → NeedsPractice. Бұл интеллектке AI берген баға емес.

**Practice Next:** NeedsPractice → Developing → Exploring → NotStarted → Strong; содан кейін төмен StrengthScore, TopicId. Әр тақырыптан әлі шешілмеген published есептің ең жеңіл difficulty-і, кейін кіші Id таңдалады. AI қажет емес.

**Leaderboard:** Submissions — негізгі дерек; Leaderboards — материалданған қорытынды. Submit-тен кейін user summary жаңартылады; Admin қайта есептей алады. Реті: Score төмендемелі → SolvedTasks төмендемелі → UserId өспелі. Бетті ашу барлық бар summary-ді қайта есептемейді, жетіспейтіндерін толықтырады.

## Қауіпсіздік пен интерфейс туралы дәл айту

- `[Authorize]` — кіру талабы; Admin рөлі — әкімшілік рұқсат; submission сұрауы `UserId` бойынша ownership тексереді.
- Identity PasswordHash сақтайды; парольді өз қолымызбен ашық мәтінге жазбаймыз.
- Antiforgery — CSRF-тен; Razor encoding/`textContent` — мәтіннің HTML болып орындалуынан; CSP nonce — рұқсат етілген script/style-ды ажырату үшін.
- CSP nonce әр сұрауға 32 кездейсоқ байттан жасалады; Monaco үшін style attribute ерекшелігі бар, script-ке `unsafe-inline`/`unsafe-eval` берілмейді.
- Cookie HttpOnly/Secure/SameSite=Lax; HTTPS; production HSTS; AllowedHosts — `localhost;127.0.0.1`.
- Hidden тест дерегі студенттің HTML/JS/AI payload-ына кірмейді; Admin тестті басқару үшін көре алады.
- C++ және Python draft-тары бөлек; Diff екі өз submission-ын, бір есеп шегінде салыстырады. Бір қолдау бар тіл — сол highlighting; аралас тіл — plaintext.
- Journey бөлек кесте емес: тарихтан алынады. Compare сілтемесі осы runtime-дағы алдыңғы әрекетке апарады.
- Сабақ code example-і тек көрсетіледі; оны ашу Docker шақырмайды.
- UI үш тілде, әр мәдениетте 581 resource key; DB есеп мәтіндері автоматты аударылмайды.

## Ең ықтимал 25 сұрақ пен қысқа жауап

1. **Жоба қандай мәселені шешеді?** Студентке кодты тексеру, қатесін түсіну және келесі жаттығуын таңдау жолын бір ортада береді.
2. **MVC деген не?** Model деректі, View көріністі, Controller сұрауды басқарады; күрделі логика services ішінде.
3. **Неге Razor, React емес?** Бұл серверде жасалатын каталогтар, формалар және нәтижелер қолданбасы; интерактивті editor үшін жергілікті JavaScript жеткілікті.
4. **DbContext не істейді?** EF арқылы кестелерге сұрау, relation mapping және SaveChanges операциясын басқарады.
5. **Migration не үшін керек?** Schema өзгерісін нұсқалап сақтайды; қазіргі жобада төрт migration бар.
6. **DI деген не?** Program қызметті тіркейді, framework оны constructor-ға береді; controller оны өзі қолмен құрмайды.
7. **Identity не береді?** Аккаунт, password hashing, sign-in cookie, роль және lockout механизмдерін береді.
8. **Authentication пен authorization айырмасы?** Біріншісі кім екенімді, екіншісі нақты әрекетке рұқсатымды анықтайды.
9. **Run мен Submit айырмасы?** Run өз input-ымен уақытша тексереді; Submit ресми тесттерді өтіп, тарих пен статистикаға сақталады.
10. **Неге Docker?** Сенімсіз кодтың желісін, құқықтарын, CPU/жадын және процестерін оқшаулау үшін.
11. **Process.Start мүлде қолданылмай ма?** DockerCli оны Docker клиентін іске қосуға қолданады; студент бағдарламасын host-та іске қоспайды.
12. **C++ пен Python runner айырмасы?** C++ executable-ге compile болады; Python syntax тексеріліп, әр тестте interpreter арқылы орындалады.
13. **Hidden тест неге браузерге берілмейді?** Жауапты алдын ала көру тексеруді бұзады; студент projection-ында input/expected/actual жоқ.
14. **WrongAnswer қалай анықталады?** Қалыпты орындалған output пен expected CR/LF және TrimEnd нормалауынан кейін сәйкес келмейді.
15. **Шексіз цикл не болады?** Уақыт шегі іске қосылып, тест TimeLimitExceeded болады; compile/job timeout-ының бөлек жолдары бар.
16. **AI неге judge емес?** Баға тек детерминдік тест нәтижесінен шығады; AI кеңесінің қате болуы мүмкін.
17. **Structured Outputs не береді?** JSON Schema-ға сай құрылым; сервер қосымша ұзындық, сан және мазмұн тексеруін орындайды.
18. **Hint 2 ашу ақылы ма?** Жаңа OpenAI сұрауы жоқ: бұрын DB-да сақталған келесі hint ашылады.
19. **Prompt injection деген не?** Source ішіне AI нұсқауын өзгертетін мәтін салу; source сенімсіз дерек ретінде бөлек беріледі.
20. **Бөтен Submission ID берілсе ше?** Ownership шарты сәйкес келмейді; бөтен source/feedback көрсетілмейді.
21. **Ұпай қалай есептеледі?** Бірегей шешілген Easy/Medium/Hard үшін 100/200/300; қайталап Accepted ұпай қоспайды.
22. **StrengthScore ақылды өлшей ме?** Жоқ, тақырыптағы аяқталған есептер мен сәтті әрекеттердің 70/30 салмақталған көрсеткіші.
23. **CSRF пен XSS айырмасы?** CSRF cookie бар браузерді бөтен әрекетке мәжбүрлейді; XSS бетке орындалатын зиянды script енгізеді.
24. **SemaphoreSlim бірнеше серверді қорғай ма?** Жоқ, бұл gate бір процесс ішінде ғана; horizontal scaling үшін ортақ үйлестіру қажет.
25. **Web процесс орындау кезінде құласа ше?** Durable queue/recovery жоқ; Pending/Running жазбасы немесе контейнер қалып қоюы мүмкін. Production-та бөлек worker, кезек, recovery және бақылау керек.

## Қажетті командалар

Жоба каталогы: `C:\ENT\Visual-Studio-Projects\IntelligentProgrammingPlatform`.

| Команда | Қысқа мағынасы |
|---|---|
| `dotnet restore` | NuGet тәуелділіктерін қалпына келтіру |
| `dotnet tool restore` | Репозиторийдің EF CLI құралын дайындау |
| `dotnet build` | Компиляция; running exe lock болса өз қолданбаңызды алдымен тоқтату |
| `dotnet ef migrations has-pending-model-changes` | Модель мен snapshot айырмасын тексеру |
| `dotnet ef database update` | Бар migration-дарды базаға қолдану |
| `dotnet ef migrations add Name` | Жаңа schema өзгерісіне migration; осы дайын жобаға қазір қажет емес |
| `dotnet run --launch-profile https` | Development HTTPS сайтты іске қосу |
| `dotnet watch run --launch-profile https` | Source өзгерісін бақылап іске қосу |
| `dotnet dev-certs https --trust` | Жергілікті HTTPS сертификатына сену |
| `docker --version` | Клиент нұсқасы; daemon жұмысының дәлелі емес |
| `docker info` | Docker daemon/engine күйі |
| `docker ps` / `docker ps -a` | Жұмыс істеп тұрған / барлық контейнерлер |
| `docker images` | Жергілікті image тізімі |
| `npm ci` / `npm run build:editor` | JS тәуелділіктері / editor жинағы |
| `git status` / `git diff` | Өзгерістерді тексеру |
| `git add <file>` / `git commit -m "message"` | Таңдалған файлды дайындап, нұсқаны сақтау |
| `git push` | Commit-терді remote-қа жіберу |

**Іске қосу:** SQL Server → Docker Desktop Linux engine → pinned images барын тексеру → restore/build/database update → HTTPS profile → **https://localhost:7115**. HTTP қосымша порт — 5191. Тоқтату — сол terminal-да **Ctrl+C**. Нақты image pull командалары негізгі нұсқаулықтың 38-бөлімінде.

**Ақау болса:** SQL — `Get-Service -Name 'MSSQL*'`; Docker — `docker info`; порт — `Get-NetTCPConnection -LocalPort 7115,5191`; ескі JS — hard reload/DevTools. API key-ді экранға шығармаңыз. Key бар болуы live API жұмысын дәлелдемейді.

## Соңғы демо реті

**9 минут шамасында:** қазақша Home → Lessons → Tasks → есеп → Python/C++ таңдау → Custom Run → қате Submit → тест status → AI feedback → келесі hint → код түзету → Accepted → Journey → Diff → Progress → Оқу картасы/Practice Next → Рейтинг → RU/EN → Admin.

Үш басты сөйлем: **«Run тарихты өзгертпейді»**, **«Judge — тест runner, AI — түсіндіруші»**, **«Рейтинг бірегей шешілген есептен есептеледі»**.

**3 минут:** Home/мақсат → дайын task пен тіл → бір Custom Run → өзіңіздің сақталған WrongAnswer/Accepted нәтижелеріңіз → сақталған AI/hints → Journey/Diff → Progress/Map → Admin/үш тіл. Сақталған нәтижені жаңа live сұрау ретінде көрсетпеңіз.

- [ ] SQL, Docker, pinned images және HTTPS дайын.
- [ ] Student/Admin аккаунттарымен кіру тексерілген.
- [ ] Бір C++ және бір Python Accepted нәтижесін көрсете аламын.
- [ ] AI live қолжетімділігі белгілі немесе сақталған feedback-пен көрсетемін.
- [ ] Hint, Diff, Progress, Lessons, үш тіл және шағын экран тексерілген.
- [ ] Құпия терезелер жабық; Git күйі белгілі; негізгі формулаларды түсіндіре аламын.

**Осы дайындықта расталғаны:** build — 0 қате/0 ескерту; pending model change жоқ; DB жаңартылған; Docker 29.6.2 Linux engine жұмыс істейді. Ақылы OpenAI сұрауы жасалмады. Браузерлік демо мен толық regression suite осы құжат дайындау кезінде қайта жүргізілмеді. Қолданба логикасы, schema және security конфигурациясы өзгертілген жоқ.
