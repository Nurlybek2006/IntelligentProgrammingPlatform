# Enhancement 5A — Сабақтар және оқу материалы

Бұл өзгеріс бұрынғы IntelligentProgrammingPlatform жобасын кеңейтеді. .NET 10, MVC, Identity, SQL Server, C++ Docker Judge, Monaco, AI Tutor, progressive hints, Custom Run, Journey, Diff, Progress, Learning Map және Leaderboard сақталды. Жаңа модульдің бағыты: **теория → мысалды түсіну → байланысты есеп → өз шешімін тексеру**.

## 1. Lessons модулі не үшін керек?

Есептер каталогы тәжірибе береді, бірақ бастаушыға түсіндірме де қажет. `/Lessons` бетінде тақырыпты таңдап, қысқа сипаттамаларды қарап, сабақты ашуға болады. Тіркелмеген адам да жарияланған материалды оқиды. Сабақтан есепке өту қолданыстағы `/Tasks/{slug}` бетіне апарады; шешім жіберу мен аккаунт ережелері өзгермейді.

Enhancement 5A күрделі LMS емес. LessonProgress, «оқып шықтым» белгісі, курсқа жазылу, ұпай немесе жаңа рейтинг қосылмады. Оқу нәтижесі туралы дерек жиналмайды; сабақ оқу бұрынғы есеп статистикасын өзгертпейді.

## 2. Lesson entity деген не?

`Models/Lesson.cs` бір оқу материалын сипаттайды:

| Өріс | Мақсаты |
|---|---|
| Id | Дерекқор тағайындайтын бүтін кілт |
| Title | Сабақ атауы; ең көбі 200 таңба |
| Slug | URL бөлігі; ең көбі 200 таңба, бірегей |
| Summary | Қысқаша сипаттама; ең көбі 1000 таңба |
| Content | Қарапайым мәтіндегі теория; форма шегі 50 000 таңба |
| CodeExample | Міндетті емес, тек көрсетілетін код; форма шегі 16 000 таңба |
| CodeLanguage | Міндетті емес `cpp`, `python` немесе `text` белгісі |
| TopicId / Topic | Сабақтың тақырыбы және navigation property |
| Order | Тақырып ішіндегі оң реттік сан |
| IsPublished | Қоғамдық қолжетімділік белгісі |
| CreatedAt | Сервер қоятын UTC уақыты |

`Content` пен `CodeExample` SQL Server-де `nvarchar(max)` болады; олардың ұзындығы Admin ViewModel арқылы шектеледі. Бұлар орындаушы командасы емес. Entity ішінде executable path, shell аргументі немесе Docker баптауы жоқ.

## 3. Topic пен Lesson байланысы қандай?

Бір Topic бірнеше Lesson ұстайды, әр Lesson бір Topic-ке тиесілі. `Topic.Lessons` коллекциясы және `ApplicationDbContext.Lessons` жиыны қосылды. FK үшін `DeleteBehavior.NoAction` қолданылады.

Әкімші тақырыпты өшіргенде алдымен ProgrammingTasks, кейін Lessons бар-жоғы тексеріледі. Сабақ бар болса, аударылған түсінікті хабарлама беріледі. Тексеру мен сақтау аралығында дерек өзгерсе де, SQL foreign key тақырыптың өшірілуіне жол бермейді. Сабақты басқа тақырыпқа көшіруге немесе қажетсіз сабақты жеке өшіруге болады.

## 4. Slug деген не?

Slug — сабақ URL-інің оқуға ыңғайлы бөлігі: `/Lessons/cpp-basics`. Формат: `^[a-z0-9]+(?:-[a-z0-9]+)*$`. Кіші латын әріптері мен цифрларға, сөздер арасында бір дефиске рұқсат бар. Бос орын, бас әріп, жол бөлгіші, басындағы/соңындағы және қатар тұрған дефистер қабылданбайды.

Форма бірегейлікті тексереді, SQL unique index қатар келген сұраныстардан қорғайды. Slug өзгерсе, ескі URL үшін автоматты redirect жасалмайды: жарияланған сілтемелерді ескеріп өзгертіңіз. SQL Server-дің ағымдағы collation ережелері lookup кезінде сақталады; енгізу формасы slug-ты қатаң тексереді.

## 5. Order не үшін керек?

Order тақырыптағы оқу ретін көрсетеді. Мысалы Basics: бағдарламалау — 1, C++ — 2, Python — 3. `TopicId + Order` жұбы бірегей; басқа тақырыптағы 1 санына рұқсат бар. Бос орындар, мысалы 1, 3, 5, жарамды. Ретті ауыстырғанда алдымен бос оң нөмірге жылжытуға болады; автоматты жаппай қайта нөмірлеу жоқ.

ViewModel `Range(1, int.MaxValue)` тексеруін, SQL `CK_Lessons_Order` шектеуін қолданады. Сондықтан HTTP формасын айналып өтсе де, нөл не теріс Order сақталмайды.

## 6. Published lesson қалай жұмыс істейді?

Қоғамдық каталог, жеке бет және алдыңғы/келесі сабақтар `IsPublished == true` шартын қолданады. Жарияланбаған сабақтың slug-ын білсе де, қоғамдық бет 404 қайтарады; Admin аккаунтымен сол қоғамдық URL-ді ашқанда да ереже өзгермейді. Жасырын сабақты тек `/Admin/Lessons/Details/{id}` көрсетеді.

Жариялау/жасыру Edit формасындағы checkbox арқылы POST-пен сақталады. Сабақтың құрылу уақыты өзгермейді. Public және Admin жауаптарында `NoStore` қолданылады. Қоғамдық тақырып фильтрінде тек жарияланған сабағы бар тақырыптар беріледі.

## 7. Public LessonsController қалай жұмыс істейді?

`Controllers/LessonsController.cs` `[AllowAnonymous]` қолданады. `Index` optional `topicId` бойынша сүзеді; каталог тақырып атауы, TopicId, Order, Id арқылы реттеледі. `Details` slug бойынша бір жарияланған сабақты іздейді. Entity-лер тікелей бетке берілмей, оқу ViewModel-деріне қажетті өрістер ғана проекцияланады; сұраныстар `AsNoTracking` және request cancellation қолданады.

`Views/Lessons/Index.cshtml` тақырып бөлімдері мен сабақ карточкаларын көрсетеді. `Details.cshtml` теорияны, міндетті емес кодты, байланысты есептерді және сабақ navigation-ын көрсетеді. Бос каталог, байланысты есеп жоқ және белгісіз slug жағдайлары бөлек қарастырылған.

## 8. Admin CRUD қалай қорғалған?

`Areas/Admin/Controllers/LessonsController.cs` Area және `[Authorize(Roles = RoleNames.Admin)]` атрибуттарын қолданады. Student пен anonymous сұраныстары әкімші деректерін алмайды. Барлық MVC POST үшін бұрынғы global `AutoValidateAntiforgeryToken` сақталды; GET өзгеріс жасамайды, Delete GET тек растау бетін ашады.

`LessonFormViewModel` тек рұқсат етілген тоғыз өрісті қабылдайды. Id және CreatedAt формаға кірмейді, жаңа уақыт `DateTime.UtcNow` арқылы серверде қойылады. Міндетті мәтіндер, ұзындықтар, slug, order, topic және тіл тексеріледі. Қате болса ModelState және тақырып тізімімен сол форма қайтады. Content пен CodeExample жолдары қырқылмай сақталады.

Бір мезеттегі өзгерістен туған unique/FK қатесі `DbUpdateErrors.IsConstraintViolation` арқылы қауіпсіз аударылған хабарламаға айналады; SQL мәтіні пайдаланушыға шықпайды. Жазба өшіріліп үлгерсе, Edit/Delete 404 қайтарады. Сабақ формаларының request шегі 512 KiB. Бұл модуль рөл беру, runner параметрі немесе AI шақыру құқығын қоспайды.

## 9. CodeExample қалай қауіпсіз көрсетіледі?

Код `<pre><code>@Model.CodeExample</code></pre>` арқылы қалыпты Razor encoding-пен беріледі. `<script>`, `</textarea>` немесе `onerror` мәтіні HTML ретінде орындалмайды. Title, Summary, Content және Topic атауы да encoded болады. `Html.Raw`, Markdown парсері және жаңа client-side кітапхана қосылмады.

Теория `white-space: pre-wrap` арқылы жолдарын сақтайды. Код блогында `tabindex="0"` және тақырыпқа сілтейтін `aria-labelledby` бар; ұзын код блогының өз ішінде scroll болады. Барлық мысалда «тек оқуға арналған» UI түсіндірмесі бар. CSP өзгермеді; Lessons беттері Monaco үшін қолданылатын style рұқсатын қоспайды.

## 10. Related Tasks қалай табылады?

Сұраныс ағымдағы Lesson.TopicId және `ProgrammingTask.IsPublished` бойынша сүзіледі. Difficulty, кейін Id бойынша реттелген алғашқы үш тапсырманың Title, Slug және Difficulty мәндері ғана алынады. TestCases мүлде сұралмайды, жасырын кіріс/жауап немесе басқа пайдаланушының шешімі оқылмайды.

Әр тапсырма бұрынғы есеп бетіне сілтейді. Байланысты жарияланған тапсырма болмаса, аударылған бос күй көрсетіледі. Бұл қарапайым байланыс Learning Map ұсыным алгоритмін, ұпай есептеуін немесе бұрынғы Progress сервисін өзгертпейді.

## 11. Previous/Next navigation қалай жұмыс істейді?

Іздеу тек бір тақырыптағы жарияланған сабақтардан жүреді. Previous — Order аз, тең болғанда Id аз жазбалардың кему ретімен біріншісі. Next — кері шарттың өсу ретімен біріншісі. Unique TopicId/Order бүгін тең ретті болдырмайды, бірақ Id тұрақты екінші кілт ретінде сақталған.

Жасырын сабақтар өткізіледі, басқа тақырыпқа автоматты өту жоқ. Бірінші сабақта Previous, соңғы сабақта Next көрсетілмейді. Сілтемеде бағытпен бірге сабақтың атауы бар, сондықтан экран оқырманы үшін де мақсаты түсінікті.

## 12. Localization қалай қосылды?

Бұрынғы үш resource тобы пайдаланылды: SharedResource — navbar, StudentResource — қоғамдық Lessons беттері, AdminResource — басқару және validation. Барлық жаңа UI `kk-KZ`, `ru-RU`, `en-US` үшін бар; culture cookie мен әдепкі қазақ тілі өзгермеді. Resource тексеруі жаңа Lessons кілттерін міндетті етеді және literal lookup, annotation, placeholder сәйкестігін тексереді.

Тоғыз resource файлда енді әр тілге **579 кілт** бар: Shared 133, Student 315, Admin 131. Дерекқордағы Title/Summary/Content аударылмайды: тіл ауыстырғанда қазақша seed мәтіні қазақша қалады. Оған арнайы бағандар немесе көшіру логикасы қосылған жоқ.

## 13. Қандай migration жасалды?

`20261002095510_AddLessons` тек Lessons кестесін, Topic FK, Slug unique index, TopicId/Order unique index және оң Order check constraint қосады. Алдыңғы кестелер өзгермейді. Migration designer және model snapshot EF құралымен жаңартылды; generated migration әдістері автоматты файлдар болып саналады. Жаңа authored C# әдістері мен конструкторларының үстінде қысқа қазақша түсіндірме бар.

```powershell
dotnet ef database update
dotnet ef migrations has-pending-model-changes
```

Миграция жергілікті базаға қолданылды. Миграция мен алғашқы seed алдында/кейін 15 бұрынғы кестенің primary-key ретімен алынған SHA-256 хештері салыстырылып, бірдей екені расталды. Құпия өрістердің мәндері шығарылмады. Миграция тарихы мен жаңа Lessons кестесі бұл салыстырудан әдейі бөлек.

## 14. Қандай seed сабақтар қосылды?

`DbSeeder` бұрынғы Development тексеруінен кейін `LessonSeeder.SeedAsync` шақырады. Production ортасында demo сабақтар автоматты қосылмайды.

| Тақырып | Реті | Сабақ | Slug |
|---|---:|---|---|
| Basics | 1 | Бағдарламалау негіздері | programming-basics |
| Basics | 2 | C++ негіздері | cpp-basics |
| Basics | 3 | Python негіздері | python-basics |
| Arrays | 1 | Массивтер негіздері | arrays-basics |
| Algorithms | 1 | Алгоритм негіздері | algorithm-basics |

Әр материалда ұғым, синтаксис, қысқа мысал, нәтижені түсіндіру және өздігінен ойланатын сұрақ бар. Related Tasks бөлігі осы тақырыптың есептеріне апарады. Мысалдар дайын judged-task шешімі емес: тұрақты мәндерді көбейту, деңгейді арттыру, есімдер тізімі, массив элементін оқу/өзгерту және сызықтық іздеу қолданылады.

Seed slug бар болса, оны өзгертпейді: әкімшінің мәтіні, жариялау күйі және уақыты сақталады. Қалаған Order бос болмаса, келесі бос оң ретті таңдайды. Қайта іске қосқанда дубль жасалмайды. Development-та demo slug өшірілсе, келесі старт оны қайта қосады; мұндай сабақты каталогтан алып тастау үшін unpublish жасаған дұрыс. Seed бір транзакцияда сақталады.

## 15. Келесі Enhancement-та Python қалай қосылады?

Қазір Python тек оқу материалының тілі. Runtime жазбасы, Python Docker image, execution endpoint немесе language selector қоспасы **жасалған жоқ**. Барлық judged/custom орындау бұрынғы C++ жолымен жүреді.

Келесі enhancement-та Python бөлек тіл ретінде жобаланады: сенімді әрі digest-пен бекітілген image, hardcoded орындау аргументтері, желісіз sandbox, уақыт/жады/PID/output шектері, cancellation/cleanup, қателерді қауіпсіз көрсету және екі тілге арналған регрессия қажет. Сабақтағы CodeLanguage runner таңдауының дереккөзі болмайды; runtime allowlist тәуелсіз қалады.

## Тексеру және қолмен қарау

Жаңа регрессия: `python scripts/verify_enhancement5a.py`. Ол нақты HTTPS cookie/antiforgery және SQL арқылы seed, anonymous/Student/Admin рұқсаты, CRUD, жариялау, 404, validation, encoding, үш тіл, filtering, navigation, related tasks, hidden tests, CreatedAt және SQL constraints тексереді. UUID fixture деректері `finally` ішінде өшіріледі. Бұл script код орындамайды және ақылы AI сұранысын жібермейді.

Бұрынғы Phase 2–5 және Enhancement 1–4 suite-тері, resource тексеруі, екі Node suite және үш C# suite сақталды. Нақты нәтиже төмендегі тексеру жазбасында беріледі. Бұрын қолданылған Enhancement 3 upgrade-only migration suite үшін базаны кері қайтару жасалмайды.

**Нақты browser viewport тексеруі әлі қажет.** Құрал браузер таппады; HTML/CSP тексеруі визуалды нәтижені дәлелдемейді. Төмендегіні әр тілде 1440×900, 1024×768, 390×844 өлшемдерінде тексеріңіз:

- Navbar: Lessons белсенді күйі, mobile мәзірі және тіл таңдауы; негізгі бетке skip-link.
- Каталог: тақырып сүзгісі сақталады, cards тар экранда бір бағанға түседі, ұзын атау/summary бет енінен шықпайды.
- Сабақ: h1 → h2 реті, теория жолдары, ұзын кодтың тек өз блогында scroll болуы, related tasks және previous/next сілтемелері.
- Tab/Shift+Tab: focus көрінеді, код блогына жетеді, мәзір мен select клавиатурадан қолжетімді; console ішінде CSP қатесі жоқ.
- Admin: барлық label, validation summary, мәтін сақталуы, publish/unpublish, delete confirmation, draft preview; кесте тар экранда өз ішінде scroll болады.
- Anonymous және Student үшін жасырын сабақ 404, Admin беті рұқсатсыз ашылмайды; тақырыпта сабақ барда өшіру түсінікті хабарламамен тоқтайды.
- Бұрынғы Monaco, Run, Submit, Journey, Diff, Progress және Leaderboard беттеріне қысқа визуалды smoke check жасаңыз.

## 2026-10-02 тексеру нәтижелері

| Тексеру | Нәтиже |
|---|---|
| Бастапқы build, EF және барлық 8 HTTP suite | PASS; өзгеріске дейін база қалыпты болды |
| Соңғы `dotnet build` | PASS — 0 қате, 0 ескерту |
| `dotnet ef database update` | PASS — AddLessons қолданылған, база жаңартылған |
| `dotnet ef migrations has-pending-model-changes` | PASS — күтілген модель өзгерісі жоқ |
| `git diff --check` | PASS — whitespace қатесі жоқ; Git-тің LF/CRLF ақпараттық ескертуі болуы мүмкін |
| Жаңа `verify_enhancement5a.py` | PASS — үш тілдегі HTTPS/SQL, CRUD, topic ауыстыру, encoding, рұқсат, навигация, constraints |
| Phase 2, 3, 4, 5 HTTP suite-тері | Барлығы PASS |
| Enhancement 1, 2, 3, 4 HTTP suite-тері | Барлығы PASS |
| Phase4Checks, Enhancement1Checks, Enhancement3Checks | Үшеуі де PASS; нақты compiler OOM/timeout тексерулері бар |
| Екі Node editor/Run/Diff suite | Екеуі де PASS; simulated DOM нақты браузер емес |
| Resource completeness | PASS — 9 файл, әр тілге 579 сәйкес кілт |
| Миграция/seed дерек сақтау | PASS — бұрынғы 15 кестенің хеші өзгермеді |
| Seed қайта іске қосу | PASS — барлық Lesson өрістері бірдей, 5 жол, дубль жоқ |
| C# түсіндірмелері | PASS — 16 жаңа authored әдіс/конструкторда қазақша comment бар |
| Ақылы AI сұраныстары | 0; бұрын сақталған offline feedback және test double қолданылды |
| Нақты browser viewport | 0; жоғарыдағы manual checklist әлі орындалуы керек |

Docker қалыпты режимде нақты компиляция, Run/Submit, sandbox шектеулері, timeout және cancellation арқылы тексерілді. Бұл өзгерісте бөлек `--unavailable` нұсқалары және бұрын қолданылған Enhancement 3 upgrade-only migration тесті қайта орындалмады. Олардың тарихи нәтижелері алдыңғы study файлдарында бар. Тесттер ортақ базаға бір уақытта fixture жазбауы үшін кезекпен жүргізілді; уақытша деректер тазаланды. Негізгі логтар ignored `obj/*enhancement5a-final.log` ішінде, жаңа suite логы `obj/verify_enhancement5a-final.log` ішінде.

## Өзгерген файлдар

Жаңа файлдар: `Models/Lesson.cs`, `ViewModels/Lessons/LessonViewModels.cs`, `ViewModels/Admin/LessonFormViewModel.cs`, екі LessonsController, `Data/LessonSeeder.cs`, екі public view, алты Admin view, AddLessons migration мен designer, `scripts/verify_enhancement5a.py`, осы оқу нұсқаулығы.

Барлығы 18 жаңа файл:

```text
Models/Lesson.cs
ViewModels/Lessons/LessonViewModels.cs
ViewModels/Admin/LessonFormViewModel.cs
Controllers/LessonsController.cs
Areas/Admin/Controllers/LessonsController.cs
Data/LessonSeeder.cs
Views/Lessons/Index.cshtml
Views/Lessons/Details.cshtml
Areas/Admin/Views/Lessons/Index.cshtml
Areas/Admin/Views/Lessons/Details.cshtml
Areas/Admin/Views/Lessons/Create.cshtml
Areas/Admin/Views/Lessons/Edit.cshtml
Areas/Admin/Views/Lessons/Delete.cshtml
Areas/Admin/Views/Lessons/_Form.cshtml
Migrations/20261002095510_AddLessons.cs
Migrations/20261002095510_AddLessons.Designer.cs
scripts/verify_enhancement5a.py
study-enhancement5a.md
```

Өзгертілген файлдар: `Models/Topic.cs`, `Data/ApplicationDbContext.cs`, `Data/DbSeeder.cs`, `Areas/Admin/Controllers/TopicsController.cs`, Admin Home, shared layout, тоғыз resource файл, EF snapshot, `wwwroot/css/site.css`, `scripts/verify_resources.py`, `README.md`.

Барлығы 19 өзгертілген файл:

```text
Models/Topic.cs
Data/ApplicationDbContext.cs
Data/DbSeeder.cs
Areas/Admin/Controllers/TopicsController.cs
Areas/Admin/Views/Home/Index.cshtml
Views/Shared/_Layout.cshtml
Resources/SharedResource.kk-KZ.resx
Resources/SharedResource.ru-RU.resx
Resources/SharedResource.en-US.resx
Resources/StudentResource.kk-KZ.resx
Resources/StudentResource.ru-RU.resx
Resources/StudentResource.en-US.resx
Resources/AdminResource.kk-KZ.resx
Resources/AdminResource.ru-RU.resx
Resources/AdminResource.en-US.resx
Migrations/ApplicationDbContextModelSnapshot.cs
wwwroot/css/site.css
scripts/verify_resources.py
README.md
```
