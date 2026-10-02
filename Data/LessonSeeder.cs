using IntelligentProgrammingPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Data;

public static class LessonSeeder
{
    // Даму ортасының бес сабағын бар мазмұн мен ретті өзгертпей қосады.
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var topics = await db.Topics.ToDictionaryAsync(topic => topic.Name, topic => topic.Id,
            StringComparer.OrdinalIgnoreCase);
        var lessons = new[]
        {
            new Lesson
            {
                Title = "Бағдарламалау негіздері", Slug = "programming-basics", TopicId = topics["Basics"], Order = 1,
                Summary = "Кіріс, өңдеу және шығыс ұғымдарын түсініп, алғашқы өрнектің нәтижесін болжаңыз.",
                Content = """
                    Бағдарлама — компьютер орындайтын нақты нұсқаулар тізбегі. Есепті шешкенде алдымен қандай дерек берілгенін, одан не табу керегін және жауаптың пішімін анықтаймыз. Бұл қадамдар кіріс → өңдеу → шығыс тізбегін құрайды.

                    Айнымалы мәнді атаумен сақтайды. Өрнек сол мәндерден жаңа мән есептейді: мысалы, boxes * itemsPerBox көбейту амалын орындайды. Меншіктеу операторы (=) оң жақтағы нәтижені сол жақтағы айнымалыға жазады; ол математикалық теңдікті тексеру емес.

                    Төмендегі мысалда 3 қораптың әрқайсысында 2 зат бар деп аламыз. Кіріс мәндері кодта берілген, өңдеу — көбейту, ал шығыс — экрандағы 6 саны. std::cout нәтижені көрсетеді, '\n' жаңа жолға көшіреді.

                    Алдымен кодты орындамай, әр айнымалының мәнін қағазға жазыңыз. boxes мәнін 4 деп елестетсеңіз, жауап қалай өзгереді? Кейін осы тақырыптың есебін ашып, оның кірісі мен шығысын өз сөзіңізбен сипаттаңыз. Мысалды дайын шешім ретінде көшірмей, есептің талабын бөлек талдаңыз.
                    """,
                CodeLanguage = "cpp", CodeExample = """
                    #include <iostream>
                    int main() {
                        int boxes = 3;
                        int itemsPerBox = 2;
                        std::cout << boxes * itemsPerBox << '\n';
                    }
                    """
            },
            new Lesson
            {
                Title = "C++ негіздері", Slug = "cpp-basics", TopicId = topics["Basics"], Order = 2,
                Summary = "C++ бағдарламасының құрылымын, типтерді, меншіктеуді және экранға шығаруды үйреніңіз.",
                Content = """
                    C++ тілінде айнымалының типі оның қандай мән сақтайтынын анықтайды. int бүтін санға, double бөлшек санға, bool ақиқат не жалған мәнге арналған. Айнымалыны пайдаланудан бұрын бастапқы мән беріңіз.

                    #include <iostream> енгізу мен шығару құралдарын қосады. Бағдарлама int main() функциясынан басталады. Фигуралы жақшалар функцияның денесін шектейді; қарапайым нұсқаулар нүктелі үтірмен аяқталады. std::cout << өрнегі мәнді экранға жібереді. Мәтін қос тырнақшамен жазылады.

                    Мысалда level бастапқыда 1. level += 2 нұсқауы level = level + 2 дегенмен бірдей, сондықтан соңғы жол Level: 3 мәтінін шығарады. Жол соңындағы '\n' келесі шығысты жаңа жолдан бастайды.

                    Синтаксистік қате компиляция кезінде табылады, ал логикалық қате дұрыс жиналған бағдарламадан қате жауап шығара алады. Бірінші жолдан бастап мәндерді бақылап, қатені осы екі топқа бөліңіз. level типін немесе бастапқы мәнін өзгерткенде не болатынын түсіндіріп, байланысты есептің шартын оқуға көшіңіз.
                    """,
                CodeLanguage = "cpp", CodeExample = """
                    #include <iostream>
                    int main() {
                        int level = 1;
                        level += 2;
                        std::cout << "Level: " << level << '\n';
                    }
                    """
            },
            new Lesson
            {
                Title = "Python негіздері", Slug = "python-basics", TopicId = topics["Basics"], Order = 3,
                Summary = PythonLessonContent.Summary, Content = PythonLessonContent.Content,
                CodeLanguage = "python", CodeExample = PythonLessonContent.CodeExample
            },
            new Lesson
            {
                Title = "Массивтер негіздері", Slug = "arrays-basics", TopicId = topics["Arrays"], Order = 1,
                Summary = "Бірнеше мәнді бірге сақтап, нөлден басталатын индекспен оқу мен өзгертуді үйреніңіз.",
                Content = """
                    Массив бір типті бірнеше мәнді ретімен сақтайды. C++ тіліндегі std::array<int, 3> үш бүтін саннан тұратын өлшемі тұрақты контейнерді білдіреді. Онымен жұмыс істеу үшін <array> тақырыбын қосамыз.

                    Индекс нөлден басталады: үш элементтің индекстері 0, 1 және 2. values[1] — екінші элемент. values[2] = 10 соңғы элементті өзгертеді. Индекс элементтің өзі емес, оның орны екенін есте сақтаңыз.

                    Мысалдың бірінші шығысы 7, себебі бастапқы екінші элементтің мәні 7. Соңғы элемент жаңартылғаннан кейін екінші жолда 10 шығады. Қалған элементтер өзгермейді.

                    Контейнер шегінен тыс values[3] өрнегін қолдануға болмайды: C++ тілінде [] шекті автоматты тексермейді. Индекс әрқашан 0 <= i < өлшем шартын орындауы тиіс. Өлшем бір болғанда жалғыз жарамды индексті атаңыз. Байланысты есепке өтпес бұрын массивті біртіндеп қарау жоспарын қағазда құрыңыз.
                    """,
                CodeLanguage = "cpp", CodeExample = """
                    #include <array>
                    #include <iostream>
                    int main() {
                        std::array<int, 3> values = {4, 7, 9};
                        std::cout << values[1] << '\n';
                        values[2] = 10;
                        std::cout << values[2] << '\n';
                    }
                    """
            },
            new Lesson
            {
                Title = "Алгоритм негіздері", Slug = "algorithm-basics", TopicId = topics["Algorithms"], Order = 1,
                Summary = "Сызықтық іздеу арқылы алгоритмнің қадамдарын, тоқтау шартын және уақыт күрделілігін талдаңыз.",
                Content = """
                    Алгоритм — есепті шешетін ақырлы, нақты қадамдар тізбегі. Оның дұрыстығын түсіндіру үшін бастапқы дерек, әр қадамдағы өзгеріс және аяқталғандағы нәтиже қажет. Алдымен шағын мысалды қолмен тексеру пайдалы.

                    Сызықтық іздеу элементтерді басынан бастап салыстырады. Тең элемент табылса, оның индексін сақтап, break арқылы циклден шығады. Табылмаса, алдын ала берілген -1 белгісі қалады. Бұл белгі жарамды индекстерден бөлек таңдалған.

                    Мысалда 5 саны {8, 5, 2} тізімінің екінші орнында, сондықтан нәтиже 1. Бірінші салыстыру өтпейді, екіншісі орындалады да, цикл тоқтайды. Ізделетін мән 4 болса, үш салыстырудан кейін -1 шығар еді.

                    Ең нашар жағдайда n элемент түгел қаралады, сондықтан уақыт күрделілігі O(n), қосымша жады O(1). Бұл белгілер нақты секундты емес, кіріс өскендегі жұмыс көлемін сипаттайды. Бір элемент, табылмайтын мән және қайталанатын мән жағдайларын ойша тексеріңіз. Байланысты есептерге көшкенде дайын кодты көшіруден бұрын әр есепке жеке алгоритм құрыңыз.
                    """,
                CodeLanguage = "cpp", CodeExample = """
                    #include <array>
                    #include <iostream>
                    int main() {
                        std::array<int, 3> values = {8, 5, 2};
                        int target = 5;
                        int found = -1;
                        for (int i = 0; i < 3; ++i) {
                            if (values[i] == target) {
                                found = i;
                                break;
                            }
                        }
                        std::cout << found << '\n';
                    }
                    """
            }
        };

        var previousPython = await db.Lessons.SingleOrDefaultAsync(lesson => lesson.Slug == "python-basics");
        if (previousPython != null && previousPython.CodeLanguage == "python"
            && previousPython.Summary == PythonLessonContent.LegacySummary
            && previousPython.Content.Replace("\r\n", "\n") == PythonLessonContent.LegacyContent.Replace("\r\n", "\n")
            && previousPython.CodeExample?.Replace("\r\n", "\n") == PythonLessonContent.LegacyCodeExample.Replace("\r\n", "\n"))
        {
            previousPython.Summary = PythonLessonContent.Summary;
            previousPython.Content = PythonLessonContent.Content;
            previousPython.CodeExample = PythonLessonContent.CodeExample;
        }

        var existing = await db.Lessons.AsNoTracking().Select(lesson => new { lesson.Slug, lesson.TopicId, lesson.Order }).ToListAsync();
        var slugs = existing.Select(lesson => lesson.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var positions = existing.Select(lesson => (lesson.TopicId, lesson.Order)).ToHashSet();
        foreach (var lesson in lessons)
        {
            if (!slugs.Add(lesson.Slug)) continue;
            while (!positions.Add((lesson.TopicId, lesson.Order))) lesson.Order++;
            lesson.IsPublished = true;
            lesson.CreatedAt = DateTime.UtcNow;
            db.Lessons.Add(lesson);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
