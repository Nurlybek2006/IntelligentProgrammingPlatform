# Enhancement 8 — ашық профиль, достар және жеке хаттар

Бұл кеңейту бұрынғы ASP.NET Core MVC / .NET 10, Identity, SQL Server / EF Core, Docker, Monaco және AI қызметтерін сақтайды. Жеке әлеуметтік мүмкіндіктер қолданыстағы жобаға қосылды. Жұмыс күні: 2026-10-19. Бұл кезеңде ақылы OpenAI шақыруы жасалмайды.

## 1. Enhancement 8-де не жасадық?

Ашық профиль, рейтингтен профильге өту, DisplayName бойынша іздеу, достық сұрауы, қабылдау/қабылдамау, сұрауды қайтару, достықты жою, блоктау және екі адамдық мәтіндік хат алмасу қосылды. Навигациядағы «Қауымдастық» мәзірі оқылмаған хаттар мен кіріс сұрауларының санын көрсетеді. Feed, топтық чат, файл, сурет, реакция, typing/presence және хабарды өңдеу/жою жоқ.

```text
Leaderboard / Search
       |
       v
 Public Profile
       |
       v
 Friend Request
       |
       v
   Friendship (Accepted)
       |
       v
DirectConversation
       |
       v
 DirectMessage ------ SQL Server (негізгі дерек)
       |
       v
  IHubContext
       |
       v
    SignalR
       |
       v
Other User Browser -- қорғалған GET арқылы жаңарту
```

## 2. Public Profile деген не?

`GET /Users/{id}` — қонаққа да ашық, кэштеуге тыйым салынған бет. `UsersController` → `SocialProfileService` → `PublicUserCard` тізбегі тек аватар, есім, тіркелген күн, деңгей, XP, шешілген есеп саны, рейтинг ұпайы және сәттілік үлесін береді. URL қолданыстағы Identity ID-ін пайдаланады; email URL-ге қойылмайды және ID экранда есім орнына көрсетілмейді.

## 3. Private Profile мен Public Profile айырмашылығы қандай?

`/Account/Profile` — иесінің жеке беті: email, өзінің әрекеттері, таңдаулылары және белсенділік картасы сияқты деректер сонда қалады. `/Users/{id}` жеке `_ProfileOverview` partial-ын қолданбайды. Тек бұрыннан бар `_UserAvatar` компоненті қайта пайдаланылады. Өз ашық профиліндегі «Профильді өңдеу» батырмасы бар `Account/EditProfile` бетіне апарады.

## 4. Friendship entity қалай жұмыс істейді?

`Id`, `UserAId`, `UserBId`, `RequestedByUserId`, `Status`, `CreatedAt`, `AcceptedAt` өрістері бір жұпты сипаттайды. Күйлер: `Pending=0`, `Accepted=1`. SQL CHECK requester жұптың мүшесі болуын және AcceptedAt күйге сәйкес толтырылуын талап етеді. Қабылданбаған/қайтарылған сұрау өшіріледі; артық notification history құрылмайды.

## 5. UserAId / UserBId неге normalize жасалады?

`SocialPair.Create` екі ID-ді `String.CompareOrdinal` бойынша реттейді. Сондықтан A→B және B→A бірдей жұпқа айналады. Браузер UserA/UserB мәндерін таңдамайды. `ResolveAsync` нақты пайдаланушыларды SQL-ден алып, canonical ID-лерді қолданады; өзімен жұп құруға болмайды. SQL жұп CHECK-і BIN2 салыстыруын қолданады.

## 6. UNIQUE pair не үшін керек?

`Friendships(UserAId,UserBId)` және `DirectConversations(UserAId,UserBId)` бірегей индекстері қайталанған қатынасты соңғы қорғаныс ретінде тоқтатады. Қызметтер бұған қоса жұпқа ортақ transaction-scoped `sp_getapplock` алады. Құлып атауы — екі ID-дің ұзындығы көрсетілген тізбегінің SHA-256 хэші. Бұл бір SQL серверін қолданатын бірнеше app instance-ке де ортақ. Күту шегі — 5 секунд; timeout қауіпсіз Busy жауабына айналады.

## 7. Friend Request қалай жіберіледі?

`POST /Friends/Request` ағымдағы иені тек `ClaimTypes.NameIdentifier` арқылы анықтайды. Қызмет жұп құлпын алып, блок пен бұрынғы қатынасты тексереді. Еш байланыс болмаса Pending құрады. Қайталанған немесе кері сұрау жаңа жол қоспайды және автоматты түрде қабылданбайды. UI өз профилінде сұрау батырмасын көрсетпейді; сервер де өзін-өзі қосуға тыйым салады.

## 8. Accept / Decline қалай қорғалған?

`[Authorize]`, POST, жалпы MVC `AutoValidateAntiforgeryToken` және қызметтің recipient тексеруі бірге жұмыс істейді. Сұрауды жіберген адам өзінің outgoing сұрауын қабылдай алмайды. Алушы Pending-ті Accepted-ке ауыстырады, `AcceptedAt` UTC уақытын алады. Decline тек Pending алушысына, Cancel тек Pending жіберушісіне беріледі. Үшінші адам target параметрін ауыстыру арқылы бөтен жұпты өзгертпейді.

## 9. Blocking қалай жұмыс істейді?

`UserBlock(BlockerUserId,BlockedUserId,CreatedAt)` бағытталған және жұбы бірегей. Блок пен достықты жою бір жұп құлпы және бір SQL транзакциясы ішінде өтеді. Екі бағыттағы блоктың кез келгені сұрау мен жаңа хатқа тыйым салады. Блок қойған адам ғана «Блоктан шығару» күйін көреді; екінші тарапқа бейтарап «Байланысу қолжетімсіз» беріледі. Іздеу екі бағыттағы блокталған жұптарды алып тастайды.

## 10. Unblock не істейді?

Тек өз BlockerUserId жолын жояды. Басқа адамның блогын өзгерте алмайды және Friendship-ті қайта құрмайды. Кейін жаңа сұрау және бөлек қабылдау қажет. `/Friends?section=blocked` өз блоктарын басқаруға көмектеседі.

## 11. DirectConversation деген не?

Екі қатысушының тұрақты хат алмасу идентификаторы. Бірінші «Хат жазу» POST әрекетінде Accepted достарға ғана lazy түрде құрылады. `CreatedAt` және `LastMessageAt` сақталады. Қатысушы бұрыннан бар диалогты достық тоқтағаннан кейін де оқи алады, бірақ жіберу рұқсаты бөлек тексеріледі.

## 12. Неге бір жұпқа бір Conversation ғана бар?

Normalized pair және UNIQUE индекс екі бағыттағы ашуды бір тарихқа біріктіреді. Бір уақытта бірнеше Open келсе де жұп құлпы бір диалог береді. Достықты қайта қабылдау бұрынғы Conversation ID-ін қайта пайдаланады; хаттар екі тізімге бөлінбейді.

## 13. DirectMessage деген не?

`Id`, `ConversationId`, `SenderUserId`, `ClientMessageId`, `Body`, `CreatedAt`, `ReadAt` сақталады. `ClientMessageId` — әр жаңа мәтіннің Guid кілті; `(SenderUserId,ClientMessageId)` UNIQUE. Желі жауабы жоғалса, сол кілт пен сол мәтінді қайталау жаңа жол қоспайды. Бұрынғы кілтті басқа мәтінге немесе диалогқа қолдану қабылданбайды. Хат шегі — trim-нен кейін бос емес, ең көбі 2000 UTF-16 таңба бірлігі; emoji кейде екі бірлік алады.

## 14. Неге message plain text?

Әңгімеге HTML не Markdown қажет емес. Razor `@message.Body` кодтайды; клиент `createElement` және `textContent` қолданады. `<script>` және `<img onerror>` жазулары мәтін түрінде қалады. CSS `white-space: pre-wrap` жаңа жолдарды, `overflow-wrap:anywhere` ұзын үздіксіз мәтінді сақ әрі оқылатын етеді. Хаттар ұштан-ұшқа шифрланбайды: SQL-де жай мәтін ретінде сақталады.

## 15. ReadAt не үшін керек?

`null` — алушы әлі оқымаған хат. `POST /Messages/Read` қатысушыны, диалогты және көрсетілген `throughId`-дің осы диалогқа тиесілі екенін тексереді. Тек басқа адам жіберген, осы ID-ге дейінгі оқылмаған хаттарды UTC уақытпен белгілейді. GET ештеңені өзгертпейді. Жіберушінің own outgoing хаты оқылды деп белгіленбейді. Read receipt UI енгізілген жоқ.

## 16. Unread count қалай есептеледі?

SQL `ReadAt IS NULL`, `SenderUserId != currentUser` және conversation membership шарттарымен санайды. Conversation list әр диалогтың санын, `SocialNotificationService` жалпы санды береді. Браузер тек көрініп тұрған қойындыда, хат тізімінің төменгі жағына жеткенде Read POST жасайды; жаңа көрінбеген хат rendered cursor-дан үлкен болса оқылмай қалады. Қолмен белгілеу батырмасы да бар.

## 17. SignalR деген не?

ASP.NET Core SignalR — сервер оқиғаларын қосылған клиенттерге жеткізетін кітапхана. Сервер бөлігі .NET shared framework құрамында. Клиенттің ресми `@microsoft/signalr` 10.0.11 пакеті esbuild арқылы `wwwroot/js/social/social.js` файлына жинақталды. MIT лицензиясы бірге сақталады. CDN қолданылмайды.

## 18. Неге messaging үшін SignalR қолданылды?

Басқа адамның жаңа хаты мен кіріс достық сұрауы бетті қолмен жаңартпай көрінеді. `NewMessage` оқиғасы тек conversation/message ID-лерін береді; мәтін таратуға қосылмайды. Клиент қауіпсіз `ThreadData`, `ListData`, `Counts` endpoint-терінен өз рұқсатымен қайта оқиды. Осылай оқиға келгеннің өзінде authorization қайта орындалады.

## 19. Неге хабарламаны Hub арқылы емес HTTP POST арқылы сақтадық?

MVC жобасында authentication, antiforgery, model validation, қауіпсіз локализацияланған жауап және rate limiting бір жерде сақталады. Hub-та message-write немесе client-selected group әдістері жоқ. HTML форма JavaScript/SignalR болмаса да жұмыс істейді.

```text
Browser
   | POST + Identity cookie + Antiforgery
   v
MessagesController
   | model validation + rate limit
   v
MessagingService
   | participant + pair lock + Accepted friendship + both-direction blocks
   +---- SQL Server: INSERT + COMMIT
   |
   +---- IHubContext -> SignalR notification -> authorized GET refresh
```

## 20. IHubContext не істейді?

MVC қызметі Hub instance-ін өзі құрмайды. `SocialNotifier` inject етілген `IHubContext<ChatHub>` арқылы trusted user топтарына оқиға жібереді. Dispatch commit-тен кейін орындалады; broadcast қатесі сақталған хатты кері қайтармайды. Тасымалдауға 3 секундтық timeout бар, логта хат мәтіні жоқ. Бір retry бұрын сақталған хатты тапса, екінші NewMessage жіберілмейді.

## 21. SignalR user group қалай қауіпсіз анықталады?

`[Authorize] ChatHub.OnConnectedAsync` сервердің `Context.UserIdentifier` мәнінен `social-user:{id}` тобын құрады. Клиент топ атауын бермейді және `JoinGroup` әдісі жоқ. Бір аккаунттың бірнеше қойындысы бір топта болады. Топ атауының құпиялығына сенбейміз: нақты рұқсаттар әр MVC сұрауында қызметпен тексеріледі. Auth cookie мерзімі аяқталғанда hub connection жабылады.

## 22. SQL Server неге source of truth?

Достық, блок, хат және ReadAt SQL-де сақталады. SignalR дерек қоймасы емес, жаңарту сигналы ғана. Оқиға жоғалса, refresh/reconnect негізгі деректі қайта алады. Жұп құлпы request/accept/remove/block/open/send операцияларына ортақ: блок commit болғаннан кейін жаңа Send сол күйді көреді. Блоктан бұрын commit болған хат заңды түрде тарихта қалады.

## 23. SignalR ажыраса не болады?

HTTP send мен тарих қолжетімді. Клиент 0/2/10/30 секундтық automatic reconnect қолданады, initial start сәтсіз болса 30 секундтан кейін қайта бастайды. Қайта қосылғанда тізім, ағымдағы thread және badge жаңартылады. Шығуда/pagehide кезінде connection тоқтайды. Жіберу кезінде батырма өшіріледі; белгісіз желілік нәтиже мәтін мен idempotency кілтін сақтайды. Жаңа хат расталған соң ғана composer тазарады. Бұл offline queue емес.

Back/Forward cache-тен `pageshow.persisted` арқылы қайтқанда клиент connection-ды қайта бастайды және SQL күйін қайта оқиды. Бұл lifecycle тармағы UI logic тестінде тексерілді. Желілік қате мәтіні draft-ты жоғалтуы мүмкін reload-ты ұсынбайды: мәтін осы ашық бетте қалады, сол формадан қайта жіберуге болады.

## 24. IDOR деген не?

Insecure Direct Object Reference — URL/формадағы объект ID-ін ауыстырып, бөтен деректі оқу немесе өзгерту қатесі. Guid/long ID-ді жасыру мұны шешпейді. Authorization объектінің нақты иесіне не қатысушысына қарай тексерілуі керек.

## 25. Conversation ownership қалай тексеріледі?

SQL query-де `Id == conversationId && (UserAId == actorId || UserBId == actorId)` шарты бар. HTML history, JSON thread, list, send және Read бәрі осы ережемен жұмыс істейді. Admin рөлі bypass бермейді. Бөтен/жоқ диалог бірдей 404 алады. Send авторын browser `SenderUserId`/`UserId` өрістерінен қабылдамайды.

## 26. XSS message-терден қалай қорғалдық?

Клиент JSON мәтінін `innerHTML` арқылы салмайды; Razor да `Html.Raw` қолданбайды. Avatar бұрынғы managed WebP path/initials компонентін қайта пайдаланады. Сервер мен клиент hostile мәтінді literal етіп көрсетеді. Нақты SQL/HTTP тест және DOM sink қолданылса бірден қате беретін JS harness бар. Қорғаныс тек CSP-ге сүйенбейді.

## 27. Rate limiting не үшін керек?

Бір аккаунттың spam әрекетін шектейді. ASP.NET built-in sliding window: Send — 30/минут, Friend Request — 10/минут, басқа әлеуметтік өзгерістер — 30/минут; алты 10 секундтық segment, queue жоқ. Partition key тек trusted Identity claim. Лимит асса локализацияланған 429 және `Retry-After: 60` келеді. Read/Counts жеке күйді қайта оқу үшін бұл write bucket-терге қосылмайды. Лимит process memory-де: production scale-out үшін distributed лимит қажет.

## 28. Blocked user неге message жібере алмайды?

Send құлпының ішінде Accepted бар-жоғын және екі бағыттағы UserBlock-ты жаңадан тексереді. Браузердегі disabled composer — ыңғайлылық; нақты қорғаныс серверде. Өзге қойынды ашық тұрса да POST ағымдағы SQL күйімен бағаланады. Real-time state event composer-ді жаңартуға көмектеседі.

## 29. Friend өшірілгеннен кейін history неге қалады?

Достық — қазіргі байланыс рұқсаты, ал conversation — бұрынғы қатысушылардың тарихы. Remove/Block тек Friendship-ке әсер етеді. Қатысушылар тарихты оқиды; жіберу жаңа Accepted-ке дейін тоқтайды. Бұл нұсқада message deletion/retention UI жоқ. Production retention саясаты бөлек шешілуі тиіс.

## 30. Admin неге private message content көрмейді?

Admin Users-қа message query, body projection не жаңа хабар оқу UI қосылған жоқ. Admin да `/Messages` ішінде өз диалогтарын ғана көреді. SQL операторында базаға тікелей құқық болса, жай мәтінді оқи алады: бұл app authorization-нан бөлек сенім шекарасы. Жоба end-to-end encryption бар деп мәлімдемейді. Хаттар AI prompt-тарына, learning analytics-ке немесе жалпы leaderboard-қа берілмейді.

## 31. Localization қалай жасалды?

SharedResource-ке 73 кілт қосылды: kk-KZ / ru-RU / en-US толық сәйкес. Жалпы Shared 286 + Student 319 + Admin 149 = әр тілге 754 кілт. Dynamic tab/empty/result топтары және placeholder parity тексеріледі. Дерекқордағы есім/хат аударылмайды. Сервер UTC сақтайды; UI ағымдағы culture бойынша күн/санды көрсетеді.

## 32. CSP SignalR-мен қалай жұмыс істейді?

Бұрынғы CSP/nonce, жергілікті script/style және `connect-src 'self'` сақталды; `unsafe-eval`, жаңа CDN немесе wildcard CORS қосылған жоқ. `SocialHubOriginMiddleware` бөтен Origin және `Sec-Fetch-Site: cross-site` hub сұрауларын 403-пен тоқтатады: CORS жалғыз өзі WebSocket-ті қорғамайды. Origin жоқ native client Identity cookie арқылы авторизацияланады; Origin authentication орнына жүрмейді. Reverse proxy-де forwarded scheme/host пен нақты origin саясатын дұрыс баптау керек.

## 33. Қандай migration жасалды?

Жалғыз жаңа migration: `20261019090619_AddSocialFeatures`. Төрт кесте, normalized pair UNIQUE, directional block UNIQUE, idempotency UNIQUE, body/status/pair CHECK және қажетті lookup/paging индекстері қосылды. Identity foreign key-лері NoAction, Conversation→Messages ғана Cascade. Бұрынғы migration файлдары өзгермейді. Identity-дің стандартты GUID ID-лері сақталды; custom өте ұзын ID саясаты енгізілсе, SQL composite index byte limit-ін қайта жобалау керек.

## 34. Қандай security tests орындалды?

`tests/Enhancement8Checks`: бөлек UUID SQL базасында self/recipient/outsider, reverse-request race, concurrent Open, concurrent idempotent Send, block/send race, read cursor, history pagination, public SQL projection және DB constraints. Соңында тек өз базасын жояды.

`scripts/verify_enhancement8.py`: HTTPS cookies/antiforgery, үш пайдаланушы, Admin IDOR, local Origin, нақты WebSocket JSON protocol, XSS, locale/CSP, limits, block/unfriend/reaccept/history, барлық hub ажыратылғанда HTTP send. `social_realtime_probe.mjs` cookie-ді stdin арқылы ғана алады және хат мәтінін логтамайды; server mutation/group әдістерін шақыру қатемен аяқталатынын тексереді.

`scripts/verify_social_ui.mjs`: textContent, duplicate event, scroll preservation, Enter/Shift/IME, busy батырма, белгісіз retry, hidden-tab read, initial failure retry/reconnect және composer permission refresh. Бұл DOM logic тесті browser rendering дәлелі емес. Нақты браузер тексерісі төмендегі evidence бөлімінде бөлек көрсетіледі.

## 35. Production-та бұл social system қалай кеңейтілер еді?

Бірнеше сервер үшін Redis/Azure SignalR backplane, distributed rate limiter, дұрыс reverse-proxy WebSocket/Origin баптауы қажет. Commit пен event арасындағы process crash терезесіне transactional outbox қосуға болады. Read/unread индекстерін нақты жүктемемен өлшеп, үлкен history үшін архив/retention саясатын енгізу керек. Қауіпсіз шағым/модерация саясаты мен SQL операторларының ең аз құқықтары бөлек жобаланады; бұл Admin-ға барлық хатты әдепкіде ашу деген сөз емес. Cookie security stamp өзгерісін белсенді hub-қа жедел жеткізу қажет болса, қосымша session revocation механизмі керек. Қазіргі `CloseOnAuthenticationExpiration` cookie expiration-ды өңдейді, толық instant revocation кепілдігі емес.

## Өнімділік пен қайта іске қосу

Friends/Search — 20 карточка, conversation list — 20 диалог, history — 50 хат. Бір қосымша жол `HasNext/HasOlder` үшін ғана алынады. History cursor ID арқылы тұрақты реттеледі; бөтен beforeId қабылданбайды. Lists `AsNoTracking` және bounded projections қолданады, last preview SQL subquery-де алынады. XP бір бетке batch есептеледі; public projection private favorites query-сін сұрамайды. Leaderboard ұпай формуласы және ranking тәртібі өзгермейді.

```powershell
npm ci
npm run build:social
dotnet build
dotnet ef database update
dotnet ef migrations has-pending-model-changes
dotnet run --project tests/Enhancement8Checks
node scripts/verify_social_ui.mjs
# HTTPS app бөлек іске қосылғанда:
python scripts/verify_enhancement8.py
python scripts/verify_resources.py
git diff --check
```

SQL/HTTP suite-тер жергілікті SQL Server Windows authentication және development HTTPS app-ты күтеді. Test fixture-лер UUID email/database арқылы шектеледі. Ақылы AI режимін қоспау керек.

## Ресми дереккөздер

- [SignalR security](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-10.0): WebSocket Origin және жеке дерек қауіпсіздігі.
- [SignalR authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0): cookie, trusted UserIdentifier және authorization.
- [JavaScript client](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0): automatic reconnect және initial start retry айырмасы.
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0): partitioned sliding-window limiter.

## Браузерде қосымша қолмен тексеруге арналған тізім

Нақты in-app браузерде 2026-10-19 күні тексерілді: қонақ рейтингінен ашық профильге өту; тест аккаунтының достар карточкасы; unread badge; populated thread; Enter арқылы жіберу және composer тазалануы; literal `<script>`; жаңа хаттың reload-сіз пайда болуы; оқылғаннан кейін unread=0; logout. 390×844 телефон, 768×1024 планшет және 1440×1000 desktop көріністері қаралды. Телефонда conversation list жасырылып, back сілтемесі шықты; desktop-та екі баған бар. Көлденең overflow байқалмады. KZ, EN және RU беттері көрінді; браузер error/warn логы бос болды. Viewport override қалпына келтірілді, тест қойындысы жабылды, екі visual fixture аккаунты мен хаттары жойылды.

Бұл **бір браузер + екінші аккаунттың HTTP POST-ы** арқылы live rendering тексерісі. Екі бөлек толық браузердің қатар жұмысын тексердік деген мәлімдеме емес. Одан бөлек автоматты suite үш нақты authenticated WebSocket connection-ды тексерді. Браузер тексерісі кезінде табылған ambient route ID қатесі түзетіліп, HTTP regression assertion қосылды.

- Екі бөлек browser profile/device арқылы бір мезетте хат алмасу; бірнеше қойындыдағы unread күйі.
- Ұзақ ұйқыдан оралу, желіні өшіру/қосу, cookie expiry және logout/login ауысуы.
- iOS/Android экран пернетақтасы, Safari/Firefox және screen-reader navigation.
- Жүктемемен 20-дан көп диалог және 50-ден көп хат; жоғарыда қалған scroll кезінде жаңа хат келуі.
- Deployment reverse proxy артында WebSocket, same-origin және CSP логтары.

Бұл тізім автоматты HTTP/WebSocket тестін алмастырмайды; нақты device және deployment ортаға арналған.

## Соңғы тексеру дәлелі

| Тексеру | Нәтиже |
|---|---|
| Өзгеріске дейінгі baseline | Build, EF/current database, diff check және бұрынғы 22 suite өтті |
| Соңғы build | 0 error, 0 warning; `obj/enhancement8-build-final.log` |
| EF database update | Database already up to date; `obj/enhancement8-final-database.log` |
| Pending model changes | Жоқ; `obj/enhancement8-final-model.log` |
| Бұрынғы 22 regression suite | Барлығы өтті; `obj/enhancement8-final-results.json` — жеке log/evidence тізімі |
| Жаңа SQL/service/concurrency/paging suite | Өтті; `obj/enhancement8-service-tests-final.log` |
| Жаңа HTTPS/SQL/real WebSocket suite | Өтті; `obj/enhancement8-http-tests-final.log` |
| Social UI logic suite | Өтті; `node scripts/verify_social_ui.mjs` |
| Resource parity | 9 resx, әр тілге 754 кілт; placeholder және dynamic family сәйкестігі өтті |
| C# purpose comments | Жаңа әдістер/конструкторлар және ConfigureSocial қазақша қысқа түсіндірмемен тексерілді; generated migration exempt |
| Migration саны | Бір жаңа `AddSocialFeatures`; бұрынғы migration файлдары өзгермеген |
| Ақылы OpenAI | 0 шақыру; SDK/AI regression-дер offline немесе сақталған fixture нәтижесін пайдаланды |
| git diff --check | Өтті |

Бұрынғы suite тізімі: resources, editor-csp, editor-ui, phase2, phase3, phase4, phase5, enhancement1, enhancement2, enhancement3, enhancement4, enhancement5a, enhancement5b, extra-tasks, Phase4Checks, Enhancement1Checks, Enhancement3Checks, Enhancement5bChecks, Enhancement6Checks, enhancement6, Enhancement7Checks, enhancement7. Үш жаңа suite-пен бірге 25 тексеру жинағы өтті.

Үш ескі leaderboard privacy assertion жаңа келісілген URL-ді ескереді: ID тек `/Users/{id}` href ішінде рұқсат; email және көрінетін ID әлі тыйым салынған. Public action `Users/Profile` деп аталып, сыртқы `/Users/{id}` URL-і сақталды; бұл Admin Users/Details сілтемелерімен action-name қақтығысын болдырмайды. Admin pagination/profile regression осы түзетуден кейін қайта өтті.

Бастапқы ProgrammingTasks, TestCases, Topics, Lessons, Runtimes, Submissions, ExecutionResults, AiFeedbacks, AcceptedCodeReviews, LessonProgresses, TaskFavorites, пайдаланушы ID/CreatedAt және DisplayName/AvatarPath/CreatedAt SHA-256 хэштері өзгермеді. Leaderboard rebuild regression `UpdatedAt` мәндерін жаңартты, сондықтан кестенің толық хэші өзгерді; барлық score/solved/submission counters сақталған тарихтан тәуелсіз қайта есептеліп расталды. Дәлел: `obj/enhancement8-data-preservation.json`.

Уақытша social/visual пайдаланушылар, Friendship/UserBlock/Conversation/Message жолдары, жеке Enhancement8 тест базалары, avatar test файлдары және Docker runner ресурстары тазартылды. Нақты пайдаланушылардың достығы не хаттары өзгертілген жоқ. Git commit жасалған жоқ.

## Тәуелділік аудитінің шектеуі

Build warning пен npm advisory бір ұғым емес. SignalR қосылғаннан кейінгі `npm audit` бұрыннан бар Monaco 0.57.0 → DOMPurify 3.4.15 тізбегінде екі low advisory көрсетті; осы екі пакет нұсқасы baseline-мен бірдей. Social rendering DOMPurify/Markdown қолданбайды. Бұл тапсырмада Monaco-ны force downgrade жасау жүргізілмеді. Бөлек dependency жаңартуында DOMPurify advisory-лерін қарап, editor bundle мен CSP regression-ін қайта тексеру керек.

## File manifest

Created (42):

- `ClientScripts/licenses/SIGNALR-LICENSE.txt`
- `ClientScripts/social-dom.js`
- `ClientScripts/social.js`
- `Controllers/FriendsController.cs`
- `Controllers/MessagesController.cs`
- `Controllers/SocialController.cs`
- `Controllers/SocialResponses.cs`
- `Controllers/UsersController.cs`
- `Hubs/ChatHub.cs`
- `Migrations/20261019090619_AddSocialFeatures.Designer.cs`
- `Migrations/20261019090619_AddSocialFeatures.cs`
- `Models/DirectConversation.cs`
- `Models/DirectMessage.cs`
- `Models/Enums/FriendshipStatus.cs`
- `Models/Friendship.cs`
- `Models/UserBlock.cs`
- `Services/Security/SocialHubOriginMiddleware.cs`
- `Services/Social/FriendshipService.cs`
- `Services/Social/MessagingService.cs`
- `Services/Social/SocialNotificationService.cs`
- `Services/Social/SocialNotifier.cs`
- `Services/Social/SocialPairLock.cs`
- `Services/Social/SocialProfileService.cs`
- `Services/Social/SocialRateLimits.cs`
- `Services/Social/SocialState.cs`
- `ViewComponents/SocialNavigationViewComponent.cs`
- `ViewModels/Social/SocialViewModels.cs`
- `Views/Friends/Index.cshtml`
- `Views/Messages/Index.cshtml`
- `Views/Shared/Components/SocialNavigation/Default.cshtml`
- `Views/Shared/_SocialActions.cshtml`
- `Views/Users/Details.cshtml`
- `scripts/build-social.mjs`
- `scripts/social_realtime_probe.mjs`
- `scripts/verify_enhancement8.py`
- `scripts/verify_social_ui.mjs`
- `study-enhancement8.md`
- `tests/Enhancement8Checks/Enhancement8Checks.csproj`
- `tests/Enhancement8Checks/Program.cs`
- `wwwroot/css/social.css`
- `wwwroot/js/social/SIGNALR-LICENSE.txt`
- `wwwroot/js/social/social.js`

Modified (20):

- `Data/ApplicationDbContext.cs`
- `Migrations/ApplicationDbContextModelSnapshot.cs`
- `Program.cs`
- `README.md`
- `Resources/SharedResource.en-US.resx`
- `Resources/SharedResource.kk-KZ.resx`
- `Resources/SharedResource.ru-RU.resx`
- `Services/Leaderboards/LeaderboardService.cs`
- `Services/Progress/ExperienceService.cs`
- `ViewModels/Leaderboard/LeaderboardViewModel.cs`
- `Views/Account/Profile.cshtml`
- `Views/Home/Privacy.cshtml`
- `Views/Leaderboard/Index.cshtml`
- `Views/Shared/_Layout.cshtml`
- `package-lock.json`
- `package.json`
- `scripts/verify_enhancement6.py`
- `scripts/verify_phase4.py`
- `scripts/verify_phase5.py`
- `scripts/verify_resources.py`
