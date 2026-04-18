using Microsoft.ML.Tokenizers;

namespace ManagedCode.DotnetSkills.Runtime;

internal static class SkillTokenCounter
{
    private static readonly Lock SyncRoot = new();
    private static readonly Tokenizer Tokenizer = TiktokenTokenizer.CreateForEncoding(EncodingName);

    public const string EncodingName = "o200k_base";
    public const string ModelName = "gpt-5 / o200k_base";

    public static int CountTokens(FileInfo skillFile)
    {
        if (!skillFile.Exists)
        {
            return 0;
        }

        return CountTokens(File.ReadAllText(skillFile.FullName));
    }

    public static int CountTokens(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        lock (SyncRoot)
        {
            return Tokenizer.CountTokens(content);
        }
    }
}
