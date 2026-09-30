# Phase 1: дерекқор, модельдер және Entity Framework Core

Бұл құжат IntelligentProgrammingPlatform жобасының бірінші кезеңін түсіндіреді. Осы кезеңнің мақсаты — кейінгі мүмкіндіктерге арналған деректер құрылымын дайындау. Кодты орындау, пайдаланушыны жүйеге кіргізу және AI көмекшісі бұл кезеңге кірмейді.

## 1. Phase 1-де не жасадық?

Платформаның негізгі модельдері мен күй түрлерін (`enum`) анықтадық, олардың байланыстарын `ApplicationDbContext` ішінде сипаттадық және SQL Server қосылымын баптадық. ASP.NET Core Identity пайдаланушы моделін қазірден қолданамыз: бұл келесі кезеңде қауіпсіз тіркелу мен кіруді қосуға негіз болады.

Негізгі файлдар:

- `Models/` — дерекқорда сақталатын объектілердің C# кластары.
- `Models/Enums/` — есеп күрделілігі мен тексеру күйлері.
- `Data/ApplicationDbContext.cs` — EF Core моделі, байланыстар және индекстер.
- `Program.cs` — дерекқор контекстін тәуелділіктер контейнеріне тіркеу.
- `appsettings.json` — SQL Server қосылымының баптауы.
- `Migrations/` — дерекқор схемасын құруға арналған `InitialCreate` миграциясы мен модель күйінің файлдары.
- `dotnet-tools.json` — осы репозиторийге арналған жергілікті `dotnet-ef` құралының манифесі.

Құрылымды дайындау мен оны SQL Server-де сәтті құру — екі бөлек нәтиже. Нақты тексеру нәтижелері құжаттың соңында көрсетіледі.

## 2. Entity Framework Core деген не?

Entity Framework Core, қысқаша EF Core — C# объектілері мен реляциялық дерекқор арасындағы жұмысты жеңілдететін кітапхана. Мұндай құрал ORM деп аталады.

Мысалы, `Topic` класы бір тақырыпты сипаттайды, ал `Topics` кестесінің әр жолы бір тақырыпты сақтайды. `Id` пен `Name` сияқты қасиеттер кесте бағандарына айналады. EF Core модельге сүйеніп SQL сұрауларын жасайды, деректерді оқиды және `SaveChangesAsync()` шақырылғанда өзгерістерді сақтайды.

EF Core SQL Server-дің орнына жүрмейді. SQL Server деректерді сақтайды, ал EF Core қолданбаға сол деректермен жұмыс істеуге көмектеседі. Бірінші кезеңде модельдер мен схема дайындалады; деректерді басқару интерфейсі кейін жасалады.

## 3. Migration деген не?

Migration, яғни миграция — дерекқор схемасының белгілі бір өзгерісін сипаттайтын C# файлдары. Схемаға кестелер, бағандар, алғашқы және сыртқы кілттер, индекстер кіреді.

Модельге жаңа қасиет қосу бар дерекқорды автоматты түрде өзгертпейді. Әдеттегі жұмыс реті:

1. C# моделін немесе оның EF Core баптауын өзгерту.
2. `dotnet ef migrations add МиграцияАтауы` арқылы өзгерісті сипаттайтын миграция жасау.
3. Жасалған миграцияны қарап шығу.
4. `dotnet ef database update` арқылы өзгерісті дерекқорға қолдану.

Миграция жасау үшін SQL Server-ге қосылу міндетті емес: EF Core модельден схема өзгерісін шығара алады. Ал оны дерекқорға қолдану үшін SQL Server қолжетімді болуы және пайдаланушының жеткілікті рұқсаты болуы керек. Сондықтан миграция файлының жасалуы дерекқордың да құрылғанын білдірмейді.

## 4. ApplicationDbContext деген не?

`ApplicationDbContext` — қолданба мен дерекқор арасындағы негізгі жұмыс нүктесі. Ол қосылым баптауын қабылдайды, EF Core моделін сипаттайды және объектілердегі өзгерістерді бақылайды.

Оның мұрагерлігі:

```csharp
ApplicationDbContext : IdentityDbContext<ApplicationUser>
```

`IdentityDbContext<ApplicationUser>` Identity пайдаланушылары мен басқа стандартты Identity объектілерін модельге қосады. Контексте платформаның жеке кестелеріне арналған `DbSet` қасиеттері бар: `Topics`, `ProgrammingTasks`, `TestCases`, `Runtimes`, `Submissions`, `ExecutionResults` және `Leaderboards`.

`OnModelCreating` әдісіндегі Fluent API қасиеттердің міндеттілігін, мәтін ұзындықтарын, байланыстарды, индекстерді және өшіру тәртібін анықтайды. Алдымен міндетті түрде:

```csharp
base.OnModelCreating(modelBuilder);
```

шақырылады. Ол Identity-дің стандартты кестелері, кілттері мен байланыстарын баптайды. Одан кейін жобаның жеке баптауы орындалады.

## 5. Әр Model не үшін керек?

| Модель | Мақсаты және негізгі деректері |
| --- | --- |
| `ApplicationUser` | Платформа пайдаланушысы. Identity өрістеріне қосымша `DisplayName` және `CreatedAt` сақтайды. Пайдаланушының жіберілімдері мен рейтинг жазбасына байланысады. |
| `Topic` | «Циклдер», «Массивтер» сияқты есеп тақырыбы. `Name` және міндетті емес `Description` сақтайды. Бір тақырыпта бірнеше есеп болады. |
| `ProgrammingTask` | Есептің шарты. `Title`, `Slug`, `Description`, `Difficulty`, `TopicId`, уақыт пен жад шектері, `IsPublished`, `CreatedAt` сақталады. `Slug` — есепке арналған бірегей мәтіндік белгі. |
| `TestCase` | Бір есептің тексеру деректері: `Input`, `ExpectedOutput`, `IsHidden`, `Order`. `ProgrammingTaskId` оның қай есепке тиесілі екенін көрсетеді. |
| `Runtime` | Қолдау көрсетілетін бағдарламалау тілі мен орындау ортасының сипаттамасы: `Name`, `LanguageKey`, `Version`, `FileExtension`, `CompileCommand`, `RunCommand`, `DockerImage`, `IsEnabled`. Мысалы, тіл кілті `cpp`, файл кеңейтімі `.cpp` болуы мүмкін. Бұл кезеңде командалар тек мәтін ретінде сақталады. |
| `Submission` | Студенттің бір рет жіберген шешімі. `UserId`, `ProgrammingTaskId`, `RuntimeId`, `SourceCode`, `Status`, уақыт белгілері, тест саны және компиляция нәтижелері сақталады. Бір студент бір есепке бірнеше шешім жібере алады. |
| `ExecutionResult` | Бір жіберілімнің бір тест бойынша нәтижесі. `SubmissionId`, `TestCaseId`, `Status`, `ActualOutput`, `ErrorMessage`, `ExitCode`, `ExecutionTimeMs`, `MemoryUsedKb` сақталады. |
| `Leaderboard` | Бір пайдаланушының рейтингке арналған жинақталған көрсеткіштері: `SolvedTasks`, `SuccessfulSubmissions`, `TotalSubmissions`, `Score`, `UpdatedAt`. Бұл көрсеткіштерді есептейтін логика әлі жасалған жоқ. |

`ProgrammingTask` атауы әдейі таңдалған. C# тіліндегі `System.Threading.Tasks.Task` асинхронды операцияларды сипаттайды, сондықтан есеп моделін `Task` деп атау түсініксіздік туғызар еді.

Жаңа есептің бастапқы шектері — `TimeLimitMs = 2000` миллисекунд және `MemoryLimitMb = 256` мегабайт. Бұл сақталатын баптаулар ғана; олар кодты орындамайды және шектеуді өздігінен қолданбайды. Уақыт белгілерінің бастапқы мәндері `DateTime.UtcNow` арқылы UTC бойынша беріледі. C# инициализаторы жаңа объект жасалғанда жұмыс істейді; оны SQL Server-дегі `DEFAULT` шектеуімен шатастырмау керек.

`string?`, `DateTime?`, `int?`, `long?`, `bool?` түрлері мәннің әлі болмауы мүмкін екенін көрсетеді. Мысалы, жаңа жіберілімде `StartedAt` пен `FinishedAt` бос болады. `CompileSucceeded = null` компиляцияның нәтижесі белгісіз екенін, ал `false` оның сәтсіз аяқталғанын білдіреді. `ExecutionTimeMs = null` мен `0` да бір мағына емес.

Күйлер бөлек `enum` түрлерімен берілген:

- `Difficulty`: `Easy`, `Medium`, `Hard` — есеп күрделілігі.
- `SubmissionStatus`: `Pending`, `Compiling`, `Running`, `Accepted`, `WrongAnswer`, `CompilationError`, `RuntimeError`, `TimeLimitExceeded`, `MemoryLimitExceeded`, `InternalError` — бүкіл жіберілімнің күйі.
- `ExecutionStatus`: `Pending`, `Passed`, `WrongAnswer`, `RuntimeError`, `TimeLimitExceeded`, `MemoryLimitExceeded`, `Skipped`, `InternalError` — жеке тестің күйі.

Бұл мәндер дерекқорда бүтін сан (`int`) ретінде сақталады. Кодтағы атаулар оқуға ыңғайлы, ал бекітілген сандық мәндер деректердің тұрақтылығын қамтамасыз етеді. Кейін бар мәннің санын ауыстыру бұрынғы жазбалардың мағынасын өзгертіп жіберуі мүмкін.

## 6. Primary Key деген не?

Primary Key, яғни алғашқы кілт — кестедегі әр жолды бірегей анықтайтын мән. Біздің модельдерде ол әдетте `Id` деп аталады.

- `Topic`, `ProgrammingTask`, `TestCase`, `Runtime`, `Leaderboard` үшін `Id` түрі — `int`.
- `Submission` және `ExecutionResult` үшін — `long`, себебі жіберілімдер мен жеке тест нәтижелерінің саны көп болуы мүмкін.
- `ApplicationUser` үшін Identity-дің стандартты `string` түріндегі `Id` қолданылады.

SQL Server-де `int` және `long` кілттері тиісінше `int` және `bigint` бағандарына сәйкес келеді. Сандық `Id` мәндері жаңа жол қосылғанда дерекқор арқылы жасалады. Пайдаланушының `Id` мәні жол түрінде сақталады; оны санға айналдыру қажет емес.

## 7. Foreign Key деген не?

Foreign Key, яғни сыртқы кілт — басқа кестедегі жолға сілтеме жасайтын баған.

Мысалы, `ProgrammingTask.TopicId = 3` болса, есеп `Topics` кестесіндегі `Id = 3` тақырыбына тиесілі. Сыртқы кілт шектеуі жоқ тақырыпқа сілтейтін есепті сақтауға жол бермейді.

`Submission.UserId` пайдаланушының `ApplicationUser.Id` мәніне сілтейді, сондықтан оның түрі де `string`. `ExecutionResult.SubmissionId` мәні `long`, өйткені `Submission.Id` түрі — `long`.

Сыртқы кілт байланыс тұтастығын сақтайды. Ол өздігінен пайдаланушының рұқсаттарын тексермейді және бизнес ережелерінің бәрін алмастырмайды.

## 8. Navigation Property деген не?

Navigation Property, яғни навигациялық қасиет — байланысты объектіге немесе объектілер жиынына C# арқылы өтуге арналған қасиет.

Мысалы:

```csharp
public int TopicId { get; set; }
public Topic Topic { get; set; } = null!;
```

`TopicId` дерекқорда сақталатын кілтті береді, ал `Topic` байланысты тақырып объектісіне қол жеткізуге арналған. Кері бағытта `Topic.ProgrammingTasks` сол тақырыптың есептер жиынын білдіреді.

Коллекциялар бос `List<T>` арқылы бастапқы мән алады. Сондықтан жаңа объектінің коллекциясына элемент қосқанда `null` қатесі болмайды. Міндетті навигациядағы `null!` жазуы компиляторға бұл байланысты кейін EF Core толтыратынын білдіреді; ол байланысты объектіні автоматты түрде жүктемейді. Оқу кезінде қажет байланыстарды, мысалы, `Include` арқылы жүктеу керек.

## 9. Entity-лер бір-бірімен қалай байланысқан?

Мұндағы `1` — бір жазба, `көп` — бірнеше байланысты жазба, `0..1` — жазба болмауы немесе біреу болуы мүмкін деген сөз.

```text
ApplicationUser (AspNetUsers)
   |
   +---- 1 -> көп Submissions
   |
   +---- 1 -> 0..1 Leaderboard

Topic
   |
   +---- 1 -> көп ProgrammingTasks
                    |
                    +---- 1 -> көп TestCases
                    |                 |
                    |                 +---- 1 -> көп ExecutionResults
                    |
                    +---- 1 -> көп Submissions
                                      |
                                      +---- 1 -> көп ExecutionResults

Runtime
   |
   +---- 1 -> көп Submissions
```

Әр `Submission` бір пайдаланушыға, бір есепке және бір орындау ортасына тиесілі. Әр `ExecutionResult` бір жіберілім мен бір тестке сілтейді. Бір тест әртүрлі жіберілімдерде қайта тексеріле алады.

`ApplicationUser` мен `Leaderboard` байланысы — бірден бірге байланыс. Пайдаланушының рейтинг жазбасы әлі құрылмауы мүмкін, сондықтан навигациясы `Leaderboard?`. Ал әр рейтинг жазбасы міндетті түрде бір пайдаланушыға тиесілі. `UserId` бірегей болғандықтан, бір пайдаланушыға екінші рейтинг жазбасын енгізу мүмкін емес. Бұл байланыс рейтинг жолын автоматты түрде құрмайды.

Бірегей индекстер мен шектеулер:

| Баған немесе бағандар жұбы | Не үшін керек? |
| --- | --- |
| `Topic.Name` | Бірдей атаулы тақырыптарды қайталамау үшін. |
| `ProgrammingTask.Slug` | Әр есептің мәтіндік белгісін бірегей ету үшін. |
| `Runtime.LanguageKey` | Бір тіл кілтіне бірнеше орындау ортасы жазбасын қоспау үшін. Осы схема әр тіл кілтіне бір конфигурация сақтайды. |
| `Leaderboard.UserId` | Бір пайдаланушыға ең көбі бір рейтинг жазбасын сақтау үшін. |
| `TestCase(ProgrammingTaskId, Order)` | Бір есептің ішінде бірдей реттік нөмірлі екі тест болмауы үшін. Басқа есепте сол нөмір қолданылуы мүмкін. |
| `ExecutionResult(SubmissionId, TestCaseId)` | Бір жіберілім мен бір тест жұбына бір ғана нәтиже сақтау үшін. |

`Submission.UserId`, `ProgrammingTaskId`, `CreatedAt`, `Status` сияқты бағандар бойынша индекстер тиісті деректерді іздеуге көмектеседі. Қарапайым индекс қайталанатын мәндерге рұқсат береді, ал бірегей индекс қайталануға тыйым салады.

Өшіру тәртібі тарихи деректерді сақтауға бағытталған. Платформаның негізгі ата-ана байланыстарында `DeleteBehavior.NoAction` қолданылады: мысалы, жіберілімдері бар пайдаланушыны немесе есепті өшіру оған қатысты тарихты автоматты түрде жоймайды. Байланысты жазбалар бар кезде дерекқор өшіруді қабылдамайды; оларды өңдеу туралы шешім кейінгі қолданба логикасында қабылданады.

`Submission -> ExecutionResults` байланысында `Cascade` бар: жіберілім әдейі өшірілсе, оның жеке тест нәтижелері де өшіріледі. Басқа негізгі жолдардағы `NoAction` SQL Server-дегі бірнеше каскадтық өшіру жолының қақтығысын болдырмайды. Identity-дің ішкі байланыстары өзінің стандартты баптауын сақтайды. Каскадтық өшіру және SQL Server шектеулері туралы [EF Core ресми құжаттамасында](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete) түсіндірілген.

## 10. ApplicationUser неге IdentityUser-дан мұра алады?

ASP.NET Core Identity пайдаланушы аккаунттарын қауіпсіз басқаруға арналған. `IdentityUser` ішінде пайдаланушы идентификаторы, аты, email, пароль хэші және басқа стандартты өрістер бар. `ApplicationUser` сол дайын модельді кеңейтіп, платформаның `DisplayName` және `CreatedAt` өрістерін қосады. Жеке пайдаланушы моделін және `IdentityDbContext<ApplicationUser>` контекстін құру тәсілі [ASP.NET Core 10 ресми құжаттамасына](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0) сәйкес келеді.

Жеке ашық мәтіндік `Password` өрісі қосылмайды. Identity парольдің өзін емес, оның хэшін сақтауға арналған `PasswordHash` өрісін пайдаланады. Тіркелу мен парольді тексеру кейін Identity қызметтері арқылы орындалуы керек.

Қазір Identity моделінің болуы — кіру жүйесі дайын деген сөз емес. Login/Register беттері, cookie арқылы аутентификация және рұқсаттарды басқару бірінші кезеңде іске асырылмайды.

## 11. appsettings.json-тағы connection string не үшін керек?

Connection string қолданбаға қай SQL Server-ге және қай дерекқорға қосылу керегін айтады:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=IntelligentProgrammingPlatformDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
}
```

- `Server=localhost` — осы компьютердегі әдепкі SQL Server данасы. Windows қызметінің аты `MSSQLSERVER` болғанымен, әдепкі данаға қосылу үшін `localhost` жеткілікті.
- `Database=IntelligentProgrammingPlatformDb` — жоба дерекқорының атауы.
- `Trusted_Connection=True` — Windows аккаунты арқылы қосылу. Файлда дерекқор паролі сақталмайды.
- `TrustServerCertificate=True` — жергілікті SQL Server сертификатына сену баптауы. Өндірістік ортада сертификат пен қосылым қауіпсіздігі бөлек дұрыс бапталуы керек.
- `MultipleActiveResultSets=True` — бір қосылымда бірнеше белсенді нәтиже жиынымен жұмыс істеуге мүмкіндік береді.

Бұл мәндер `appsettings.json` ішіндегі басқа баптаулармен бірге жарамды JSON объектісінде орналасады.

## 12. Program.cs-та database қалай тіркелді?

`ApplicationDbContext` тәуелділіктер контейнеріне SQL Server провайдерімен тіркеледі:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.")));
```

`GetConnectionString("DefaultConnection")` конфигурациядағы қосылым жолын оқиды. Егер ол табылмаса, `InvalidOperationException` нақты қате хабарын береді. `UseSqlServer` EF Core-ға SQL Server провайдерін қолдануды тапсырады. `AddDbContext` арқылы кейін контроллер немесе қызмет контексті конструктор параметрімен ала алады. Әдепкіде контекст бір HTTP сұрауы аясында қолданылады.

Identity кестелерін модельдеу және миграция жасау үшін контекстің `IdentityDbContext<ApplicationUser>` класынан мұра алуы мен Identity EF Core пакетінің болуы жеткілікті. Осы кезеңде `AddIdentity`, `AddDefaultIdentity`, Login/Register маршруттарын немесе аутентификация ағынын қосу қажет емес. Қолданыстағы MVC баптауы сақталады.

## 13. SQL Server-де қандай кестелер пайда болды?

`InitialCreate` миграциясы сәтті қолданылып, `IntelligentProgrammingPlatformDb` ішінде келесі жеті домендік кесте құрылды:

```text
Topics
ProgrammingTasks
TestCases
Runtimes
Submissions
ExecutionResults
Leaderboards
```

Identity стандартты жеті кесте қосты:

| Кесте | Мақсаты |
| --- | --- |
| `AspNetUsers` | Пайдаланушылар, соның ішінде `DisplayName` және `CreatedAt`. |
| `AspNetRoles` | Рөлдердің анықтамалары. |
| `AspNetUserRoles` | Пайдаланушы мен рөл арасындағы байланыс. |
| `AspNetUserClaims` | Пайдаланушы туралы Identity тұжырымдары (`claim`). |
| `AspNetRoleClaims` | Рөлге қатысты Identity тұжырымдары. |
| `AspNetUserLogins` | Сыртқы кіру провайдерлерімен байланыстар. |
| `AspNetUserTokens` | Identity токендеріне арналған жазбалар. |

EF Core қолданылған миграцияларды `__EFMigrationsHistory` қызметтік кестесінде тіркейді. Осылайша нақты тексерілген құрылым — жеті домендік кесте, жеті Identity кестесі және миграциялар тарихының бір кестесі, барлығы 15 кесте. `ApplicationUser` үшін бөлек `ApplicationUsers` кестесі жасалмайды: пайдаланушылар `AspNetUsers` ішінде сақталады.

Identity кестелерінің болуы рөлдер немесе аккаунттар автоматты түрде толтырылды дегенді білдірмейді. Бірінші кезең рөлдерді басқару мүмкіндігін де іске асырмайды. Кестелердің құрылғаны SQL Server-ден тек оқуға арналған `sqlcmd` сұрауымен расталды.

## 14. InitialCreate migration қалай жұмыс істейді?

Бірінші миграцияның логикалық аты — `InitialCreate`; EF Core уақыт белгісімен бірге оның толық идентификаторы — `20260930082441_InitialCreate`. Миграция үш негізгі бөліктен тұрады:

- `..._InitialCreate.cs` ішіндегі `Up()` кестелерді, кілттерді, байланыстарды және индекстерді құруды сипаттайды.
- Сол файлдағы `Down()` осы миграцияны кері қайтару әрекеттерін сипаттайды. Бастапқы миграция үшін бұл кестелерді өшіруге және олардың деректерін жоғалтуға әкеледі, сондықтан кері қайтаруды жай тексеру үшін орындамау керек.
- `..._InitialCreate.Designer.cs` миграцияға қатысты модель метадеректерін, ал `ApplicationDbContextModelSnapshot.cs` келесі миграциямен салыстыруға арналған модельдің соңғы күйін сақтайды.

Қалыпты командалар реті:

```powershell
dotnet build
dotnet ef --version
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet build
```

Алғашқы тексеруде `dotnet-ef` қолжетімсіз болды. Сондықтан осы репозиторийге жергілікті құрал манифесін құрып, тұрақты `10.0.12` нұсқасын орнаттық:

```powershell
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.12 --local
```

Бұл .NET 10 ортасында манифест репозиторий түбіріндегі `dotnet-tools.json` файлына жасалды; ол `.config/` ішінде емес. Құрал жүйеге жаһандық түрде орнатылған жоқ. Енді `InitialCreate` бар, сондықтан оны қайтадан дәл сол атпен жасау қажет емес. Нақты орындалған командалардың жинағы төменде берілген.

`database update` жетпей тұрған миграцияларды ретімен орындайды. Қажет дерекқор жоқ болса, SQL Server рұқсаттары жеткілікті болғанда оны жасайды. Әр қолданылған миграция `__EFMigrationsHistory` кестесіне жазылады. Кейін сол команданы қайта орындағанда бұрын қолданылған миграциялар қайта жасалмайды.

## 15. Келесі Phase-та не жасалуы керек?

Phase 2-де Identity негізінде қауіпсіз тіркелу мен кіруді дайындау керек: тиісті қызметтерді тіркеу, Login/Register беттері, шығу, енгізілген деректерді тексеру және қорғалатын беттерге қолжетімділікті ұйымдастыру. Қажет болса, сол кезеңнің нақты талабына сәйкес рөлдер мен рұқсаттар анықталады.

Есептер интерфейсі, код редакторы, код жіберу, тестілеу, AI түсіндірмелері, статистика және рейтинг есептеу кейінгі кезеңдерге жатады. Олардың ешқайсысы осы бірінші кезеңде іске асырылмайды.

`SourceCode` әзірге тек дерекқорда сақталатын мәтін. Студент кодын ASP.NET серверінің процесінде орындауға болмайды. Болашақ орындау жүйесі CPU, жад, уақыт және желі шектеулері бар оқшауланған Docker контейнерлерін қолдануы керек. `Runtime` ішіндегі команда мәтіндері бұл кезеңде орындалмайды. `IsHidden` өрісі де жасырын тесті өздігінен қорғамайды: кейін оларды студентке қайтармайтын серверлік сұраулар мен интерфейс ережелері қажет болады.

## Нақты тексеру нәтижелері

2026 жылғы 30 қыркүйекте бірінші кезеңнің құрастыруы, миграциясы және SQL Server дерекқоры тексерілді:

| Тексеру | Күйі |
| --- | --- |
| Бастапқы `dotnet build` | Сәтті: 0 қате, 0 ескерту. |
| `dotnet ef --version` | Бастапқыда құрал болмады; жергілікті орнатудан кейін `10.0.12` расталды. |
| `InitialCreate` миграциясын жасау | `20260930082441_InitialCreate` сәтті жасалды. |
| `dotnet ef database update` | Сәтті: `localhost` серверінде Windows аутентификациясы арқылы `IntelligentProgrammingPlatformDb` құрылды. |
| Домендік және Identity кестелерін тексеру | `sqlcmd` арқылы барлық 15 кестенің бар екені расталды. |
| Миграциялар тарихы | `__EFMigrationsHistory` ішінде `20260930082441_InitialCreate`, EF Core нұсқасы `10.0.12` сақталған. |
| Сыртқы кілттер | Доменнің 8 сыртқы кілті де қосулы және сенімді күйде. Тек `ExecutionResults -> Submissions` кілтінде `CASCADE`, қалған жетеуінде `NO_ACTION`. |
| Бірегей индекстер | 9-бөлімдегі 6 домендік бірегей индекс расталды. |
| `dotnet ef migrations has-pending-model-changes` | Модель мен миграция күйінің арасында қолданылмаған өзгеріс жоқ. |
| Соңғы `dotnet build` | Сәтті: 0 қате, 0 ескерту. |
| Phase 2 | Іске асырылған жоқ. |

Орнатудың алғашқы әрекеттерінде ортаның шектеулері кездесті: пакет орнату sandbox ішінде `127.0.0.1:9` проксиіне қосыла алмай, `NU1301` қатесін берді; құрал манифесін жасау кезінде шаблон кэшіне қолжетімділікке тыйым салынды. Сол командалар қажетті рұқсатпен қайта орындалып, сәтті аяқталды. Бұл қателер жоба кодындағы құрастыру қатесі емес; аяқталмаған орнату немесе дерекқор қатесі қалған жоқ.

### Нақты орындалған баптау және тексеру командалары

Командалар жоба түбірінде PowerShell арқылы орындалды. Рұқсат шектеуіне байланысты қайталанған орнату әрекеттері жоғарыда түсіндірілген; төменде әр команданың нақты мәтіні көрсетілген:

```powershell
dotnet --info
dotnet nuget locals global-packages --list
dotnet tool list --local
dotnet tool list --global
Get-Service -Name MSSQLSERVER -ErrorAction SilentlyContinue
dotnet add IntelligentProgrammingPlatform.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.12
dotnet build
dotnet ef --version
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.12 --local
dotnet ef --version
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet ef migrations has-pending-model-changes
dotnet build
sqlcmd -S localhost -E -C -d IntelligentProgrammingPlatformDb -b -W -s "|" -Q "SET NOCOUNT ON; SELECT DB_NAME() AS DatabaseName; SELECT name AS TableName FROM sys.tables ORDER BY name; SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory; SELECT OBJECT_NAME(parent_object_id) AS ChildTable, OBJECT_NAME(referenced_object_id) AS ParentTable, delete_referential_action_desc AS DeleteBehavior, is_disabled, is_not_trusted FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id) NOT LIKE 'AspNet%' ORDER BY ChildTable, ParentTable; SELECT OBJECT_NAME(object_id) AS TableName, name AS IndexName, is_unique FROM sys.indexes WHERE is_primary_key = 0 AND name IS NOT NULL AND OBJECT_NAME(object_id) IN ('Topics', 'ProgrammingTasks', 'TestCases', 'Runtimes', 'Submissions', 'ExecutionResults', 'Leaderboards') ORDER BY TableName, IndexName;"
```
