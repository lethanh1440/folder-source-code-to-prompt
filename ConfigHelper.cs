using System.IO;
using System.Text.Json;

namespace ProjectToPromptScanner
{
    public static class ConfigHelper
    {
        public const string SAVE_FOLDER = "saved";
        public const string VIRTUAL_FOLDER = "virtual";
        public static readonly string LAST_CONFIG_FILE = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FOLDER, "last_config.txt");

        public static void EnsureSaveFolder()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FOLDER);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            string virtualPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, VIRTUAL_FOLDER);
            if (!Directory.Exists(virtualPath)) Directory.CreateDirectory(virtualPath);
        }

        public static List<string> GetSavedConfigs()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FOLDER);
            if (!Directory.Exists(path)) return new List<string>();

            return new DirectoryInfo(path)
                .GetFiles("*.scanfolder")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .ToList();
        }

        public static string GetLastSessionConfigName()
        {
            if (File.Exists(LAST_CONFIG_FILE)) {
                return File.ReadAllText(LAST_CONFIG_FILE).Trim();
            }
            return null;
        }

        public static void SaveLastSessionName(string configName)
        {
            try { File.WriteAllText(LAST_CONFIG_FILE, configName); } catch { }
        }

        public static void SaveState(string inputName, ProjectState state)
        {
            string fileName = inputName + ".scanfolder";
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FOLDER, fileName);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static ProjectState LoadState(string selectedName)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SAVE_FOLDER, selectedName + ".scanfolder");
            if (!File.Exists(fullPath)) return null;
            try { File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow); } catch { }
            return JsonSerializer.Deserialize<ProjectState>(File.ReadAllText(fullPath));
        }
    }
}
