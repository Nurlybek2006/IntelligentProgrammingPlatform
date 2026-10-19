# Ultimate UI — нақты браузердегі тексеру

Осы сессияда браузер құралы қолжетімсіз болды, screenshot **алынған жоқ**. Төмендегі тармақтар автоматты HTTP/DOM-double тесттерімен жабылды деп есептелмейді. Қазақша интерфейсті негізгі тексеру тілі ретінде қолданыңыз; соңынан RU/EN-де ұзын мәтіндер мен мәзірді салыстырыңыз.

## Дайындау

1. `dotnet run --launch-profile https` арқылы Development қолданбасын ашыңыз.
2. Браузерде `https://localhost:7115` ашыңыз. Certificate ескертуін қолданушы өзі шешеді.
3. DevTools responsive viewport-ты кезекпен **1440×900**, **1024×768**, **390×844** қойыңыз. Browser zoom — 100%; кейін 200% zoom тексеріңіз.
4. Қонақ, бөлек Student тест аккаунты және Admin арқылы қараңыз. Бар пайдаланушының жеке хабарын/кодын screenshot-қа түсірмеңіз. Тест деректерін ғана қолданыңыз.
5. Console және Network панельдерін ашыңыз. CSP қатесі, stylesheet/worker/module 404 және JavaScript exception болмауы керек.

## Бет бойынша screenshot және тексеру

Әр жолдағы үш өлшемде screenshot алыңыз; толық бетпен қатар editor/chat үшін экранның негізгі бөлігін де түсіріңіз. Ұсынылатын жергілікті атау: `obj/ultimate-ui-screenshots/kk-home-1440.png`. Бұл файлдар қазір жасалған жоқ.

| Бет / жол | Нақты қарайтын нәрсе | 1440 | 1024 | 390 |
|---|---|---|---|---|
| Қонақ Home `/` | Hero, екі CTA, оқу циклі, 4 feature; бос кеңістік шамадан тыс емес | ☐ | ☐ | ☐ |
| Жеке Home `/` | Аты, төрт нақты metric, сабақ пен келесі есеп, соңғы әрекеттер | ☐ | ☐ | ☐ |
| Lessons `/Lessons` | Tabs, topic filter, қысқа карточкалар, hover/focus | ☐ | ☐ | ☐ |
| Python `/Lessons?language=python` | Байланысқан timeline, реттілік, ұзын тақырып | ☐ | ☐ | ☐ |
| Lesson Details | Мәтін жолы, code block, practice, previous/next; content өзгермеген | ☐ | ☐ | ☐ |
| Tasks `/Tasks` | Search/topic/difficulty, row hover, favorite, solved, кесте scroll | ☐ | ☐ | ☐ |
| Solve `/Tasks/sum-of-two-numbers` | Desktop 40/60, laptop/mobile stack, редактор өлшемі, sticky action | ☐ | ☐ | ☐ |
| Submission Details | Accepted және error header, уақыт/тест, hidden-test құпиялығы | ☐ | ☐ | ☐ |
| AI Tutor / AI Review | Сақталған жауап, hint қадамдары, complexity, visible disclaimer | ☐ | ☐ | ☐ |
| Journey / Compare | Timeline, екі code pane немесе тар экрандағы inline diff | ☐ | ☐ | ☐ |
| Progress `/Progress` | Metrics, XP, topic rows, error patterns, next task | ☐ | ☐ | ☐ |
| Profile `/Account/Profile` | Avatar, деңгей, email тек жеке бетте, XP, heatmap, recent | ☐ | ☐ | ☐ |
| Public profile `/Users/{id}` | Аты/жалпы stats, social controls; email/source/favorites жоқ | ☐ | ☐ | ☐ |
| Friends `/Friends` | Бес бөлім, карточкалар, incoming/outgoing, бос күйдегі CTA | ☐ | ☐ | ☐ |
| Messages `/Messages` | Sidebar, preview/time/unread, empty state | ☐ | ☐ | ☐ |
| Conversation | Header/profile, екі түрлі bubble, composer, ұзын мәтін | ☐ | ☐ | ☐ |
| Leaderboard | Алғашқы 3 орын, өз қатары, avatar, ұзын DisplayName | ☐ | ☐ | ☐ |
| Favorites | Бос және толтырылған тізім, star күйі | ☐ | ☐ | ☐ |
| Login / Register | Label, autofill, validation, password helper, Tab тәртібі | ☐ | ☐ | ☐ |
| Edit Profile | Upload control, avatar fallback, көмекші мәтін | ☐ | ☐ | ☐ |
| Admin dashboard | Айқын Admin контекст, басқару карталары | ☐ | ☐ | ☐ |
| Admin Users/Topics/Lessons/Tasks/TestCases | Кесте container scroll, CRUD формалары, validation | ☐ | ☐ | ☐ |
| 403 / 404 / error | Қате нөмірі, түсіндірме, қайту сілтемесі, private exception жоқ | ☐ | ☐ | ☐ |

## Navbar және тіл — міндетті әрекеттер

- [ ] 1440-та nav, avatar және тіл controls тік бағытта бір сызықта, биіктігі бірдей.
- [ ] 1024 және 390-та hamburger ашылып/жабылады; негізгі links екі/үш бағанға топталады.
- [ ] User dropdown: Profile, Favorites, Admin тек Admin-ге, Logout.
- [ ] Community dropdown: Friends, Messages және бар unread badges.
- [ ] Тіл dropdown: ағымдағы тіл көрінеді; Қазақша/Русский/English таңдағанда secure POST болып, сол route/query-ге қайтады.
- [ ] Бөлек «Ауыстыру» батырмасы жоқ; JavaScript өшірілгенде де тіл form-ы және негізгі nav қолжетімді.
- [ ] Escape мәзірді жабады, фокус ашқан control-ға қайтады. ArrowUp/Down/Home/End, Tab/Shift+Tab жұмыс істейді.
- [ ] Dropdown панель астында қалып қоймайды және экранның оң жақ шетінен шықпайды.
- [ ] Ұзын DisplayName, RU/EN атаулары және 99+ badge navbar-ды бұзбайды.

## Editor / console

- [ ] C++ және Python highlighting, worker және caret дұрыс; editor тек өзінің ішінде көлденең scroll жасайды.
- [ ] Екі тілдің draft-тары тәуелсіз; reload/back/forward мәтінді дұрыс сақтайды.
- [ ] Reset confirmation-нан бас тартсаңыз код өзгермейді; растағанда тек таңдалған тіл жаңарады.
- [ ] Custom Input/Run Result: алғашқы Run, қайталанған Run, runtime change, error жағдайы дұрыс tab ашады.
- [ ] Keyboard shortcut редактор ішінде ғана іске қосылады; Custom Input ішіндегі Enter код жібермейді.
- [ ] Busy кезінде duplicate Run/Submit жоқ; аяқталған соң батырмалар қайта қосылады.
- [ ] Source code, stdout және қате мәтініндегі HTML таңбалары мәтін күйінде көрсетіледі.
- [ ] Reduced motion қосылғанда hover қозғалысы мен smooth result scroll болмайды.

## Chat / SignalR

- [ ] Екі бөлек браузер профиліндегі **тест аккаунттары** арасында хабарлама refresh-сіз келеді.
- [ ] Тек accepted friends жібере алады; block/unfriend болғанда composer unavailable күйіне өтеді.
- [ ] Enter — send, Shift+Enter — newline; IME composition хабарды ерте жібермейді.
- [ ] Offline кезінде жазылған мәтін сақталады; reconnect кезінде history/counts қайта жаңарады.
- [ ] History scroll жоғарыда болса жаңа хабар оны төменге күштеп түсірмейді.
- [ ] 390-та әңгіме таңдалса sidebar жасырылады; «Артқа» қайта тізімге апарады.
- [ ] Телефонның виртуалды keyboard-ы composer-ді жаппайды; landscape және safe-area дұрыс.
- [ ] Header-дегі connection мәтіні басқа адамның «online» мәртебесі ретінде қабылданбайды.

## Accessibility және аяқтау

- [ ] Skip link main-ге апарады; фокус әр control-да анық көрінеді.
- [ ] 200% zoom-де тапсырмалар/батырмалар жоғалмайды; бүкіл бет көлденең сырғымайды.
- [ ] Screen reader тіл control, disclosure, tabs, validation және chat announcements-ты түсінікті оқиды.
- [ ] KZ негізгі тексеру, RU және EN smoke check; raw resource key немесе күтпеген ағылшын UI мәтіні жоқ.
- [ ] Chromium-нан бөлек Firefox/Safari және нақты телефонда қысқа smoke check орындалды.
- [ ] Нәтижелерді браузер нұсқасы/өлшем/бет атымен тіркеп, нақты screenshot-тарды ғана «тексерілді» деп белгілеңіз.

Ақылы AI батырмасын visual тест үшін автоматты баспаңыз. Сақталған немесе offline fixture жауабын қолданыңыз. Бұл checklist-тегі белгіленбеген тармақтар орындалды деп есептелмейді.
