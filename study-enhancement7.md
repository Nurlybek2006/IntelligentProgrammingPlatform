# Enhancement 7 — сапалы код және жеке оқу ілгерілеуі

Бұл нұсқа бұрынғы ASP.NET Core MVC / .NET 10, Identity, SQL Server / EF Core, Docker, Monaco және OpenAI SDK архитектурасын кеңейтеді. Жоба қайта жасалған жоқ. Негізгі жұмыс 2026 жылғы 19 қазанда тексерілді. Құжаттағы AI талдауы — ықтимал қателері бар кеңес; формалды статикалық дәлел емес.

## 1. Enhancement 7-де не жасадық?

Accepted шешімге қолмен сұралатын AI Code Review, уақыт/жад күрделілігінің бағасы, тарихтан есептелетін XP/Level, сабақ аяқтау, есеп таңдаулылары және 365 күндік белсенділік картасы қосылды. Есеп шығару беті екі панельді жұмыс орнына айналды. Бұрынғы профиль/аватар, Admin Users, C++/Python, Custom Run, Submit, Tutor, hints, Journey, Diff, Progress және Leaderboard сақталды.

```text
Accepted Submission
      |
      +---- AI Code Review (иесі батырманы басқанда)
      |        |
      |        +---- Readability
      |        +---- Naming
      |        +---- Efficiency
      |        +---- Style
      |        +---- Time Complexity
      |        +---- Space Complexity
      |
      +---- ExperienceService
               |
               +---- Task XP
               +---- Level

Lesson
   +---- LessonProgress
             +---- 20 XP

ProgrammingTask
   +---- TaskFavorite

Submissions
   +---- Activity Heatmap
```

Оқу жолы: сабақ → есеп → таңдаулыға сақтау → код → Custom Run → Submit → қате болса Tutor/hints → түзету → Accepted → Code Review → XP/Level → Journey → профильдегі белсенділік.

## 2. Accepted AI Code Review деген не?

Бұл дұрыс жауап берген кодтың оқылымдылығын, атауларын, артық жұмысын, тиімділігін және стилін түсіндіретін шолу. `AcceptedCodeReviewsController.Analyze` сұрауды `AcceptedCodeReviewService` қызметіне береді. Қызмет иесін және аяқталған Accepted күйін тексеріп, бұрынғы шолу болса бірден қайтарады. Жаңа талдау тек пайдаланушының айқын POST сұрауымен басталады; Submit оны автоматты шақырмайды.

## 3. Ол AI Tutor-дан несімен ерекшеленеді?

Tutor қате шешімді түсінуге және келесі қадамды табуға көмектеседі; progressive hints сол кеңесті біртіндеп ашады. Code Review тесттен өткен шешімнің сапасын талдайды. Сондықтан `AiFeedback` кеңейтілмеді: жеке `AcceptedCodeReview` моделі, қызметі және view model бар. Екеуі ортақ ресми `OpenAiFeedbackClient` пен `AiRequestGate` инфрақұрылымын қолданады.

## 4. Неге тек Accepted solution талданады?

Дұрыстыққа арналған кеңес пен сапаны талдаудың мақсаты бөлек. Сервер `Status == Accepted` және `FinishedAt != null` шарттарын тексереді. Иесі емес пайдаланушы, anonymous сұрау немесе WrongAnswer нәтижесі ақылы review шақыра алмайды. Иесі `ClaimsPrincipal` арқылы анықталады; браузерден `UserId` қабылданбайды. Жаңа мүмкіндік үшін Admin bypass енгізілмеді.

## 5. Structured Output қалай жұмыс істейді?

Ресми .NET SDK Responses API-ге `strict` JSON Schema жібереді. Нәтижеде тоғыз міндетті өріс бар: `summary`, `readability`, `naming`, `efficiency`, `style`, `timeComplexity`, `spaceComplexity`, `complexityExplanation`, `suggestions`. Соңғысы дәл үш қысқа ұсыныстан тұрады. Артық өрістерге рұқсат жоқ; сервер JSON көлемін, өріс ұзындықтарын, бос мәндерді және толық бағдарламаға ұқсас жауаптарды қосымша тексереді.

Жауаптың схемасы оның мазмұны математикалық дұрыс екенін дәлелдемейді. Refusal, incomplete, қате JSON, timeout және API қатесі қауіпсіз `Unavailable` нәтижесіне айналады. Бұл мінез-құлық [OpenAI Structured Outputs құжаттамасындағы](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses) схема, refusal және толық емес жауап ұғымдарына сай тексерілді.

Trusted instruction reviewer рөлін, толық шешім бермеуді және hidden tests ашпауды анықтайды. Студент коды мен есеп мәтіні JSON ішіндегі сенімсіз дерек болып қалады. DTO тек бес өрісті сериализациялайды: тақырып, сипаттама, қиындық, сервер анықтаған бағдарламалау тілі, Accepted код. `ResponseLanguage` деректер JSON-ына кірмейді; ол trusted instruction арқылы беріледі. Тест кестелері, email, UserId, cookie, connection string және Docker мәліметтері DTO-ға қосылмайды. Бар құпия/жол тазартқышы да қолданылады.

## 6. Time Complexity деген не?

Бұл кіріс көлемі `n` артқанда алгоритм жұмысының қалай өсетінін сипаттайтын Big-O белгілеуі. Мысалы, бір рет массивті өту — әдетте `O(n)`, екі толық кірістірілген цикл — жиі `O(n²)`, тұрақты арифметикалық амалдар — `O(1)`. Нақты цикл шекаралары мен қолданылған алгоритм маңызды; сыртқы көрініс жалғыз өзі жеткіліксіз.

## 7. Space Complexity деген не?

Бұл алгоритмге қажет жадтың кіріс көлемімен өсуі. Бірнеше тұрақты айнымалы әдетте `O(1)`, `n` элементтік қосымша массив `O(n)` болады. AI түсіндірмесі кіріс сақтауын немесе қосымша жадты қалай санағанын нақтылауы мүмкін; бағалауды студент тексеруі керек.

## 8. Неге Complexity «AI estimate» деп көрсетіледі?

Модель кодты қате түсінуі, кіріс шектеулерін немесе кітапхана операциясының құнын ескермеуі мүмкін. UI-де қазақша, орысша және ағылшынша көрінетін ескерту бар: күрделілікті AI бағалайды және баға қате болуы мүмкін. Бұл ескерту талдауға дейін де, нәтижемен бірге де көрсетіледі. Толық бағдарламаны анықтайтын сүзгі — қосымша қорғаныс, барлық ықтимал модель жауабына абсолютті кепілдік емес.

## 9. Measured runtime пен Big-O айырмашылығы қандай?

Measured runtime — нақты тесттің Docker ішінде орындалған миллисекундтық уақыты. Ол процессорға, жүйе жүктемесіне, кіріске және іске қосу шығынына тәуелді. Big-O — `n` өскендегі асимптотикалық сипаттама. `17 ms` пен `O(n)` бір көрсеткіш емес. Бұрынғы өлшенген уақыт бөлек көрсетіледі, review картасы оны алмастырмайды.

## 10. AcceptedCodeReview entity не үшін керек?

Шолу submission тарихына тиесілі жеке жазба ретінде сақталады. Өрістері: `Id`, `SubmissionId`, `Model`, `Summary`, `ReadabilityFeedback`, `NamingFeedback`, `EfficiencyFeedback`, `StyleFeedback`, `TimeComplexity`, `SpaceComplexity`, `ComplexityExplanation`, `SuggestionsJson`, `CreatedAt`, nullable `InputTokens`/`OutputTokens`. `SubmissionId` unique, байланыс `Submission 1 → 0..1 AcceptedCodeReview`.

`20261018191944_AddLearningEngagementFeatures` — осы enhancement үшін жалғыз migration. Ол үш кестені, unique/FK шектеулерін және `Submissions(UserId, CreatedAt)` индексін қосады. Бұрынғы migration файлдары өзгермеді. Review submission жойылғанда cascade арқылы тазаланады; бұл қолданыстағы тарихи submission жою қорғанысын айналып өтетін жаңа endpoint бермейді.

## 11. Неге бір submission-ға бір review ғана жасалады?

Бір код үшін қайталанатын API шығынын болдырмау және алынған кеңесті сақтау үшін. Алдымен кэш, кейін ортақ process gate, SQL транзакциясындағы `sp_getapplock`, құлыптан кейін екінші кэш тексеруі қолданылады. Unique индекс соңғы дерекқор кепілдігін береді. SQL құлпы әртүрлі процесс ішіндегі бөлек gate болғанда да бір submission үшін қатар сұрау жіберуді тежейді.

SDK-де автоматты retry жоқ. Қазіргі баптау: `gpt-6-luna`, 1800 output token шегі, 45 секунд timeout; сервис timeout мәнін 15–60 секунд аралығында шектейді. Ақылы жауап алынғаннан кейін клиент ажыраса да сақтауға бөлек 10 секунд беріледі. Егер API жауап беріп, одан кейін SQL сақтау сәтсіз болса, кейінгі қолмен қайталау жаңа шығын әкелуі мүмкін: сыртқы API мен SQL арасында ортақ exactly-once транзакция жоқ. Сақталған шолу бар кезде жаңа сұрау жасалмайды. Тілді ауыстыру да оны қайта генерацияламайды.

## 12. XP System қалай жұмыс істейді?

`ExperienceService` пайдаланушының өзгертілетін XP бағанын ұстамайды. Ол Accepted submission-дардан distinct есептерді және `LessonProgress` санын есептейді:

```text
TotalXp = DistinctSolvedTaskXp + CompletedLessonXp
CompletedLessonXp = CompletedLessons × 20
```

`TotalXp` — барлық XP; `DistinctSolvedTaskXp` — әрбір бөлек шешілген есеп сыйақыларының қосындысы; `CompletedLessons` — unique аяқталған сабақ саны. Қызмет `AsNoTracking` және SQL агрегаттарын қолданады, `SourceCode` жүктемейді. Admin бетінің пайдаланушылары бірден үш сұраумен есептеледі: task XP, lesson саны, favorites саны. Әр есепке немесе әр пайдаланушыға жеке сұрау жасалмайды.

## 13. Easy/Medium/Hard қанша XP береді?

| Қиындық | Алғашқы Accepted сыйақысы |
|---|---:|
| Easy | 50 XP |
| Medium | 100 XP |
| Hard | 200 XP |

Task Details беті шешілмеген есепке сыйақыны, шешілген есепке алынған XP күйін көрсетеді. Жүйе тарихтан және есептің ағымдағы қиындығынан есептейді; қиындықты кейін өзгерту derived XP-ге де әсер етеді. Бөлек тарихи сыйақы ledger-і енгізілген жоқ.

## 14. Неге repeated Accepted қосымша XP бермейді?

`UserId + ProgrammingTaskId` бойынша distinct есептер саналады. Бір есепті бес рет немесе алдымен C++, кейін Python арқылы шешу оны екінші есепке айналдырмайды. Submission тарихы мен белсенділік артады, ал task XP бір рет қана беріледі. Custom Run XP сұрауына кірмейді.

## 15. Lesson completion неге 20 XP?

20 XP — оқу әрекетін қолдайтын осы жобаға таңдалған тұрақты ереже. Ол ең жеңіл есептің 50 XP сыйақысынан төмен. Батырманы қайта басу жаңа reward тудырмайды. Сабақты аяқтау өзіндік белгілеу екенін ескеру керек: бұл емтиханнан өту немесе теорияны меңгеруді автоматты дәлелдеу емес.

## 16. LessonProgress деген не?

`Id`, `UserId`, `LessonId`, `CompletedAt` өрістері бар аяқтау жазбасы. `(UserId, LessonId)` unique. Тек жарияланған сабаққа authenticated POST рұқсат етіледі; antiforgery міндетті. Қайталанған және қатар сұраулар бір жазбаға әкеледі.

Аяқтау **бір бағытты**: undo endpoint жоқ. Бұл оқу тарихын тұрақты ұстайды. Сабақ кейін жасырылса, аяқтау тарихы мен XP сақталады. Lesson/User FK-лары `NoAction`; әкімші completion тарихы бар сабақты жоя алмайды. GET/POST guard, қауіпсіз FK қатесін өңдеу және үш тілдегі түсінікті хабарлама бар. Favorite cascade-тері бөлек конфигурацияланған, SQL Server multiple-cascade-path мәселесі туындамайды.

## 17. Level қалай есептеледі?

`L` — кемінде 1 болатын ағымдағы деңгей. Деңгейге кіру шегі:

```text
Threshold(L) = 250 × (L − 1) × L / 2
```

`TotalXp >= Threshold(L)` орындалатын ең үлкен `L` алынады. Екілік іздеу және `decimal` аралық арифметика үлкен `long` мәндерінде дөңгелектеу/көбейту қатесін болдырмайды.

| TotalXp | Level | Осы деңгейдегі XP | Келесі деңгейге қажет толық аралық |
|---:|---:|---:|---:|
| 0 | 1 | 0 | 250 |
| 249 | 1 | 249 | 250 |
| 250 | 2 | 0 | 500 |
| 749 | 2 | 499 | 500 |
| 750 | 3 | 0 | 750 |
| 1500 | 4 | 0 | 1000 |

## 18. XP-to-next-level формуласы қандай?

```text
XpNeededForNextLevel = 250 × CurrentLevel
CurrentLevelXp = TotalXp − Threshold(CurrentLevel)
RemainingXp = XpNeededForNextLevel − CurrentLevelXp
Percentage = 100 × CurrentLevelXp / XpNeededForNextLevel
```

`XpNeededForNextLevel` — сол деңгей аралығының толық ұзындығы; оны қалған XP деп шатастырмау керек. Мысалы, Level 4 ішіндегі `420 / 1000` кезінде TotalXp = 1500 + 420 = 1920, RemainingXp = 580. Бөлгіш кемінде 250. Профильде level, total XP, progress, сабақ саны және task XP көрсетіледі; native `<progress>` элементінің accessible label-і бар.

## 19. Leaderboard Score пен XP айырмашылығы қандай?

Leaderboard — жарыстық рейтинг, бұрынғы Easy/Medium/Hard = 100/200/300 ұпай ережесін сақтайды. XP — жеке оқу дамуы, 50/100/200 және сабаққа 20. XP rank есептемейді. Learning Weakness Map-тағы StrengthScore, Practice Next және topic strength формулалары өзгерген жоқ. Admin level, XP, completed lessons, favorites санын тек оқиды; XP түзету формасы жоқ.

## 20. TaskFavorite деген не?

`TaskFavorite(Id, UserId, ProgrammingTaskId, CreatedAt)` — пайдаланушының есепті кейінге сақтауы. `(UserId, ProgrammingTaskId)` unique индексі қайталануды болдырмайды. Task/user өшірілгенде favorites cascade арқылы тазалануы мүмкін, өйткені олар бағаланған шешім тарихы емес. Бұрынғы есепті жою шектеулері сол күйінде қалады.

## 21. Favorites қалай қорғалған?

`FavoritesController` authenticated, Add/Remove POST және antiforgery арқылы орындалады. Иесі тек claims-тен алынады; басқа UserId жіберу әсер етпейді. Add тек жарияланған есепке рұқсат береді; Remove тек өз жазбасын жояды. Қайта Add/Remove қауіпсіз әрі idempotent. `returnUrl` тек жергілікті адрес болса қолданылады.

`GET /Tasks/Favorites` тек жарияланған есептерді SQL projection арқылы шығарады: тақырып, topic, difficulty, solved state, remove батырмасы. Жасырылған есептің мәтіні ашылмайды. Таңдаулы батырмасы каталогта және есеп тақырыбының жанында бар; навигация Tasks/Profile ішінде орналасқан.

## 22. Activity Heatmap қалай есептеледі?

`SubmissionActivityService` сенімді `TimeProvider` арқылы бүгінгі UTC күнін алады. Аралық: бүгіннен 364 күн бұрынғы түнгі 00:00-ден ертеңгі 00:00-ге дейін, соңғы шекара exclusive. SQL сол пайдаланушының official submission-дарын `CreatedAt.Date` бойынша топтап, жалпы және Accepted санын қайтарады. Бос күндер жадта толықтырылады: әрқашан 365 күн.

Profile реті: identity/avatar → XP/Level → stats → Coding Activity → recent activity. Әр күнде ағымдағы UI мәдениетіне сай дата, submission және Accepted саны бар `title`/`aria-label` қолданылады. UTC саясаты UI-де жазылған; сервердің жергілікті уақыты есепке араласпайды.

## 23. Custom Run неге Heatmap-қа кірмейді?

Custom Run — жеке кіріспен уақытша эксперимент. Ол `Submissions` кестесіне жазылмайды. Heatmap, XP және existing progress есептеуі official тарихқа сүйенеді. Run нәтижесі уақытша интерфейсте көрсетіледі; Submit қана жаңа әрекет ретінде сақталады.

## 24. Heatmap intensity қалай есептеледі?

| Күндегі submission саны | CSS intensity |
|---:|---:|
| 0 | 0 |
| 1 | 1 |
| 2 | 2 |
| 3–4 | 3 |
| 5 немесе көп | 4 |

Карта CSS Grid арқылы жеті жолмен салынады; chart кітапханасы немесе CDN қосылмаған. Күн ұяшықтары keyboard focus алады, түстен бөлек сандық accessible сипаттамасы бар. Шағын экранда карта өз контейнерінің ішінде көлденең айналады. Бір SQL aggregate орындалады; SourceCode, ExecutionResults және AI мәтіндері жүктелмейді. `(UserId, CreatedAt)` индексі диапазон сұрауына арналған.

## 25. Task solving workspace қалай өзгерді?

1200 px және жоғарыда problem/editor шамамен 42/58 бөлінеді. Әр панельдің өз scroll аймағы бар. 1199 px және төменде панельдер бір бағанға түседі. Editor негізгі биіктігі 55vh, mobile 50vh; минимумы desktop 360 px, mobile 320 px. Есеп тақырыбы, difficulty, XP, favorite, limits, language және Journey сілтемесі редактордан жоғары орналасады.

`TaskStatementReader` тек жеке жолдағы белгілі Input/Output/Constraints тақырыптарын бөледі; қалған plain text қалыпты Razor encoding арқылы көрсетіледі. Базадағы authored мәтін өзгермейді. Мысалдар тек visible test projection-ынан алынады. Толық тарих жұмыс бетіне жүктелмейді: бұрынғы бес қысқа әрекет collapsed Journey ішінде сақталды. Accepted Details review, journey және бар болса сол тілдегі алдыңғы шешіммен comparison ұсынады.

## 26. Monaco draft status қалай жұмыс істейді?

Existing sessionStorage оқшаулауы аккаунт, task және language бойынша сақталды. Код өзгергенде draft жазылады; сәтті сақтау немесе storage қолжетімсіздігі бөлек localized статуспен көрсетіледі. Тілді ауыстыру сол тілдің draft-ын қайтарады. Draft сервердегі submission емес, осы браузер tab-ындағы уақытша жұмыс; оны тұрақты backup деп түсінуге болмайды.

## 27. Reset Starter Code қалай жұмыс істейді?

`Reset starter code` ағымдағы тілдің starter мәтінін қояр алдында localized confirmation сұрайды. Cancel draft-ты өзгертпейді. Confirm тек осы тілдің editor/model және draft күйін қайтарады; басқа тілдің draft-ы сақталады. Run/Submit орындалып жатқанда reset бұғатталады.

## 28. Keyboard shortcuts қалай жұмыс істейді?

Monaco host ішіндегі Ctrl/Cmd+Enter — Run, Ctrl/Cmd+Shift+Enter — Submit. Listener editor-ға шектелген, сондықтан Custom Input немесе басқа формада Enter-ді тартып алмайды. Alt/repeat және busy күйі ескерілмейді. Run/Submit ортақ `form.dataset.busy` guard қолданады; екі батырма мен reset қайталанған әрекетке жол бермейді. Submit label/progress және Run label/result өз localized күйін көрсетеді; `pageshow` back-forward cache жағдайында басқаруды қалпына келтіреді.

## 29. CSP жаңа JavaScript-пен қалай сақталды?

Жаңа логика жергілікті `ClientScripts/code-editor.js` пен `wwwroot/js/custom-run.js` ішінде. Editor bundle `npm run build:editor` арқылы жаңартылды. Script `unsafe-inline`, `unsafe-eval`, жаңа CDN немесе сыртқы worker қосылған жоқ. Existing nonce және Monaco-ға ғана арналған style саясаты сақталды.

AI output, task text және profile мәтіндері Razor арқылы кодталады; user/AI output үшін `Html.Raw` қолданылмайды. Custom Run DOM мәтіні `textContent` арқылы жазылады. Favorite, Lesson completion және Code Review state өзгертетін GET бермейді. AI қатесі Accepted мәртебесін өзгертпейді, raw exception UI-ге шықпайды. Docker digest, non-root, no-network, read-only filesystem, ресурстық лимиттер мен cleanup өзгермеді.

## 30. Қандай regression/security tests орындалды?

Өзгеріске дейін барлық 20 existing suite өтті. Соңғы іске қосуда да сол 20 suite өтті: resources; editor CSP; editor/custom-run UI; Phase 2, 3, 4, 5 HTTP; Enhancement 1, 2, 3, 4, 5A, 5B HTTP; extra tasks; C# Phase4Checks, Enhancement1Checks, Enhancement3Checks, Enhancement5bChecks, Enhancement6Checks; Enhancement 6 HTTP.

Жаңа `tests/Enhancement7Checks` бөлек UUID SQL базасын жасап, соңында жойды. XP reward/duplicate/cross-language, 249/250/749/750/1500 шекаралары мен `long.MaxValue`, lesson unique/FK guard, favorite owner/unpublished/concurrency, SQL query саны және 365 UTC/intensity/Accepted aggregate тексерілді. Review DTO өрістері, trusted language, prompt boundary, sanitization, ownership, non-Accepted, cache, cross-process gate кезіндегі SQL lock, malformed/oversized/full-program response, cancellation және failure тексерілді. Official SDK transport жергілікті handler-мен strict schema, token/model, store/tools параметрлерін, refusal/incomplete/401/429 және retry жоқтығын тексерді. Бұл suite paid API қолданбайды.

`scripts/verify_enhancement7.py` нақты HTTPS, SQL және Docker арқылы CSRF/Auth/ownership, forged UserId, safe redirect, repeat favorite/completion, unpublished content, Admin lesson delete guard, Run/Accepted XP/activity, no automatic review, saved-review XSS/cache/culture және Admin read-only көрсеткіштерін тексерді. Default режимі — нөл paid call. Fixture-лер `finally` ішінде тазаланады.

Нақты AI тексерісі **бір рет** орындалды: `gpt-6-luna`, **539 input / 567 output tokens**, time **O(1)**, space **O(1)**. Қазақша valid review үш ұсыныспен сақталды; толық rewritten бағдарлама қайтпады. Duplicate POST және English-ке ауысу сақталған review-ді өзгертпей қолданды. Нақты шақыру саны **1**. Fixture submission/review жойылды; ignored `obj/enhancement7-live-result.json` ledger-ін өшіріп қайта тест жүргізуге болмайды.

Тексеріс барысында екі қажетті түзету жасалды: native XP progress-ке accessible label берілді; extra-tasks тесті жаңа бөлімдерге бөлінген шарттың барлық мәтіні мен ретін тексеруге бейімделді. SQL уақытша кідірісі кезінде дерек өшірілген жоқ, SQL service қайта іске қосылған жоқ; соңында күткен regression сәтті аяқталды.

### Нақты браузерде тексерілген шекара

Windows Chrome нақты қолданылды, responsive emulation өлшемдері: **1440×900**, **1024×768**, **390×844**. Ағылшын UI-де C++ Monaco highlighting, draft status, toolbar/reset, favorite, readable examples және reachable Run/Submit визуалды тексерілді. Desktop екі панель, laptop/mobile бір баған көрсетті. Mobile Custom Input қолжетімді; DOM өлшемі `innerWidth=390`, `innerHeight=844`, `scrollWidth=390`, editor height `422` px болды. Консольде app/CSP error байқалмады; DevTools бір ықтимал improvement issue көрсетті, ол толық аудиттен өткізілген жоқ.

Қалыпты desktop Chrome терезесінде Profile де ашылды: бұрынғы аватар/identity, XP/Level progress, stats және 365 күндік бос heatmap реті көзбен тексерілді. Қолданыстағы нақты аккаунтпен жаңа submission/favorite/completion немесе paid AI әрекеті жасалған жоқ. Сондықтан браузердегі барлық әрекетті end-to-end тексердік деп айтпаймыз: Run/Submit, Python switching, draft/reset/shortcut және duplicate guards автоматты service/HTTP/DOM тесттерімен тексерілді. Қалған manual жұмыс: осы әрекеттердің толық keyboard/pointer тізбегін disposable аккаунтпен KZ/RU/EN-де жүргізу, screen reader тексерісі, Profile-дің кішірек экрандары және saved review карталарын барлық үш өлшемде көзбен қарау. Жаңа review үшін қайта ақылы сұрау қажет емес — сақталған fixture қолдануға болады.

### Соңғы командалар және тазалау

`dotnet build`: **0 warning, 0 error**. `dotnet ef database update --no-build`: база **up to date**. `dotnet ef migrations has-pending-model-changes --no-build`: **pending change жоқ**. `git diff --check`: өтті. `verify_resources.py`: әр тілде **681 key** (Shared 213, Student 319, Admin 149), parity және динамикалық key coverage сақталды.

Қосымша үш failure-path тексерісі де өтті: `verify_phase3.py --unavailable`, `verify_enhancement2.py --unavailable`, `verify_enhancement5b.py --unavailable`. Тек app процесінің `DOCKER_HOST` айнымалысы жоқ test pipe-қа бағытталды; Docker қызметі өшірілген жоқ. Соңында қалыпты Development app қайта іске қосылды.

Baseline/final SHA-256 салыстыруы tasks, tests, topics, lessons, runtimes, submissions, execution results, AI feedback және пайдаланушы ID/CreatedAt мәндері өзгермегенін растады. Existing leaderboard rebuild тексерісі summary-дің `UpdatedAt` мәнін жаңартады; соңғы ұпайлар мен санағыштар бұрынғы формула бойынша тарихқа сәйкес екені бөлек тексерілді. Enhancement 7 test users/tasks/lessons/topics, үш жаңа кестедегі fixture rows, жеке SQL test databases, runner containers және temporary workspaces саны — **0**. Нақты пайдаланушы деректері жойылған жоқ.

Жергілікті evidence файлдары ignored `obj/` ішінде: `enhancement7-final-results.json`, `enhancement7-final-remainder-results.json`, `enhancement7-final-tail-results.json` (алғашқы сәтсіз әрекеттерді де сақтайды), `enhancement7-service-checks.log`, `enhancement7-http-checks.log`, `enhancement7-live-result.json`, `enhancement7-final-build.log`, `enhancement7-final-database.log`, `enhancement7-final-model.log`, `enhancement7-final-data-audit.json` және `enhancement7-unavailable-*.log`.

Құпиялар source/docs/log-қа көшірілген жоқ. Әр жаңа C# method/constructor алдында қысқа қазақша purpose comment бар; generated migration methods-ке қолмен comment қосылмады. Қолданыстағы Auth, antiforgery, ownership, hidden-test, CSP және Docker шектеулері әлсіретілген жоқ.

### Файлдар тізімі

**Жасалған файлдар: 29**

- `Controllers/AcceptedCodeReviewsController.cs`
- `Controllers/FavoritesController.cs`
- `Controllers/LessonCompletionsController.cs`
- `Migrations/20261018191944_AddLearningEngagementFeatures.Designer.cs`
- `Migrations/20261018191944_AddLearningEngagementFeatures.cs`
- `Models/AcceptedCodeReview.cs`
- `Models/LessonProgress.cs`
- `Models/TaskFavorite.cs`
- `Services/AI/AcceptedCodeReviewContent.cs`
- `Services/AI/AcceptedCodeReviewInput.cs`
- `Services/AI/AcceptedCodeReviewService.cs`
- `Services/Progress/ExperienceService.cs`
- `Services/Progress/LessonProgressService.cs`
- `Services/Progress/SubmissionActivityService.cs`
- `Services/Submissions/TaskFavoriteService.cs`
- `Services/Submissions/TaskStatementReader.cs`
- `ViewModels/Progress/ExperienceViewModel.cs`
- `ViewModels/Progress/SubmissionActivityViewModel.cs`
- `ViewModels/Submissions/AcceptedCodeReviewViewModel.cs`
- `ViewModels/Tasks/FavoriteButtonViewModel.cs`
- `Views/Favorites/Index.cshtml`
- `Views/Shared/_Experience.cshtml`
- `Views/Shared/_FavoriteButton.cshtml`
- `Views/Shared/_SubmissionActivity.cshtml`
- `Views/Submissions/_AcceptedCodeReview.cshtml`
- `scripts/verify_enhancement7.py`
- `study-enhancement7.md`
- `tests/Enhancement7Checks/Enhancement7Checks.csproj`
- `tests/Enhancement7Checks/Program.cs`

**Өзгертілген файлдар: 42**

- `Areas/Admin/Controllers/LessonsController.cs`
- `Areas/Admin/Views/Lessons/Delete.cshtml`
- `Areas/Admin/Views/Users/Index.cshtml`
- `ClientScripts/code-editor.js`
- `Controllers/LessonsController.cs`
- `Controllers/SubmissionsController.cs`
- `Controllers/TasksController.cs`
- `Data/ApplicationDbContext.cs`
- `Migrations/ApplicationDbContextModelSnapshot.cs`
- `Models/Submission.cs`
- `Program.cs`
- `README.md`
- `Resources/AdminResource.en-US.resx`
- `Resources/AdminResource.kk-KZ.resx`
- `Resources/AdminResource.ru-RU.resx`
- `Resources/SharedResource.en-US.resx`
- `Resources/SharedResource.kk-KZ.resx`
- `Resources/SharedResource.ru-RU.resx`
- `Services/AI/OpenAiFeedbackClient.cs`
- `Services/Profile/AdminUsersService.cs`
- `Services/Profile/ProfileService.cs`
- `Services/Submissions/TaskPageService.cs`
- `ViewModels/Account/ProfileViewModel.cs`
- `ViewModels/Admin/UsersViewModel.cs`
- `ViewModels/Lessons/LessonViewModels.cs`
- `ViewModels/Submissions/SubmissionDetailsViewModel.cs`
- `ViewModels/Tasks/TaskCatalogViewModel.cs`
- `ViewModels/Tasks/TaskDetailsViewModel.cs`
- `Views/Account/Profile.cshtml`
- `Views/Lessons/Details.cshtml`
- `Views/Shared/_ProfileOverview.cshtml`
- `Views/Submissions/Details.cshtml`
- `Views/Tasks/Details.cshtml`
- `Views/Tasks/Index.cshtml`
- `Views/Tasks/_SubmissionForm.cshtml`
- `scripts/verify_editor_csp.mjs`
- `scripts/verify_extra_tasks.py`
- `scripts/verify_resources.py`
- `tests/Enhancement6Checks/Program.cs`
- `wwwroot/css/site.css`
- `wwwroot/js/custom-run.js`
- `wwwroot/js/editor/code-editor.js`
