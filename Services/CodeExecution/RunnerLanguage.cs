namespace IntelligentProgrammingPlatform.Services.CodeExecution;

public sealed class RunnerLanguage
{
    public const string CppStarter = "#include <iostream>\nusing namespace std;\n\nint main()\n{\n    // Write your solution here\n\n    return 0;\n}\n";
    public const string PythonStarter = "# Write your solution here\n";
    public const string PythonImage = "python@sha256:5024f48ba9441d4b13a95d3945abc6365538e3a31109833367a1923523c6efed";

    public static RunnerLanguage Cpp { get; } = new("cpp", "C++ 20", "GCC 14.3.0", ".cpp", "main.cpp",
        CodeRunnerOptions.Image, CppStarter, true,
        new[] { "/usr/local/bin/g++", "-std=c++20", "-O2", "-pipe", "-fdiagnostics-color=never", "/source/main.cpp", "-o", "/build/program" },
        new[] { "/app/program" });
    public static RunnerLanguage Python { get; } = new("python", "Python 3", "3.13.16", ".py", "main.py",
        PythonImage, PythonStarter, false,
        new[] { "/usr/local/bin/python3", "-I", "-S", "-B", "-X", "pycache_prefix=/tmp/pycache", "-m", "py_compile", "/app/main.py" },
        new[] { "/usr/local/bin/python3", "-I", "-S", "-B", "-u", "/app/main.py" });
    public static IReadOnlyList<string> Keys { get; } = Array.AsReadOnly(new[] { "cpp", "python" });

    public string Key { get; }
    public string Name { get; }
    public string Version { get; }
    public string FileExtension { get; }
    public string SourceFile { get; }
    public string Image { get; }
    public string StarterCode { get; }
    public bool ProducesExecutable { get; }
    public IReadOnlyList<string> CompileCommand { get; }
    public IReadOnlyList<string> RunCommand { get; }

    // Тек сервер анықтаған өзгермейтін тіл параметрлерін сақтайды.
    private RunnerLanguage(string key, string name, string version, string fileExtension, string sourceFile,
        string image, string starterCode, bool producesExecutable, string[] compileCommand, string[] runCommand)
    {
        Key = key;
        Name = name;
        Version = version;
        FileExtension = fileExtension;
        SourceFile = sourceFile;
        Image = image;
        StarterCode = starterCode;
        ProducesExecutable = producesExecutable;
        CompileCommand = Array.AsReadOnly(compileCommand);
        RunCommand = Array.AsReadOnly(runCommand);
    }

    // Дерекқор тіл кілтін тек бекітілген екі орындаушы анықтамасына сәйкестендіреді.
    public static RunnerLanguage? Find(string? key) => key switch
    {
        "cpp" => Cpp,
        "python" => Python,
        _ => null
    };
}
