# Enhancement 6: Python оқу жолы, профиль және аватар

## 1. Не жасалды?

Қолданыстағы MVC жобаға тоғыз Python сабағы, өңделетін профиль, аватар, рейтингтегі және мәзірдегі сурет, әкімшінің пайдаланушылар бөлімі қосылды. Жоба қайта құрылмады. C++/Python орындаушысы, Monaco, AI Tutor, кеңестер, Journey, Diff, Progress және есеп басқару қызметтері сақталды.

## 2. Python сабақтары қалай ұйымдастырылды?

`/Lessons` бетінде «Python оқу жолы» сілтемесі бар. Ол `/Lessons?language=python` арқылы жарияланған Python сабақтарын `Order`, кейін `Id` бойынша реттейді. Сабақ бетінде бүкіл Python жолының алдыңғы/келесі сабағы көрсетіледі; тақырып ішіндегі бұрынғы навигация да сақталған.

| Оқу қадамы | Тақырып | Slug | Бар Topic | Lesson.Order |
|---|---|---|---|---|
| 1 | Python тіліне кіріспе | `python-basics` | Basics | 3 |
| 2 | Айнымалылар және деректер типтері | `python-variables` | Basics | 4 |
| 3 | input() және print() | `python-input-output` | Basics | 5 |
| 4 | if / elif / else шарттары | `python-conditions` | Basics | 6 |
| 5 | for және while циклдері | `python-loops` | Basics | 7 |
| 6 | list — Python тізімдері | `python-lists` | Arrays | 8 |
| 7 | Функциялар | `python-functions` | Basics | 9 |
| 8 | Жолдармен жұмыс | `python-strings` | Basics | 10 |
| 9 | Қарапайым алгоритмдер | `python-algorithms` | Algorithms | 11 |

Basics тақырыбындағы 1 және 2 орын бұрынғы бағдарламалау/C++ сабақтарында қалады. Жаңа Topic қажет болмады. Тізімдер сабағында массив есептері, жолдар сабағында Palindrome Check/Count Vowels, алгоритм сабағында Binary Search бар тақырыптық проекция арқылы көрінеді. Әр сабақта түсінік, қолдану себебі, синтаксис, шағын мысал, күтілетін шығыс, жиі қате және практика берілген. Қазақша мәтін тіл ауыстырғанда автоматты аударылмайды; Python синтаксисі өзгермейді. Бағаланатын есептердің толық шешімдері берілмейді.

## 3. Lesson seed қалай жұмыс істейді?

`PythonCurriculumSeeds.Create` тоғыз анықтаманы береді. `LessonSeeder` Development ортасында тұрақты Slug бойынша жоқ сабақтарды ғана қосады. Бұрынғы `python-basics` ID-і сақталып, тек белгілі ескі seed-тің Title, Summary, Content, CodeExample, CodeLanguage өрістері дәл сәйкес келсе жаңартылады. Жол соңы CRLF/LF айырмасы ғана еленбейді. Әкімші өзгерткен мәтін, рет, тақырып және жариялану күйі өздігінен қайта жазылмайды. Жаңа сабақтың TopicId+Order орны бос болмаса, бар орынды бұзбай келесі бос орын алынады; сондықтан әкімші өзгерткен ортадағы нақты рет әдепкі кестеден өзгеше болуы мүмкін. Таза seed-те рет 3–11.

Бір транзакция ішінде қосу және бірегей Slug/TopicId+Order индекстері сақталады. Қатар іске қосылатын бірнеше seed процесін орналастыру деңгейінде бір рет орындау керек; бұл жергілікті оқу жобасына арналған startup seed. Қайта қосу тарихи жіберілімдерді, AI нәтижелерін не ұпайларды өзгертпейді.

## 4. ApplicationUser.AvatarPath не үшін керек?

Nullable `AvatarPath` аватарсыз пайдаланушыны да қолдайды. Мысалы, `/uploads/avatars/<32 таңбалы GUID>.webp`. Бұл сервер жасайтын салыстырмалы жол. Профиль формасында AvatarPath өрісі жоқ; сұрауға артық өріс қосу серверге жол тағайындауға мүмкіндік бермейді.

## 5. Неге database BLOB емес?

Осы шағын орналастыруда файлды бөлек сақтау сурет беруді және дерекқор моделін жеңілдетеді. SQL тек байланысты сақтайды. Бағасы: дерекқор мен аватар бумасын бірге сақтық көшіру керек. Жергілікті файлды жоғалту логинді бұзбайды, бірақ суретті қайта жүктеу қажет болады.

## 6. AvatarService не істейді?

`Services/Profile/AvatarService.cs` нақты файл көлемін шектеп оқиды, форматты декодермен анықтайды, толық декодталуын тексереді, EXIF бағытын түзетеді, ортасынан шаршы қиып, WebP ретінде сақтайды. Бір процесс ішінде ең көбі екі сурет қатар декодталады. `ProfileService` Identity жаңартуын және ескі файлды тазалау ретін басқарады; контроллер тек пайдаланушыны анықтайды, validation қатесін аударып, нәтижені көрсетеді.

Жаңа тәуелділік — [SkiaSharp 4.153.1](https://www.nuget.org/packages/SkiaSharp/4.153.1). Бұл декодтауға және қайта кодтауға арналған кітапхана; сурет форматын тек кеңейтімнен болжаудың орнына нақты өңдеу жасайды. Windows native тәуелділігі NuGet арқылы келеді. Linux-ке орналастырғанда сол нұсқадағы Linux native assets пакетін қосып тексеру қажет.

## 7. GUID не үшін керек?

Әр жүктеуге жаңа кездейсоқ GUID беріледі. Файл аты аккаунт ID-ін, email не есімді ашпайды. `FileMode.CreateNew` бар файлды үстінен жаздырмайды. Жаңа URL браузердің ескі суретті кэштен көрсету мәселесін де шешеді.

## 8. Неге original filename қолданылмайды?

Бастапқы атауда `../`, жол бөлгіштері, HTML немесе басқа күтпеген мәтін болуы мүмкін. Ол сақтау жолына да, HTML бетке де қосылмайды. Жарамды суретке `.exe` атауы берілсе де, сервер оның декодталған кескінінен жаңа `.webp` файл жасайды; файл атауы форматқа сенім көзі емес.

## 9. MIME type неге жеткіліксіз?

Клиент `Content-Type: image/png` деп кез келген байт жібере алады. Сондықтан MIME және кеңейтім тек файл таңдау интерфейсіне көмектеседі. Қауіпсіздік шешімін SKCodec анықтаған JPEG/PNG/WebP форматы мен декодтаудың сәтті нәтижесі қабылдайды.

## 10. Image decoding не үшін керек?

Тек алғашқы байттары суретке ұқсайтын файл жеткіліксіз. Сервер өлшемді тексеріп, пиксельдерді толық оқиды; қиылған/бұзылған файл қабылданбайды. Нәтиже таза bitmap-тен қайта кодталады: бастапқы EXIF және қажетсіз метадеректер көшірілмейді. Шығыс — ең көбі 512×512 шаршы; кіші сурет жасанды үлкейтілмейді. Көп кадрлы сурет декодер арқылы анықталса қабылданбайды.

## 11. SVG неге қабылданбайды?

SVG белсенді мазмұнды қамтуы мүмкін. Бұл мүмкіндікте тек растрлық JPEG/PNG/WebP рұқсат етіледі. SVG, GIF, HTML, мәтін және орындалатын файлдар қабылданбайды. Профильдегі `img` көзі де басқарылатын жолдың қатаң үлгісінен өтуі керек; сыртқы URL, `javascript:` және `data:text/html` қолданылмайды.

## 12. Файл өлшемі қалай шектеледі?

Шек — **4 MiB = 4 194 304 байт**. `IFormFile.Length` алдын ала тексеріледі және ағын оқылғанда нақты байт саны қайта шектеледі. Multipart сұрауға шектен бөлек 64 KiB форма үстемесі қарастырылған. Ең үлкен жақ — 8192 пиксель, жалпы пиксель саны — 16 777 216. Бұл шағын қысылған файлдан өте үлкен жад бөлінуін шектейді. Reverse proxy қолданылса, оның request limit параметрі осы шекпен келісуі керек.

## 13. Аватар қалай ауыстырылады?

Реті: жаңа суретті тексеру → жаңа файлды сақтау → Identity арқылы AvatarPath-ты жаңарту → ескі файлды жою. Жарамсыз сурет бұрынғы атты/суретті өзгертпейді. Identity concurrency stamp бәсекелес жаңартудың үнсіз үстінен жазылуын тежейді. Жаңарту сәтсіз болса, жаңа файл тазартылады; DB commit нәтижесі белгісіз болса, әлі сілтенген файл жойылмайды. Файл жүйесі мен SQL арасында ортақ транзакция жоқ: процесс дәл аралықта тоқтаса, orphan файл қалуы мүмкін. Ол үшін production-та reconciliation қажет.

## 14. Ескі аватар қалай тазаланады?

`AvatarService.Delete` тек басқарылатын атауды өңдейді. Файл табылмаса, әрекет қауіпсіз аяқталады. Рұқсат/IO қатесі логқа жазылады, аккаунт жаңартуы бұзылмайды. Қалыпты ауыстыру және алып тастау HTTP тесттері физикалық ескі файлдың жойылғанын тексереді. Жаңа файлдар Git-ке қосылмайды; publish кезінде пайдаланушы бумасын үстінен ауыстырмау керек.

## 15. Path traversal деген не?

`../` арқылы рұқсат етілген каталогтан сыртқа шығуға тырысу. Жою жолы сұраудан алынбайды: сервер пайдаланушының сақталған AvatarPath-ын оқиды. Қатаң `/uploads/avatars/[a-f0-9]{32}.webp` үлгісі, `Path.GetFullPath`, каталог шекарасы және каталог/файл reparse-point тексерісі қолданылады. Файл жүйесіне жергілікті әкімші деңгейінде қолжетімді шабуылдаушыны бұл веб-функция оқшаулай алмайды; сақтау бумасына тек қолданбаға қажетті құқық берілуі тиіс.

## 16. Initials fallback қалай жұмыс істейді?

`UserAvatarViewModel.GetInitials` бос орындарды өткізіп, бірінші және соңғы сөздің алғашқы Unicode мәтін элементін алады. `Әлия Өмірбек` → `ӘӨ`, `Nurlybek` → `N`, бос атау → `?`. Құрама таңбалар `StringInfo` арқылы бөлінбей сақталады. `_UserAvatar.cshtml` барлық бетте қолданылады. Сурет жүктелмесе, сыртқы site.js оны жасырып, CSS initials-ті қалдырады. Көрінетін толық DisplayName әрқашан суреттің жанында қалады. Alt/aria-label жүйелік бөлігі аударылады.

## 17. Профиль статистикасы қайдан алынады?

`ProfileService` бар `ProgressService.GetAsync` нәтижесін қолданады. Бір есептің қайталанған Accepted жіберілімдері solved/score-ды көбейтпейді. Ұпай — Easy 100, Medium 200, Hard 300. Completed саны қолданыстағы Accepted…InternalError аралығындағы күйлерді қамтиды, SuccessRate — Accepted/Completed. Жаңа статистика формуласы енгізілген жоқ.

Соңғы бес әрекет `AsNoTracking`, `OrderByDescending(CreatedAt)`, `ThenByDescending(Id)`, `Take(5)` проекциясымен келеді. Бетте тапсырма, runtime, күй және UTC уақыты көрсетіледі. SourceCode оқылмайды. Профильде DisplayName мен аватар ғана өңделеді; email, UserId, CreatedAt, рөл және ұпай өзгертілмейді.

## 18. Leaderboard аватарды қалай алады?

`LeaderboardService.GetPageAsync` SQL проекциясында `row.User.DisplayName` және `row.User.AvatarPath` оқиды. Әр жол үшін жеке пайдаланушы сұрауы жасалмайды. 40 px дөңгелек сурет не initials көрсетіледі. Navbar сол бұрыннан оқылатын currentUser-ді пайдаланып, 28 px аватар көрсетеді. Жария рейтинг HTML-інде email, UserId және Identity құпия бағандары болмайды.

## 19. AvatarPath неге Leaderboard кестесінде жоқ?

Профильдің жалғыз дерек көзі — ApplicationUser. Көшірме баған болса, сурет ауысқанда екі жерді бірге жаңарту қажет болып, сәйкессіздік тууы мүмкін. Leaderboard кестесі тек қайта есептелетін нәтиже summary-сін сақтайды.

## 20. Admin Users қалай қорғалған?

`[Area("Admin"), Authorize(Roles = RoleNames.Admin)]` `/Admin/Users` маршрутын қорғайды. Тізім 20 пайдаланушыдан беттеледі; қауіпсіз бағандар проекциямен, рөлдер сол бет үшін бір топтық сұраумен алынады. Жеке профиль, email, рөл, тіркелу күні, solved/score және соңғы әрекеттер көрсетіледі. PasswordHash, SecurityStamp, token берілмейді. Рөл өзгерту функциясы қосылған жоқ.

## 21. Admin avatar moderation қалай жұмыс істейді?

Әкімші пайдаланушыны ID арқылы таңдайды, бірақ файл жолын жібермейді. POST әрекеті сервердегі AvatarPath-ты қолданады. Global AutoValidateAntiforgeryToken бұл әрекетті де қорғайды. Алдымен DB-де жол null болады, кейін басқарылатын файл тазартылады. GET және CSRF token-сыз POST суретті жоймайды. Студент әкімші әрекетіне өте алмайды.

## 22. Localization қалай жаңартылды?

Жаңа жүйелік сөздер Shared, Student және Admin ресурстарының kk-KZ/ru-RU/en-US нұсқаларына қосылды. Ресурс тексерісі кілттердің толықтығын, бос емес мәндерін және `{0}` орынбасарларын салыстырады. Қазір әр тілде **621 кілт**, барлығы тоғыз RESX файл. Пайдаланушы аты/email-і, код және сабақ мазмұны аударылмайды. Жүктеу өрісінің label/көмек мәтіні, батырмалар, validation, pagination және аватардың alt мәтіні аударылған.

## 23. Қандай migration жасалды?

`20261018181540_AddUserAvatar` тек `AspNetUsers.AvatarPath nvarchar(200) NULL` қосады. Ескі migration-дар өзгермеді; snapshot жаңа модельге сәйкестенді. Leaderboards схемасы өзгерген жоқ. Жергілікті базаға migration қолданылды. Алдымен migration жасалғаннан кейін қайта build жасау қажет: `--no-build` ескі assembly-ді қолданса, pending model туралы қате шығады; оны елемеуге не қорғанысты өшіруге болмайды.

## 24. Қандай тексерістер бар?

`tests/Enhancement6Checks` нақты Skia декодтауын, WebP/512 px нәтижесін, кездейсоқ атауды, жарамсыз/бос/үлкен/SVG/GIF/executable/қиылған суреттерді, traversal/сыртқы URL-ді, Unicode initials-ті және жеке уақытша SQL базасындағы seed idempotency/admin-edit сақтауын тексереді. Екі Identity контекстінің concurrency қақтығысы және әдейі істен шығарылған SQL UPDATE бұрынғы аватарды сақтап, артық файлды тазалайтыны тексерілді. Уақытша база мен UUID каталогы finally ішінде жойылады.

`scripts/verify_enhancement6.py` нақты жергілікті HTTPS/SQL арқылы үш формат жүктеуді, replacement/remove, CSRF/POST/auth, артық өрістер арқылы аккаунт ауыстыру әрекетін, Admin модерациясын, 20 жолдық pagination-ды, рейтинг құпиялығын, үш тілдегі UI-ды, статистиканы, бес соңғы әрекетті және тоғыз сабақтың байланысты есептерін тексереді. Fixture аккаунттары, жіберілімдері және аватарлары тазаланады. `--capture-state`, кейін нақты restart, `--check-state` барлық lesson/history хэштерін салыстырады.

Baseline-де build, EF model/database және 18 қолданылатын regression жиынтығы өтті. Өзгерістен кейінгі толық regression нәтижелері төмендегі тексеру мәртебесінде беріледі. Ақылы `verify_phase5_live.py --live` орындалмайды. `verify_enhancement3_migration.py` бұрынғы схемаға арналған upgrade сценарийі, қазіргі базаға rollback жасалмайды. Node UI doubles тесті нақты браузер тесті болып саналмайды.

Браузердегі визуалды тексеріс орындалған жоқ: Windows Computer Use Chrome URL-ын сенімді анықтай алмағандықтан UI жұмысын тоқтатты. 1440×900, 1024×768, 390×844 өлшемдері үшін CSS бейімделген, бірақ нақты көріністі қолмен тексеру қажет. Профильдің жиналуы, метрика карточкалары, кестенің көлденең айналуы, мәзір аватары, Tab/focus, суретсіз fallback және үш тілдегі ұзын мәтіндер — қолмен тексерілетін тармақтар.

## 25. Production-та сақтау қалай өзгерер еді?

Бір жергілікті қолданба үшін `wwwroot/uploads/avatars` жеткілікті. Бірнеше сервер болса, әрқайсысының жеке дискісі бірдей суретті көрмейді. Сондықтан S3/Azure Blob тәрізді ортақ object storage, қауіпсіз объект кілті, резервтік көшіру, orphan cleanup және қажет болса CDN керек. Бұл enhancement cloud storage қоспайды. Public аватарды жүктеген пайдаланушы оның рейтингте көрінетінін формадағы түсіндірмеден біледі.

## Тексеру мәртебесі

Тексеру күні: 2026-10-18. Соңғы `dotnet build`: **0 қате, 0 ескерту**. `dotnet ef database update`: база өзекті. `has-pending-model-changes`: өзгеріс жоқ. `git diff --check`: таза.

- Бұрынғы **18 жиынтық өтті**: resources, editor-csp, editor-ui (Node doubles), phase2/3/4/5, enhancement1/2/3/4/5a/5b, extra-tasks және Phase4Checks/Enhancement1Checks/Enhancement3Checks/Enhancement5bChecks.
- Docker қолжетімсіз болатын **үш режим өтті**: phase3, enhancement2, enhancement5b `--unavailable`. DOCKER_HOST тек уақытша app процесінде өзгертілді; Docker engine өшірілген жоқ. Қалыпты сервер қайта қосылды.
- Жаңа **екі жиынтық өтті**: Enhancement6Checks және verify_enhancement6.py. Соңғы файл тазалау өзгерісінен кейін екеуі де қайта тексерілді.
- Нақты restart салыстыруы сабақтардың, жариялану/рет мәндерінің және сол сәттегі басқа жолдардың хэштері өзгермегенін көрсетті. Enhancement migration/seed бұрынғы тарихи жолдарды өзгертпеді. Толық regression ішіндегі бұрыннан бар Admin Leaderboard Rebuild summary жолдарын қайта есептейді, сондықтан оның UpdatedAt қоса алғандағы хэші жаңарады; Submissions, ExecutionResults, AiFeedbacks және басқа тарихи дерек хэштері сақталды.
- Жүктелген тест аватарлары, fixture аккаунттары және оқшау тест базалары қалмады. `wwwroot/uploads/avatars` ішінде тек `.gitkeep` бар; нақты суреттер Git ignore арқылы жабылған. Генерацияланған зиянсыз сурет fixture-лері тек ignored `obj/` ішінде болады.
- Өзгерген C# файлдарындағы method/constructor декларацияларының алдындағы қазақша түсініктемелер тексерілді; generated migration әдістері бұл талаптан босатылады.
- Соңғы NuGet аудиті ағымдағы дереккөздер бойынша осал пакет таппады. Барлық сақталған Leaderboard сандық көрсеткіштері бастапқы жіберілім тарихынан есептелген мәндерге сәйкес келді.
- CSP/nonce, Identity/CSRF, Docker digest, оқшаулау, resource limits, hidden tests, AI-дың бөлек сұралуы және hint шектері әлсіретілген жоқ. Docker/AI орындау қызметтерінің коды өзгерген жоқ.

Логтар — ignored `obj/enhancement6-final-*.log`, `obj/enhancement6-service-checks.log`, `obj/enhancement6-http-final.log`, `obj/enhancement6-unavailable-*.log`. Нақты браузер және үш viewport тексерісі жоғарыда айтылған себеппен қолмен орындалуы тиіс; оны автоматты тест нәтижесіне қосқан жоқпыз.

Қайта restore кезіндегі NuGet аудиті SQL клиентінен келген `System.Security.Cryptography.Pkcs 9.0.11` тәуелділігіне ескерту берді. Сол major тармағындағы 9.0.21 нұсқасы тікелей бекітілді; [Microsoft advisory](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx) осы нұсқаны түзетілген деп көрсетеді. Қауіпсіздік ескертулерін басу қолданылған жоқ.

## Файлдар тізімі

### Жаңа (21)

- `Areas/Admin/Controllers/UsersController.cs`
- `Areas/Admin/Views/Users/Details.cshtml`
- `Areas/Admin/Views/Users/Index.cshtml`
- `Data/PythonCurriculumSeeds.cs`
- `Migrations/20261018181540_AddUserAvatar.Designer.cs`
- `Migrations/20261018181540_AddUserAvatar.cs`
- `Services/Profile/AdminUsersService.cs`
- `Services/Profile/AvatarService.cs`
- `Services/Profile/AvatarValidationException.cs`
- `Services/Profile/ProfileService.cs`
- `ViewModels/Account/ProfileEditViewModel.cs`
- `ViewModels/Account/UserAvatarViewModel.cs`
- `ViewModels/Admin/UsersViewModel.cs`
- `Views/Account/EditProfile.cshtml`
- `Views/Shared/_ProfileOverview.cshtml`
- `Views/Shared/_UserAvatar.cshtml`
- `scripts/verify_enhancement6.py`
- `study-enhancement6.md`
- `tests/Enhancement6Checks/Enhancement6Checks.csproj`
- `tests/Enhancement6Checks/Program.cs`
- `wwwroot/uploads/avatars/.gitkeep`

### Өзгерген (33)

- `.gitignore`
- `Areas/Admin/Views/Home/Index.cshtml`
- `Controllers/AccountController.cs`
- `Controllers/LessonsController.cs`
- `Data/ApplicationDbContext.cs`
- `Data/LessonSeeder.cs`
- `IntelligentProgrammingPlatform.csproj`
- `Migrations/ApplicationDbContextModelSnapshot.cs`
- `Models/ApplicationUser.cs`
- `Program.cs`
- `README.md`
- `Resources/AdminResource.en-US.resx`
- `Resources/AdminResource.kk-KZ.resx`
- `Resources/AdminResource.ru-RU.resx`
- `Resources/SharedResource.en-US.resx`
- `Resources/SharedResource.kk-KZ.resx`
- `Resources/SharedResource.ru-RU.resx`
- `Resources/StudentResource.en-US.resx`
- `Resources/StudentResource.kk-KZ.resx`
- `Resources/StudentResource.ru-RU.resx`
- `Services/Leaderboards/LeaderboardService.cs`
- `ViewModels/Account/ProfileViewModel.cs`
- `ViewModels/Leaderboard/LeaderboardViewModel.cs`
- `ViewModels/Lessons/LessonViewModels.cs`
- `Views/Account/Profile.cshtml`
- `Views/Leaderboard/Index.cshtml`
- `Views/Lessons/Details.cshtml`
- `Views/Lessons/Index.cshtml`
- `Views/Shared/_Layout.cshtml`
- `Views/Shared/_LoginPartial.cshtml`
- `scripts/verify_resources.py`
- `wwwroot/css/site.css`
- `wwwroot/js/site.js`

