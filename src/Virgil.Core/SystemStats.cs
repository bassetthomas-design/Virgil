using System;

namespace Virgil.Core;

/// <summary>
/// Représente un instantané des principales statistiques système utilisées par Virgil.
/// Cette structure regroupe les pourcentages d'utilisation et les températures
/// des différents composants du PC. Elle est utilisée par les services de
/// monitoring et les moteurs de décision (par exemple, <see cref="Virgil.Domain.MoodEngine"/>).
/// </summary>
public readonly record struct SystemStats(
    double Cpu,    // Pourcentage d'utilisation du CPU
    double Gpu,    // Pourcentage d'utilisation du GPU
    double Ram,    // Pourcentage d'utilisation de la mémoire vive
    double Disk,   // Pourcentage d'utilisation du disque
    double CpuTemp, // Température du CPU en °C
    double GpuTemp, // Température du GPU en °C
    double DiskTemp // Température du disque en °C
);
