using System;
using System.Collections.Generic;

namespace Virgil.Domain
{
    public static class PunchlineEngine
    {
        private static readonly Random Rng = new();
        private static readonly List<string> HumorLines = new()
        {
            "Je sens ton CPU tousser un peu… un café virtuel ?",
            "La poussière digitale, mon pire ennemi.",
            "Le GPU transpire, ça chauffe ici !",
            "Tout roule. Je pourrais presque faire une sieste.",
            "J’ai effacé 2 Go de bêtises, je me sens léger !"
        };

        private static readonly List<string> ProLines = new()
        {
            "Analyse système en cours.",
            "Surveillance des performances OK.",
            "Aucune anomalie détectée.",
            "Températures dans les seuils normaux.",
            "Cycle de maintenance terminé."
        };

        public static string GetRandom(string tone = "humor")
        {
            var list = tone == "pro" ? ProLines : HumorLines;
            return list[Rng.Next(list.Count)];
        }
    }
}
