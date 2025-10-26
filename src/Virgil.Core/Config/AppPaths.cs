using System;

namespace Virgil.Core.Config
{
    public static class AppPaths
    {
        public static string ProgramDataRoot =>
            Environment.ExpandEnvironmentVariables(@"%ProgramData%\Virgil");

        public static string UserDataRoot =>
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\Virgil";

        public static string ProgramDataConfig => ProgramDataRoot + @"\config.json";
        public static string UserConfig => UserDataRoot + @"\user.json";
        public static string LogsDir => ProgramDataRoot + @"\logs";
        public static string LogFile => LogsDir + @"\virgil-.log"; // Serilog rolling file uses {Date}
    }
}
