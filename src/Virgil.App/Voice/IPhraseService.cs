namespace Virgil.App.Voice;

public interface IPhraseService
{
    string Get(string key, PhraseContext ctx);
    string? TryGetRandom(string bucket, PhraseContext ctx);
}
