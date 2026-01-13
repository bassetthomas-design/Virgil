namespace Virgil.App.Services
{
    public interface ISecretStore
    {
        void SaveOpenAiApiKey(string key);
        string? LoadOpenAiApiKey();
        void ClearOpenAiApiKey();
    }
}
