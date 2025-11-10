namespace Virgil.App.Chat;

public enum PhraseStyle { Pro, Taquin }

public sealed class PhraseEngine
{
    private readonly Random _rng = new();
    public PhraseStyle Style { get; set; } = PhraseStyle.Taquin;

    public string For(string intent, string phase, int? percent = null)
    {
        // squelette minimal: on remplira la bibliothèque complète plus tard
        return (intent, phase) switch
        {
            ("clean", "start") => Style == PhraseStyle.Taquin ? "Ok, en avant. J’ouvre les fenêtres." : "Nettoyage intelligent démarré.",
            ("clean", "progress") when percent.HasValue => Style == PhraseStyle.Taquin ? $"J’en suis à {percent}%…" : $"Progression {percent}%",
            ("clean", "finish") => Style == PhraseStyle.Taquin ? "Nettoyage terminé. Ton PC respire." : "Nettoyage terminé.",
            ("update", "start") => Style == PhraseStyle.Taquin ? "Updates en marche. Ne touche à rien." : "Mises à jour démarrées.",
            ("update", "progress") when percent.HasValue => $"Mises à jour {percent}%",
            ("update", "finish") => "Mises à jour terminées.",
            _ => "…"
        };
    }
}
