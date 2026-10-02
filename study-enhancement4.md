# Enhancement 4 — үш тіл, соңғы әрлеу және қорытынды аудит

Бұл кезең қолданыстағы ASP.NET Core MVC жобасын аяқтауға арналған. Identity, SQL Server, EF Core, Docker, Monaco, AI Tutor және бұрынғы оқу мүмкіндіктері сақталады. Жаңа frontend framework, бағдарламалау тілі немесе жеке microservice қосылмайды.

**2026-10-02 күнгі тексеру күйі:** baseline және қорытынды автоматты тексерулер өтті. Build: 0 қате, 0 ескерту; дерекқор жаңартылған, жаңа миграция жоқ. Нақты браузер қолжетімсіз: UI құралындағы apps/browsers тізімі бос, `iab` ашу әрекеті `Browser is not available: iab` қатесін қайтарды. Нақты тексерілген viewport саны — **0**, визуалды тексерілген тіл саны — **0**. Бұл шектеуді HTTP немесе JavaScript mock тестімен алмастырып, «браузерде өтті» деп жазуға болмайды.

## 1. Enhancement 4-де не жасадық?

Интерфейс қазақ, орыс және ағылшын тілдеріне ASP.NET Core localization арқылы ауыстырылады. Навигация, бет тақырыптары, батырмалар, тексеру хабарлары, мәртебелер және Admin интерфейсі ресурстардан алынады. Оқу картасының логикасы ағылшын мәтінінен тәуелсіз `TopicStrengthLevel` enum-ына көшірілді. AI жаңа талдауды ағымдағы интерфейс тілінде сұрайды.

Финал әрлеу бұрынғы қараңғы developer стилін сақтайды. Run — қосымша әрекет, Submit — негізгі әрекет; кеңес ашу батырмасы талдау жасаудан бөлек. Delete қауіпті әрекет ретінде ерекшеленеді. Шынайы визуалды нәтиже браузер қолжетімді болғанда [manual-enhancement4-checklist.md](manual-enhancement4-checklist.md) арқылы тексеріледі.

## 2. Localization деген не?

Localization — қолданбаның жүйелік мәтіндерін пайдаланушы таңдаған тілге бейімдеу. Бұл тапсырма мазмұнын автоматты аудару емес. Бір `Nav_Tasks` кілті үш ресурста үш мәтін береді, ал `/Tasks` маршруты мен контроллердің әрекеті өзгермейді.

Мәтін Razor бетіне encoded түрде беріледі. Аудармаға HTML кодын немесе JavaScript бағдарламасын енгізудің қажеті жоқ. Бұл XSS қорғанысын және CSP ережелерін сақтауға көмектеседі.

## 3. Culture деген не?

`CultureInfo.CurrentCulture` сан, пайыз және күн пішіміне әсер етеді. `CultureInfo.CurrentUICulture` ресурс таңдауға жауап береді. Request Localization Middleware осы екеуін ағымдағы HTTP сұрауына орнатады; басқа пайдаланушының таңдауын өзгертпейді.

Сандық есептеу тілге тәуелді емес. Мысалы, оқу күшінің 47 ұпайы барлық тілде 47 болып қалады; экрандағы ондық бөлгіш қана culture талабына сай көрсетілуі мүмкін. Техникалық `data-*` мәндері қажет жерде invariant форматта сақталады.

## 4. kk-KZ, ru-RU, en-US нені білдіреді?

| Culture | Интерфейс тілі | HTML тілі | AI жауабы |
|---|---|---|---|
| `kk-KZ` | Қазақша | `kk` | Kazakh |
| `ru-RU` | Русский | `ru` | Russian |
| `en-US` | English | `en` | English |

Әдепкі culture — `kk-KZ`. Қолдау көрсетілетін тізім дәл осы үш мәннен тұрады. `Student`, `Admin`, `cpp`, claim атаулары, маршруттар және enum мәндері аударылмайды.

## 5. Resource (.resx) деген не?

`.resx` — тұрақты кілт пен оған сәйкес мәтінді сақтайтын .NET ресурсы. `Resources` ішінде SharedResource, StudentResource және AdminResource топтарының әр тілге арналған файлдары қолданылады. Shared ресурсы ортақ батырма, мәртебе, difficulty және validation мәтіндерін береді; қалған екеуі өз интерфейс аймағының мәтіндерін жинайды.

Мысалы, StudentResource ішіндегі `Submit_Button` кілтінің ішкі атауы өзгермейді, ал мәні culture-ға сай ауысады. Ресурс файлдары компиляция кезінде .NET assembly ресурстарына айналады; пайдаланушыға үлкен JavaScript сөздігі жіберілмейді.

## 6. IStringLocalizer деген не?

`IStringLocalizer<SharedResource>` ағымдағы UI culture бойынша мәтінді табады. Marker class ресурс тобының орнын анықтайды; онда жеке аударма логикасы болмайды. Razor не презентацияға жауап беретін контроллер ресурстың кілтін сұрайды.

Формат параметрлері үшін, мысалы, әрекет саны немесе тапсырма атауы үшін `{0}` сияқты орынбасарлар қолданылады. Пайдаланушы немесе Admin енгізген мәндер бұрынғыдай HTML encoding арқылы шығарылады. Есептеу қызметі экранға арналған аударма сөйлемін құрастырмайды.

## 7. RequestLocalizationMiddleware қалай жұмыс істейді?

`Program.cs` ішінде `AddLocalization` ресурстар каталогын тіркейді. `RequestLocalizationOptions` қолдау көрсетілетін culture тізімін және әдепкі қазақ тілін анықтайды. Provider ретінде `CookieRequestCultureProvider` қолданылады.

`UseRequestLocalization` MVC және қате беттері көрсетілер алдында іске қосылады. Cookie болмаса қазақ тілі қолданылады. Қалыпты интерфейс таңдауы cookie арқылы жасалады; query string немесе браузердің Accept-Language тақырыбы тұрақты таңдауды үнсіз ауыстырмайды. `Content-Language` тақырыбы ағымдағы culture-ды көрсетеді.

## 8. Culture cookie деген не?

ASP.NET Core-дың стандартты `.AspNetCore.Culture` cookie-і `c=kk-KZ|uic=kk-KZ` тәрізді форматта culture таңдауын сақтайды. Бұл Identity authentication cookie-інен бөлек; тіл ауыстыру пайдаланушыны жүйеден шығармауы керек.

Culture cookie `Secure`, `HttpOnly`, `SameSite=Lax` параметрлерімен жазылады. Ол HTTPS арқылы жіберіледі, JavaScript оны оқымайды. Мерзімі бір жыл; Path қолданбаның жолына сәйкес беріледі. Cookie ішінде API кілті, пароль, source code немесе feedback сақталмайды.

## 9. Тілді ауыстыру endpoint қалай қорғалған?

`POST /Culture/Set` қалыпты Razor form арқылы шақырылады. Жалпы MVC antiforgery filter POST сұрауын тексереді. `kk-KZ`, `ru-RU`, `en-US` мәндері ғана қабылданады; еркін culture аты немесе қолдан жасалған ресурс жолы қабылданбайды.

Кіру бетінде де тіл таңдау керек болғандықтан endpoint анонимді пайдаланушыға қолжетімді. Ол аккаунт, тапсырма немесе submission өзгертпейді; тек интерфейс cookie-ін жазады. Жауап кэшке сақталмайды.

## 10. Open redirect деген не және қалай қорғалдық?

Open redirect — сайттың өз endpoint-і арқылы пайдаланушыны бөгде доменге бағыттау қатесі. Сондықтан `returnUrl` сенімді деп қабылданбайды. `Url.IsLocalUrl` тексеруі және `LocalRedirect` қолданылады.

`/Tasks/example` сияқты жергілікті жолға қайтуға болады. Бөгде `https://...` URL, `//...` схемасыз сыртқы жол немесе жарамсыз мән келгенде қолданбаның басты беті таңдалады. Бұл тіл ауыстыру формасының қауіпсіздігін сақтайды.

## 11. html lang не үшін керек?

`<html lang="kk">`, `lang="ru"`, `lang="en"` мәндері ағымдағы culture-дан алынады. Экраннан оқу бағдарламасы мәтінді дұрыс дыбыстау үшін осы белгіні қолданады. Page title де негізгі беттерде аударылады.

Бұл белгі мәтінді өзі аудармайды. Нақты аударма `.resx` арқылы орындалады. Monaco-ның платформадан берілетін редактор және екі diff жағының accessible label мәтіндері де ресурс атрибуттарынан алынады.

## 12. Неге database content автоматты аударылмайды?

Task Title, Description, Topic Name, Topic Description, студент source code-ы, custom input/output, compiler output және бұрын сақталған AI feedback бастапқы қалпында сақталады. Оларды автоматты аудару кодтың, тесттің немесе тапсырманың мағынасын өзгертуі мүмкін.

Үш тілге арнап жаңа DB бағандары енгізілмейді. Localization дерек моделінің құрылымын өзгертпеуі тиіс. Тіл ауыстыру тек келесі сұраудың интерфейс көрінісіне әсер етеді.

## 13. Неге enum мәндерін аудармаймыз?

`SubmissionStatus.Accepted`, `Difficulty.Easy` сияқты мәндер есептеу, дерекқор және қауіпсіздік тексерулері үшін тұрақты идентификаторлар болып қалады. Оларды аудару сақталған деректермен сәйкестікті бұзуы мүмкін.

Презентация қабаты enum-ды ресурс кілтіне айналдырады. Аударылған мәтінді қайтадан enum-ға түрлендірмейміз. Сол сияқты `Student` пен `Admin` role атаулары және runtime-ның `cpp` кілті барлық culture-да өзгермейді.

## 14. Status UI қалай аударылады?

`Status_Accepted`, `Status_WrongAnswer`, `Status_CompilationError` және басқа тұрақты кілттер мәртебенің көрінетін мәтінін береді. Submissions, Details, Journey, Home және Progress бірдей ортақ mapping қолданады. Белгінің түсімен бірге мәртебе мәтіні де бар.

Custom Run JSON нәтижесіндегі enum атауы өзгермейді. JavaScript сол күйге сәйкес серверден алынған localized label-ды көрсетеді; CSS class тек рұқсат етілген күйлерден таңдалады. Бағдарлама output-ы және диагностика `textContent` арқылы беріледі.

## 15. Difficulty қалай аударылады?

Ішкі `Easy`, `Medium`, `Hard` мәндері сол күйінде қалады. Интерфейсте `Difficulty_Easy`, `Difficulty_Medium`, `Difficulty_Hard` ресурстары пайдаланылады. Admin select option-ының value мәні өзгермейді; тек пайдаланушы көретін атау аударылады.

Есепті difficulty бойынша таңдау, рейтинг ұпайы және Practice Next реті аудармаға тәуелді емес. Admin енгізген Topic немесе Task атауы аударма кілті ретінде пайдаланылмайды.

## 16. AI Tutor тілін қалай таңдайды?

`OpenAiTutorService` сұраудың `CurrentUICulture.Name` мәнінен `AiResponseLanguage` enum-ын таңдайды. `ru-RU` — Russian, `en-US` — English, қалған қауіпсіз default — Kazakh. Әдеттегі HTTP ағыны үш қолдау көрсетілетін culture-мен шектелген.

`AiTutorInput.ResponseLanguage` — сервер контексті; `[JsonIgnore]` арқылы студент деректері бар JSON-ға кірмейді. `OpenAiFeedbackClient.BuildInstructions` enum-нан тек үш бекітілген тіл атауының бірін алады да, сенімді нұсқауға қосады. Source code немесе task description ішіндегі «басқа тілде жауап бер» мәтіні сенімсіз дерек болып қалады.

Модельден summary, explanation және үш hint таңдалған тілде сұралады. JSON schema property атаулары, errorCategory кодтары және code identifiers өзгермейді. Prompt boundary offline transport арқылы тексеріледі; бұл нақты модельдің барлық сұрауда міндетті түрде дұрыс тіл қолданатынын дәлелдемейді. Осы enhancement тексерулерінде ақылы OpenAI сұрауы жасалмайды.

## 17. Неге ескі AI feedback қайта аударылмайды?

Сақталған feedback өзінің бастапқы тілінде көрсетіледі. `AnalyzeAsync` feedback бар екенін OpenAI клиентін шақырудан бұрын тексереді. Culture ауыстыру, бет ашу немесе келесі hint-ті көрсету жаңа analysis жасамайды.

Сондықтан тіл ауыстырғаннан кейін AI мәтіні интерфейс тілінен өзгеше болуы мүмкін; бұл сақталған пайдаланушы дерегінің әдейі сақталуы. Hint heading және батырмалар аударылады, ал әлі ашылмаған hint мәтіні Razor model-ге де, HTML-ге де берілмейді.

## 18. TopicStrengthLevel не үшін енгізілді?

Бұрын оқу күшінің ағылшын label мәтіні PracticePriority есептеуіне қатысқан. Енді `TopicStrengthLevel` enum-ының `NotStarted`, `Exploring`, `NeedsPractice`, `Developing`, `Strong` мәндері пайдаланылады. Бұл ViewModel/есептеу деңгейіндегі түр; EF entity немесе DB бағаны емес.

Формула өзгермейді: `StrengthScore = (CompletionRate × 0.70 + SubmissionSuccessRate × 0.30) × 100`, нәтиже 0–100 аралығына шектеледі. 0 әрекет — NotStarted; 1–2 әрекет — Exploring; кейін ≥75 — Strong, ≥45 — Developing, қалғаны — NeedsPractice. `2/4` орындалу және `2/5` accepted үлесі мысалында ұпай **47**.

## 19. Неге business logic UI string-ке тәуелді болмауы керек?

Аудармашы «Needs practice» мәтінін өзгертсе де, Practice Next ұсынымының реті өзгермеуі тиіс. Сондықтан басымдық enum арқылы есептеледі: NeedsPractice → Developing → Exploring → NotStarted → Strong; одан кейін төменгі StrengthScore және TopicId қолданылады. Тақырыптың ішінде ең оңай шешілмеген жарияланған есеп, одан кейін TaskId таңдалады.

Қате профилі де тұрақты `Compilation`, `WrongAnswer`, `Runtime`, `TimeLimit`, `MemoryLimit` кодтарын береді. Ең жиі қате жоқ болса `None` қайтады. Ұсыныс қызметі ағылшын сөйлемінің орнына Level, SolvedTasks және PublishedTasks метадеректерін береді; сөйлемді view аударады. InternalError және уақытша Custom Run оқу күшіне кірмейді.

## 20. CSP localization-дан кейін қалай сақталды?

Тіл ауыстырғыш JavaScriptсіз form POST қолданады. Редактор мен Run-ның шағын мәтіндері encoded `data-*` атрибуттарына қойылып, жергілікті external JavaScript арқылы оқылады. `unsafe-eval` немесе жалпы inline script рұқсаты қосылмайды.

Бұрынғы request nonce, Monaco stylesheet nonce patch, same-origin worker және editor бар беттерге ғана арналған style attribute ерекшелігі сақталады. `textContent` бағдарламаның output-ын HTML ретінде орындамайды. HTTP CSP тақырыбы мен Node nonce тесті нақты браузер console тексеруінен бөлек дәлел ретінде көрсетіледі.

## 21. Responsive design қалай тексерілді?

Қолмен тексеруге арналған өлшемдер: **1440×900**, **1024×768**, **390×844**. Әр өлшемде үш тілдің навигациясы, ұзын батырмалар, task/editor орналасуы, Run нәтижесі, Learning Map, Journey және Diff қаралады.

Осы ортада нақты браузер ашылмады, сондықтан responsive көрініс визуалды түрде расталған жоқ. Monaco-ның automatic layout және тар экранға арналған inline diff параметрлері Node doubles арқылы тексеріледі; олар whole-page overflow жоқ екенін дәлелдей алмайды. Қалған жұмыс manual checklist-те ашық белгіленген.

## 22. Accessibility қалай тексерілді?

Source code, custom input, runtime және тіл таңдау control-дарына label беріледі. Form және button сияқты semantic HTML қолданылады. Мәртебе тек түспен берілмейді; table header, progress label және негізгі навигацияның ағымдағы бет белгісі тексеріледі.

Код/HTML аудиті бар болғанымен, нақты keyboard tab реті, focus көрінісі, экраннан оқу және mobile navbar әрекеті браузерде тексерілуі керек. Оларды статикалық аудит нәтижесімен толық өтті деп жарияламаймыз.

## 23. Monaco үш тілде қалай тексерілді?

Жергілікті редактор JavaScript-і C++ source-ты бұрынғы textarea мен model арасында синхрондайды; culture оны аудармайды. Draft key бұрынғы user/task шекарасын сақтайды. Submit validation және compiling мәтіні сервер локализациясынан алынады. Diff original/modified model-дері read-only күйінде қалады.

Node тексерулері kk/ru/en accessible labels, validation/progress мәтіні, draft/validation басымдығы, source-sync, local worker URL, diff disposal және responsive diff параметрлерін тексереді. Run doubles сан пішімін, POST token-ді, duplicate click қорғанысын және hostile output-тың text-only берілуін тексереді. Нақты теру, highlighting, worker startup және rendered diff браузер болмағандықтан расталған жоқ.

## 24. Browser visual test қалай орындалды?

Алдымен UI құралының қолжетімді surfaces тізімі сұралды: apps және browsers бос болды. Одан кейін `iab` арқылы жергілікті сайтты ашу талпынысы browser unavailable қатесімен аяқталды. Сондықтан screenshot түсірілмеді және бірде-бір бетті визуалды тексердік деп есептемейміз.

| Тексеру | Нақты нәтиже |
|---|---|
| Browser automation | Қолжетімсіз |
| Визуалды viewport | 0 |
| Визуалды тіл | 0 |
| Browser JavaScript/CSP console | Тексерілмеді |
| Monaco typing/worker/diff rendering | Тексерілмеді |
| 390×844 overflow/navbar | Тексерілмеді |

HTTP smoke тесттері page status, ресурстар, encoding, cookie және авторизацияны тексере алады. Олар screenshot немесе browser interaction емес. Manual checklist осы айырмашылықты сақтайды.

## 25. Resource completeness қалай тексерілді?

`python scripts/verify_resources.py` әр marker тобы үшін kk-KZ, ru-RU және en-US кілттерін салыстырды. Бір тілде кілт жоқ болса, duplicate key болса немесе міндетті мәтін бос болса тексеру сәтсіз аяқталады. Format орынбасарлары мен кодтағы тұрақты resource сілтемелері де тексерілді. Уақытша көшірмелердегі бір кілтті әр тілден бөлек алып тастағанда тексеру дұрыс сәтсіз аяқталды; негізгі ресурстар өзгерген жоқ.

Қалыпты .NET fallback production-да жұмыс істей алады, бірақ тест жоқ аударманы fallback арқылы жасырмайды. HTTP smoke үш тілдегі тексерілген беттер мен атрибуттарда raw resource key жоқ екенін растады. **Нәтиже: PASS, 9 .resx файл, әр тілде 526 кілт: SharedResource — 132, StudentResource — 294, AdminResource — 100. Барлығы 1578 аударма мәні.**

## 26. Final security regression нәтижесі қандай?

Өзгеріске дейін build, EF model/database күйі, Docker және қолданыстағы regression baseline сәтті өтті. Localization аяқталған соң төмендегі қорытынды нәтижелер нақты қайта тексерілді.

| Қорытынды тексеру | Нәтиже |
|---|---|
| `dotnet build`, errors/warnings | PASS: 0 қате, 0 ескерту |
| EF pending model / database update | PASS: модель өзгерісі жоқ, база жаңартылған, миграция қосылмады |
| Phase 2 / Phase 3 / Phase 4 / Phase 5 | Барлық төрт HTTP suite PASS; Phase 3 — 13 нақты Docker жіберілімі |
| Enhancement 1 / 2 / 3 | Үш HTTP suite және Phase4Checks / Enhancement1Checks / Enhancement3Checks C# suite-тері PASS |
| Localization, cookie, open redirect, ресурстар | Enhancement 4 HTTP/SQL/Docker және resource check PASS; kk-KZ / ru-RU / en-US |
| AI trusted language / existing feedback / hint leak | PASS: offline SDK, үш тіл, белгісіз culture fallback, cached feedback, ашылмаған кеңестерді қорғау |
| Custom Run DB isolation / Journey / Diff ownership | PASS: Run ешнәрсе сақтамайды, бөтен source ашылмайды, дұрыс хронология |
| Docker restrictions / image digest / cancellation | PASS: нақты контейнер шектеулері, pin, OOM/timeout/output, cleanup; Phase 3 және Enhancement 2 unavailable нұсқалары да PASS |
| CSP HTTP / rebuilt bundle checks | PASS: nonce, local assets/worker URL, unsafe-eval жоқ; нақты browser console тексерілмеді |
| Editor/Diff/Run localization Node doubles | PASS; нақты браузер емес |
| `git diff --check` / secret scan / fixture cleanup | PASS: whitespace қатесі, жарияланған құпия, fixture аккаунт/есеп/тақырып, runner контейнері немесе temp workspace қалдығы жоқ |

Қорытынды аудитте Admin authorization, CSRF, submission ownership, hidden-test redaction, unrevealed hints, XSS encoding, AllowedHosts, Docker pinning және API key secrecy сақталғаны расталды. Нақты жергілікті OpenAI кілті мен әкімші құпиясөзі тек жадта салыстырылды, мәндері экранға шығарылған жоқ. Tracked және ignored емес файлдарда `bin`, `obj`, `.vs`, `node_modules`, test logs, скриншот немесе fixture артефактілері жоқ. Өзгерген/жаңа C# файлдарындағы 91 explicit әдіс/конструктор тексерілді: барлығында тікелей алдында қазақша purpose-комментарийі бар. Generated migrations өзгермеді.

`verify_phase5_live.py` бұл enhancement кезінде орындалмады: ол әдейі ақылы API шақыруына арналған бөлек opt-in тест. `verify_enhancement3_migration.py` — тек бұрынғы схемадан upgrade кезінде орындалатын бір реттік тексеру; осы базаға Enhancement 3 миграциясы бұрын қолданылғандықтан қайта орындалмады және база кері қайтарылмады. Оның тарихи нәтижесі study-enhancement3.md ішінде бар. Қалыпты регрессияның барлық suite-тері және Docker unavailable нұсқалары қайта орындалды.

## 27. Финал demo сценарийі қандай?

Негізгі университет демонстрациясы қазақ тілінде жүргізіледі: Home → Login → Tasks → Task Details → қате C++ код → custom input және Run → Submit → Wrong Answer → AI талдауы → Hint 1 → Hint 2 ашу → кодты өзгерту → әрекеттерді салыстыру → дұрыс шешім → Accepted → Journey → Progress → Learning Map → Practice Next → Leaderboard.

Diff екі ресми әрекетті салыстырады. Редакторда кодты өзгерту өздігінен жаңа әрекет жасамайды; салыстыру үшін екінші сақталған submission қажет. Сондықтан көрсетілімде екі бар қате әрекетті салыстыруға немесе түзетілген шешімді Submit еткен соң алдыңғы нұсқамен салыстыруға болады.

Қазақша сценарий HTTPS/SQL және нақты Docker арқылы толық өтті: Run → WrongAnswer → айқын белгіленген синтетикалық сақталған AI пікірі → бұрынғы пікірді қайта көрсету → Hint 1/2 → Accepted → Diff/Journey → Progress/Map/Practice Next/Leaderboard. Live OpenAI сұрауы болған жоқ. Жаңа AI тілін таңдауды offline SDK transport тексерді; синтетикалық кеңес модельдің шынайы жауабы ретінде көрсетілмейді. Нақты браузердегі толық қазақша demo **орындалмаған**. Russian/English smoke негізгі және 14 Admin маршрутында, validation/error беттерінде HTTP деңгейінде өтті. kk → ru → en → kk циклінде аутентификация, жергілікті URL және барлық дерекқор кестесінің толық жол hash-тары өзгермеді.

## 28. Жобаның толық архитектурасы қандай?

```text
Browser
   |
   +---- Culture Cookie
   |
   v
Request Localization
   |
   v
ASP.NET Core MVC
   |
   +---- Resources (KZ / RU / EN)
   |
   +---- Identity
   |
   +---- EF Core ---- SQL Server
   |
   +---- Docker Runner
   |
   +---- OpenAI Tutor
   |
   +---- Progress / Learning Map
   |
   +---- Leaderboard
```

Browser C++ source жібереді, ал тек Docker judge ресми execution status анықтайды. Custom Run сол sandbox пен ортақ execution gate арқылы уақытша нәтиже береді. AI код орындамайды және judge статусын өзгертпейді. Оқу картасы мен рейтинг ресми сақталған тарихтан есептеледі. Localization осы архитектураның presentation қабатын толықтырады.

## 29. Жобаны қорғауда қандай негізгі технологияларды айту керек?

ASP.NET Core MVC және Razor — серверлік маршруттар мен UI; Identity — тіркелу, кіру және role authorization; EF Core пен SQL Server — тұрақты деректер; Docker — оқшауланған C++ орындау; Monaco — кодты өңдеу және салыстыру; OpenAI Responses — құрылымды оқу кеңесі; ASP.NET Core Localization — үш тілдегі жүйелік интерфейс.

Қауіпсіздік дәлелдері ретінде antiforgery, encoded output, submission ownership, hidden-test filtering, server-side hint reveal, CSP nonce, explicit AllowedHosts, pinned Docker digest, no-network sandbox және ресурс шектеулерін көрсетуге болады. Жоба осы feature set-пен аяқталады: competitions, achievements, chat, Redis, Kubernetes немесе mobile app қосылмайды.

## 30. Жобаны іске қосу командалары қандай?

SQL Server мен Docker Desktop Linux containers режимі қолжетімді болуы керек. Құпиялар README-дегі User Secrets тәртібімен жергілікті орнатылады; нақты мәндерді файлға немесе терминал журналына жарияламау керек.

```powershell
dotnet restore
dotnet build
dotnet ef database update
dotnet ef migrations has-pending-model-changes
dotnet run --launch-profile https
```

Жергілікті URL — `https://localhost:7115`. Редактор source-ы өзгерсе, қолданыстағы npm құралымен bundle жасалады:

```powershell
npm ci
npm run build:editor
node scripts/verify_editor_csp.mjs
node scripts/verify_enhancement2_ui.mjs
```

Қолданыстағы HTTP regression скрипттері іске қосылған қолданбаны қажет етеді:

```powershell
python scripts/verify_phase2.py
python scripts/verify_phase3.py
python scripts/verify_phase4.py
python scripts/verify_phase5.py
python scripts/verify_enhancement1.py
python scripts/verify_enhancement2.py
python scripts/verify_enhancement3.py
python scripts/verify_resources.py
python scripts/verify_enhancement4.py
dotnet run --project tests/Phase4Checks/Phase4Checks.csproj
dotnet run --project tests/Enhancement1Checks/Enhancement1Checks.csproj
dotnet run --project tests/Enhancement3Checks/Enhancement3Checks.csproj
git diff --check
```

Тесттерді ортақ fixture деректері мен runner лимиттеріне кедергі келтірмеу үшін кезекпен жүргізу керек. Қолданба орындалып тұрғанда executable-ды қайта build жасау Windows файл lock қатесін беруі мүмкін: алдымен қолданбаны тоқтатып, build аяқталғанын күту керек. HTTP тесттері кәдімгі регрессияда ағылшын culture cookie-ін анық орнатады; жаңа localization тексеруі үш тілді бөлек тексереді. Нақты браузер жұмысы осы командалардың нәтижесінен бөлек manual checklist бойынша орындалады.

## Файлдар тізімі: 20 жаңа, 72 өзгертілген

### Жасалған файлдар

```text
AdminResource.cs
Controllers/CultureController.cs
Resources/AdminResource.en-US.resx
Resources/AdminResource.kk-KZ.resx
Resources/AdminResource.ru-RU.resx
Resources/SharedResource.en-US.resx
Resources/SharedResource.kk-KZ.resx
Resources/SharedResource.ru-RU.resx
Resources/StudentResource.en-US.resx
Resources/StudentResource.kk-KZ.resx
Resources/StudentResource.ru-RU.resx
Services/Localization/SupportedCultures.cs
SharedResource.cs
StudentResource.cs
ViewModels/Progress/TopicStrengthLevel.cs
Views/Shared/_CultureSelector.cshtml
manual-enhancement4-checklist.md
scripts/verify_enhancement4.py
scripts/verify_resources.py
study-enhancement4.md
```

### Өзгертілген файлдар

```text
Areas/Admin/Controllers/LeaderboardController.cs
Areas/Admin/Controllers/ProgrammingTasksController.cs
Areas/Admin/Controllers/TestCasesController.cs
Areas/Admin/Controllers/TopicsController.cs
Areas/Admin/Views/Home/Index.cshtml
Areas/Admin/Views/ProgrammingTasks/Create.cshtml
Areas/Admin/Views/ProgrammingTasks/Delete.cshtml
Areas/Admin/Views/ProgrammingTasks/Details.cshtml
Areas/Admin/Views/ProgrammingTasks/Edit.cshtml
Areas/Admin/Views/ProgrammingTasks/Index.cshtml
Areas/Admin/Views/ProgrammingTasks/_Form.cshtml
Areas/Admin/Views/TestCases/Create.cshtml
Areas/Admin/Views/TestCases/Delete.cshtml
Areas/Admin/Views/TestCases/Edit.cshtml
Areas/Admin/Views/TestCases/Index.cshtml
Areas/Admin/Views/TestCases/_Form.cshtml
Areas/Admin/Views/Topics/Create.cshtml
Areas/Admin/Views/Topics/Delete.cshtml
Areas/Admin/Views/Topics/Edit.cshtml
Areas/Admin/Views/Topics/Index.cshtml
Areas/Admin/Views/_ViewImports.cshtml
ClientScripts/code-editor.js
Controllers/AccountController.cs
Controllers/CustomRunsController.cs
Controllers/SubmissionsController.cs
Program.cs
README.md
Services/AI/AiTutorInput.cs
Services/AI/OpenAiFeedbackClient.cs
Services/AI/OpenAiTutorService.cs
Services/Progress/LearningInsightsService.cs
Services/Submissions/SubmissionService.cs
ViewModels/Account/LoginViewModel.cs
ViewModels/Account/RegisterViewModel.cs
ViewModels/Admin/ProgrammingTaskFormViewModel.cs
ViewModels/Admin/TestCaseFormViewModel.cs
ViewModels/Admin/TopicFormViewModel.cs
ViewModels/Progress/LearningInsightsViewModel.cs
ViewModels/Submissions/CustomRunViewModel.cs
ViewModels/Submissions/SubmitViewModel.cs
Views/Account/AccessDenied.cshtml
Views/Account/Login.cshtml
Views/Account/Profile.cshtml
Views/Account/Register.cshtml
Views/Home/Index.cshtml
Views/Home/Privacy.cshtml
Views/Journeys/Index.cshtml
Views/Leaderboard/Index.cshtml
Views/Progress/Index.cshtml
Views/Progress/_LearningMap.cshtml
Views/Shared/Error.cshtml
Views/Shared/_AttemptJourney.cshtml
Views/Shared/_ErrorLayout.cshtml
Views/Shared/_Layout.cshtml
Views/Shared/_LoginPartial.cshtml
Views/Shared/_RecentSubmissions.cshtml
Views/Submissions/Compare.cshtml
Views/Submissions/Details.cshtml
Views/Submissions/My.cshtml
Views/Tasks/Details.cshtml
Views/Tasks/Index.cshtml
Views/Tasks/_SubmissionForm.cshtml
Views/_ViewImports.cshtml
scripts/verify_editor_csp.mjs
scripts/verify_enhancement2_ui.mjs
scripts/verify_phase2.py
scripts/verify_phase3.py
tests/Enhancement3Checks/Program.cs
tests/Phase4Checks/Program.cs
wwwroot/css/site.css
wwwroot/js/custom-run.js
wwwroot/js/editor/code-editor.js
```
