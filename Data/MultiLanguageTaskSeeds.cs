using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;

namespace IntelligentProgrammingPlatform.Data;

public static class MultiLanguageTaskSeeds
{
    // Екі тілге ортақ есептерді тұрақты реттелген ашық және жасырын тесттермен құрады.
    public static IReadOnlyList<ProgrammingTask> Create(IReadOnlyDictionary<string, int> topics) => new[]
    {
        new ProgrammingTask
        {
            Title = "Count Even Numbers", Slug = "count-even-numbers", TopicId = topics["Arrays"],
            Difficulty = Difficulty.Easy, TimeLimitMs = 2000, MemoryLimitMb = 128, IsPublished = true,
            Description = "Read N (1 <= N <= 10000) on the first line, then N integers between -1000000000 and 1000000000 on the second line. Print how many of these integers are even. Zero and negative even integers count as even. Output one integer. The input and output format is identical for C++ and Python.",
            TestCases = new List<TestCase>
            {
                new() { Order = 1, Input = "5\n1 2 3 4 6", ExpectedOutput = "3" },
                new() { Order = 2, Input = "3\n0 -2 5", ExpectedOutput = "2" },
                new() { Order = 3, Input = "4\n2 4 6 8", ExpectedOutput = "4", IsHidden = true },
                new() { Order = 4, Input = "4\n1 3 5 7", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 5, Input = "1\n-7", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 6, Input = "6\n-8 -3 -2 0 9 12", ExpectedOutput = "4", IsHidden = true },
                new() { Order = 7, Input = "1\n0", ExpectedOutput = "1", IsHidden = true }
            }
        },
        new ProgrammingTask
        {
            Title = "Palindrome Check", Slug = "palindrome-check", TopicId = topics["Basics"],
            Difficulty = Difficulty.Medium, TimeLimitMs = 2000, MemoryLimitMb = 128, IsPublished = true,
            Description = "Read one word containing only lowercase English letters a-z, with length from 1 to 10000. The word contains no spaces. Determine whether it reads the same from left to right and right to left. Print YES for a palindrome or NO otherwise, using exactly these uppercase words. A single character is a palindrome.",
            TestCases = new List<TestCase>
            {
                new() { Order = 1, Input = "level", ExpectedOutput = "YES" },
                new() { Order = 2, Input = "code", ExpectedOutput = "NO" },
                new() { Order = 3, Input = "x", ExpectedOutput = "YES", IsHidden = true },
                new() { Order = 4, Input = "abba", ExpectedOutput = "YES", IsHidden = true },
                new() { Order = 5, Input = "abcba", ExpectedOutput = "YES", IsHidden = true },
                new() { Order = 6, Input = "abca", ExpectedOutput = "NO", IsHidden = true },
                new() { Order = 7, Input = "ab", ExpectedOutput = "NO", IsHidden = true }
            }
        },
        new ProgrammingTask
        {
            Title = "Binary Search", Slug = "binary-search", TopicId = topics["Algorithms"],
            Difficulty = Difficulty.Medium, TimeLimitMs = 2000, MemoryLimitMb = 128, IsPublished = true,
            Description = "Read N (1 <= N <= 10000), then N distinct integers in strictly increasing order on the second line, and a target integer on the third line. All values are between -1000000000 and 1000000000. Find the target in the sorted array. Print its ZERO-BASED index (the first element has index 0), or -1 if it is absent. Use the sorted order to design a binary search; do not change the input order.",
            TestCases = new List<TestCase>
            {
                new() { Order = 1, Input = "5\n1 3 5 7 9\n5", ExpectedOutput = "2" },
                new() { Order = 2, Input = "4\n2 4 6 8\n5", ExpectedOutput = "-1" },
                new() { Order = 3, Input = "4\n-9 -4 0 12\n-9", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 4, Input = "4\n-9 -4 0 12\n12", ExpectedOutput = "3", IsHidden = true },
                new() { Order = 5, Input = "1\n7\n7", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 6, Input = "1\n7\n-3", ExpectedOutput = "-1", IsHidden = true },
                new() { Order = 7, Input = "5\n-20 -11 -6 -2 4\n-6", ExpectedOutput = "2", IsHidden = true },
                new() { Order = 8, Input = "3\n1 5 9\n20", ExpectedOutput = "-1", IsHidden = true }
            }
        },
        new ProgrammingTask
        {
            Title = "Массив элементтерінің қосындысы", Slug = "array-sum", TopicId = topics["Arrays"],
            Difficulty = Difficulty.Easy, TimeLimitMs = 2000, MemoryLimitMb = 128, IsPublished = true,
            Description = """
                Берілген N бүтін саннан тұратын массивтің барлық элементтерінің қосындысын табыңыз. Элементтер теріс, нөл немесе оң болуы мүмкін.

                Кіріс пішімі
                Бірінші жолда N саны беріледі.
                Екінші жолда бос орынмен бөлінген N бүтін сан беріледі.

                Шығыс пішімі
                Массив элементтерінің қосындысын бір бүтін сан ретінде шығарыңыз.

                Шектеулер
                1 <= N <= 100000.
                Әр элементтің мәні -1000000 мен 1000000 аралығында, шекаралары қоса алынады.
                Қосындының абсолют мәні 100000000000 санынан аспайды. C++ тілінде қосындыны сақтау үшін 64 биттік бүтін сан түрі қажет; Python бүтін сандары бұл аралықты қолдайды.

                Мысал
                Кіріс:
                5
                1 2 3 4 5
                Шығыс:
                15

                Түсіндірме: бес элементтің қосындысы 15-ке тең.
                """,
            TestCases = new List<TestCase>
            {
                new() { Order = 1, Input = "5\n1 2 3 4 5", ExpectedOutput = "15" },
                new() { Order = 2, Input = "5\n-2 0 7 -5 4", ExpectedOutput = "4" },
                new() { Order = 3, Input = "1\n-17", ExpectedOutput = "-17", IsHidden = true },
                new() { Order = 4, Input = "6\n0 0 0 0 0 0", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 5, Input = "4\n-8 -3 -11 -2", ExpectedOutput = "-24", IsHidden = true },
                new() { Order = 6, Input = "6\n-1000000 999999 1 -8 8 42", ExpectedOutput = "42", IsHidden = true },
                new() { Order = 7, Input = "100000\n" + string.Join(' ', Enumerable.Repeat("1000000", 100000)), ExpectedOutput = "100000000000", IsHidden = true },
                new() { Order = 8, Input = "100000\n" + string.Join(' ', Enumerable.Repeat("-1000000", 100000)), ExpectedOutput = "-100000000000", IsHidden = true }
            }
        },
        new ProgrammingTask
        {
            Title = "Жолдағы дауысты әріптер саны", Slug = "count-vowels", TopicId = topics["Basics"],
            Difficulty = Difficulty.Easy, TimeLimitMs = 2000, MemoryLimitMb = 128, IsPublished = true,
            Description = """
                Берілген жолдағы ағылшын тілінің дауысты әріптерінің жалпы санын табыңыз.
                Дауысты әріптер: a, e, i, o, u. Олардың бас әріп түрлері A, E, I, O, U да есептеледі. Қалған әріптер мен бос орындар есептелмейді.

                Кіріс пішімі
                Бір жол мәтін беріледі. Жол ағылшынның a-z, A-Z әріптері мен кәдімгі бос орындардан тұрады. Жолдың басында, ортасында және соңында бос орындар болуы мүмкін; жол тек бос орындардан да тұра алады. Мәтінді бір сөзбен шектемей, жолды толық оқыңыз.

                Шығыс пішімі
                Дауысты әріптердің жалпы санын бір бүтін сан ретінде шығарыңыз. Дауысты әріп болмаса, 0 шығарыңыз.

                Шектеулер
                Бос орындарды қоса есептегендегі жол ұзындығы 1 мен 100000 аралығында. Жолды аяқтайтын таңба бұл ұзындыққа кірмейді.
                Кіріс тек жоғарыда көрсетілген ағылшын әріптері мен бос орындардан тұрады; қазақша әріптер берілмейді.

                Мысал
                Кіріс:
                Hello World
                Шығыс:
                3

                Түсіндірме: мәтінде e әрпі бір рет, o әрпі екі рет кездеседі.
                """,
            TestCases = new List<TestCase>
            {
                new() { Order = 1, Input = "Hello World", ExpectedOutput = "3" },
                new() { Order = 2, Input = "AEIOU aeiou", ExpectedOutput = "10" },
                new() { Order = 3, Input = "rhythms", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 4, Input = "aeiou", ExpectedOutput = "5", IsHidden = true },
                new() { Order = 5, Input = "AEIOU", ExpectedOutput = "5", IsHidden = true },
                new() { Order = 6, Input = "ApPlE OrAnGe", ExpectedOutput = "5", IsHidden = true },
                new() { Order = 7, Input = "u", ExpectedOutput = "1", IsHidden = true },
                new() { Order = 8, Input = "Z", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 9, Input = "   a E   i O u   ", ExpectedOutput = "5", IsHidden = true },
                new() { Order = 10, Input = "     ", ExpectedOutput = "0", IsHidden = true },
                new() { Order = 11, Input = new string('A', 100000), ExpectedOutput = "100000", IsHidden = true }
            }
        }
    };
}
