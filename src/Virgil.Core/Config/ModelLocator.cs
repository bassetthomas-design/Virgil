using System.IO;

namespace Virgil.Core.Config;

public sealed class ModelLocator
{
    public const string ExpectedFileName = "llama-3.1-8b-instruct-q4_k_m.gguf";

    public string ModelDirectory => Path.Combine(AppPaths.ProgramDataRoot, "AI", "Models");

    public string ModelPath => Path.Combine(ModelDirectory, ExpectedFileName);

    public string ModelHashPath => Path.Combine(ModelDirectory, $"{ExpectedFileName}.sha256");

    public bool IsInstalled => File.Exists(ModelPath);
}
