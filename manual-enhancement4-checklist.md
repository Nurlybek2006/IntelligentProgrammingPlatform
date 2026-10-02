# Enhancement 4 — нақты браузердегі қорытынды тексеру

**Ағымдағы күй: ОРЫНДАЛМАҒАН.** Осы ортадағы UI inventory бос; `iab` қолжетімсіз. Нақты визуалды viewport — **0**, тіл — **0**. HTTP, offline AI transport және Node/Monaco doubles нәтижелерін осы тізімге browser PASS ретінде көшірмеңіз. Скриншот, browser console және нақты keyboard/mobile interaction әлі қажет.

## Дайындық

- [ ] Қолданбаны `dotnet run --launch-profile https` арқылы ашу; HTTPS сертификаты дұрыс қабылданған.
- [ ] Docker Desktop Linux containers режимінде, image digest қолданыстағы pin-ге сәйкес.
- [ ] Тек уақытша немесе келісілген demo пайдаланушысын қолдану; нақты студент source-ын скриншотқа шығармау.
- [ ] Екі сан қосу сияқты жарияланған C++ demo task, visible example және кемінде бір hidden test дайын.
- [ ] Student және Admin сессияларын бөлек тексеру; пароль, token, User Secrets немесе OpenAI key-ді журналға/скриншотқа түсірмеу.
- [ ] Live AI қолданылса оны бөлек белгілеу. Сақталған не offline fixture feedback-ті жаңа live жауап деп көрсетпеу.

## Viewport және тіл матрицасы

Әр жолда Home, Tasks, Task Details, Submission Details, Progress, Leaderboard, Login және Admin Dashboard беттерінің бәрін қараңыз. Одан кейін Journey, Diff, Hint Ladder және Custom Run-ды тексеріңіз. Нәтижеге нақты browser атауын, уақытын және тексерілген беттерді жазыңыз.

| Viewport | Culture | Негізгі 8 бет | Қосымша 4 мүмкіндік | Console/CSP | Нақты нәтиже |
|---|---|---|---|---|---|
| 1440×900 | kk-KZ | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 1440×900 | ru-RU | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 1440×900 | en-US | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 1024×768 | kk-KZ | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 1024×768 | ru-RU | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 1024×768 | en-US | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 390×844 | kk-KZ | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 390×844 | ru-RU | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |
| 390×844 | en-US | Тексерілмеді | Тексерілмеді | Тексерілмеді | PENDING |

## Тіл ауыстыру және жүйелік мәтіндер

- [ ] Cookie жоқ жаңа browser profile қазақша ашылады; `<html lang="kk">` және мағыналы қазақша title бар.
- [ ] `kk → ru → en → kk` тізбегін navbar арқылы орындау; ағымдағы тіл анық көрінеді.
- [ ] Ауыстырған соң Login күйі сақталады; мүмкін жерде сол жергілікті бет пен query сақталады.
- [ ] Cookie `Secure`, `HttpOnly`, `SameSite=Lax`; сыртқы return URL-ға бағыттау болмайды.
- [ ] Әр тілде raw `Nav_...`, `Status_...`, `Strength_...` немесе басқа resource key көрінбейді.
- [ ] Status, difficulty, button, validation, TempData және error page мәтіндері бір culture-ға сай.
- [ ] Task/Topic DB мәтіні, C++ source, input/output, compiler output және бұрынғы AI feedback өзгермеген.
- [ ] Тілді ауыстыру жаңа submission, AI analysis немесе leaderboard өзгерісін жасамайды.
- [ ] Login/Register/Profile/AccessDenied және Admin Topics/Tasks/TestCases формаларын бос/жарамсыз енгізумен тексеру.

## Нақты Monaco және Custom Run әрекеті

- [ ] Task бетінде Monaco жүктеледі, highlighting бар, C++ кодын нақты теруге болады.
- [ ] Source пен Custom Input control-дарының label-ы бар; editor accessible label-ы ағымдағы тілде.
- [ ] Екі сан қосатын код пен `2 3` custom input енгізіп, Run басу; уақытша output `5`.
- [ ] Run — secondary, Submit — primary; Run кезінде екі батырма уақытша өшеді, аяқталғанда қалпына келеді.
- [ ] Жүгіру күйі, нәтиже мәртебесі, уақыт және «сақталмайды» түсіндірмесі ағымдағы тілде.
- [ ] Нәтижеден бұрын/кейін submission саны, Journey, Progress және Leaderboard бірдей.
- [ ] Syntax error, runtime error және input limit жағдайлары түсінікті; compiler/program мәтіні аударылмайды.
- [ ] HTML тәрізді input/output орындалмай, жай мәтін болып көрінеді.
- [ ] Submit нақты ресми әрекет жасайды және нәтижеге апарады; hidden test contents көрсетілмейді.
- [ ] Reload/navigation кезінде осы user/task draft-ы күтілгендей сақталады; validation source ескі draft-пен басылмайды.
- [ ] Тілді ауыстырғанда draft бұзылмайды; source code мәтіні өзгермейді.

## AI Hint Ladder

- [ ] AI heading, Analyze CTA, hint нөмірі, reveal next/final және try-before-reveal мәтіндері ағымдағы тілде.
- [ ] Жаңа талдауда тек Hint 1 көрінеді; толық HTML/DOM ішінде unrevealed Hint 2/3 мәтіні жоқ.
- [ ] Reveal next → Hint 2, reveal final → Hint 3; қайта жүктегенде ашылу күйі сақталады.
- [ ] Hint reveal және culture switch жаңа OpenAI сұрауын жасамайды.
- [ ] Бұрынғы feedback тілі сол күйінде қалады; summary/explanation/hints автоматты аударылмайды.
- [ ] Live analysis арнайы қолданылса жаңа жауап ағымдағы тілде; technical/schema identifiers өзгермеген. Қолданылмаса осы тармақты «LIVE AI ҚОЛДАНЫЛМАДЫ» деп белгілеу.
- [ ] Басқа user submission-ында feedback оқу не hint ашу мүмкін емес.

## Journey және Code Diff

- [ ] Journey нөмірлері, status, AI hint counts, UTC date display және navigation барлық тілде түсінікті.
- [ ] Compact/full timeline, pagination және алдыңғы әрекетпен compare link дұрыс.
- [ ] Тек осы user-дің бір task бойынша екі ресми submission-ын салыстыруға болады.
- [ ] Desktop diff ыңғайлы side-by-side; code өзгертуге болмайды.
- [ ] Mobile diff inline/тар экранға сай көрініске ауысып, оқуға және scroll жасауға жарайды.
- [ ] Код ішіндегі `</textarea>` не `<script>` тәрізді мәтін HTML ретінде орындалмайды.
- [ ] Басқа user ID, бірдей ID, жоқ ID және екі бөлек task жұбы 404/қауіпсіз denial береді.

## 390×844 mobile тексеруі

- [ ] Navbar пернетақтамен және touch арқылы ашылып/жабылады; тіл selector-ы collapsed menu ішінде қолжетімді.
- [ ] Бүкіл бет көлденең scroll жасамайды; тек қажет table/code ішкі аймағы scroll жасайды.
- [ ] Task description → editor → custom input → Run/Submit → result таза тігінен орналасады.
- [ ] Monaco бет енін күштеп үлкейтпейді; Run/Submit экраннан жоғалмайды.
- [ ] Hint cards, Learning Map және Practice Next ұзын қазақша/орысша мәтінмен сыйып тұрады.
- [ ] Leaderboard және Admin кестелері оқылады; action батырмалары жабылмайды.
- [ ] Journey, Diff, validation және error alert мәтіндері бір-бірін баспайды.

## Accessibility және визуалды бірізділік

- [ ] Tab/Shift+Tab арқылы navbar, language, editor fallback, Run/Submit және AI actions жетімді.
- [ ] Focus ring анық көрінеді; focused element sticky navbar астында жасырылмайды.
- [ ] Барлық form control label-мен байланысқан; checkbox/select атауы түсінікті.
- [ ] Status тек түспен берілмейді; әр badge-де мәтін бар.
- [ ] Table header, heading hierarchy, progress accessible label және current nav белгісі дұрыс.
- [ ] Card аралығы, table padding, button биіктігі, form ені және empty state alignment бірізді.
- [ ] Delete danger стилінде; барлық action бірдей primary емес.
- [ ] Browser zoom және ұзын resource мәтінінде маңызды action жоғалмайды.

## Browser console және CSP

- [ ] Әр тілде маңызды JavaScript error жоқ.
- [ ] CSP violation жоқ; `unsafe-eval` немесе жалпы inline script рұқсаты қосылмаған.
- [ ] Monaco worker same-origin URL арқылы жүктеледі; editor мен diff нақты жұмыс істейді.
- [ ] Run fetch POST, antiforgery және JSON нәтижесі дұрыс; localization JavaScript error жоқ.
- [ ] Console қолжетімсіз болса нақты limitation жазу; «console таза» деп белгілемеу.

## Қазақша қорытынды demo

- [ ] Home → Login → Tasks → task → қате код → Custom Run → Submit → Wrong Answer.
- [ ] AI analysis немесе анық белгіленген сақталған feedback → Hint 1 → Hint 2.
- [ ] Кодты өзгерту; екі сақталған әрекет барда Diff көрсету. Бір draft екі ресми әрекетті алмастырмайды.
- [ ] Дұрыс шешімді Submit → Accepted → алдыңғы әрекетпен Diff → Journey.
- [ ] Progress → Learning Map → Practice Next → Leaderboard.
- [ ] Әр қадамда kk-KZ сақталған; нақты жасалған Docker/AI әрекеттері мен қолданылған fixtures адал сипатталған.
- [ ] Соңында ru-RU және en-US негізгі беттерін smoke тексеріп, raw resource key жоқ екенін жазу.

Нәтиже жазбасы: browser/version — **PENDING**; viewport/language — **PENDING**; тексеруші — **PENDING**; байқалған ақаулар — **PENDING**; live AI — **ҚОЛДАНЫЛМАҒАН**. Қажет түзетулер енгізілген соң тек әсер еткен browser қадамдарын қайта орындаңыз.
