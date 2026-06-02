using System.IO;
using System.Text.Json;
using Windows.Storage;
using System.Text.Json.Serialization; 
    
namespace TabWriter
{
    public class AppSettings
    {
        //set the default font Size to 16.0
        public double FontSize { get; set; } = 16.0;

        //Add more properties here later as needed
    }

    //I dont understand this right now. But it should fix some errors
    [JsonSerializable(typeof(AppSettings))]
    internal partial class AppSettingsContext : JsonSerializerContext
    {
    
    }

    public static class SettingsManager
    {
        static string SettingsFile = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Settings.json");
        private static readonly JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            TypeInfoResolver = AppSettingsContext.Default
        };

        public static AppSettings Load()
        {
            //Laod file
            if (File.Exists(SettingsFile))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }
        //Save the settings after any change
        public static void Save(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsFile, json);
        }
    }
}
