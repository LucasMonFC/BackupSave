using MSCLoader;

namespace BackupSave
{
    public class LocalizationManager
    {
        private const string SummerBrazilianLocalizationModId = "MSC_Localization_Core_BR";
        private const string WinterBrazilianLocalizationModId = "MWC_Localization_Core_BR";

        public LocalizationManager(string gameFolder = "")
        {
        }

        public static string Text(string english, string portuguese)
        {
            return IsBrazilianLocalizationInstalled() ? portuguese : english;
        }

        public string GetString(string section, string key, string defaultValue = "")
        {
            return string.IsNullOrEmpty(defaultValue) ? section + "." + key : defaultValue;
        }

        public string[] GetAutoRestoreModeValues()
        {
            return new string[]
            {
                Text("Disabled", "Desligada"),
                Text("Restore All", "Restaurar Tudo"),
                Text("Restore keeping gravestones", "Restaurar mantendo as lápides")
            };
        }

        public string GetImportedPrefix()
        {
            return Text("IMPORTED - ", "IMPORTADO - ");
        }

        public static bool IsBrazilianLocalizationInstalled()
        {
            try
            {
                return ModLoader.IsModPresent(GetBrazilianLocalizationModId());
            }
            catch
            {
                return false;
            }
        }

        public static string GetBrazilianLocalizationModId()
        {
            return ModLoader.CurrentGame == Game.MySummerCar
                ? SummerBrazilianLocalizationModId
                : WinterBrazilianLocalizationModId;
        }
    }
}
