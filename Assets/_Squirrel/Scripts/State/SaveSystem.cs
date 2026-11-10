using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Squirrel.World
{
    [Serializable]
    public class EdgeSnapshot
    {
        public int phase;
        public int stage;
        public float timeLeft;
        public float baseRadius;
        public float pushedBack;
    }

    /// <summary>Снимок прогресса. Версия формата — с первого дня, чтобы старые сохранения не ломали игру.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string scene;
        public string nest;
        public List<string> flags = new List<string>();
        public float dream;
        public float compassion;
        public int sleepDepth;
        public int deaths;
        public int bonusSeeds;
        public int sap;
        public bool hasEdge;
        public EdgeSnapshot edge = new EdgeSnapshot();
        public string savedAt;
    }

    /// <summary>JSON-файлы в Application.persistentDataPath, по одному на слот.</summary>
    public static class SaveSystem
    {
        /// <summary>Тесты подменяют папку, чтобы не трогать настоящие сохранения.</summary>
        public static string DirectoryOverride;

        static string Folder => string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;

        public static string PathFor(string slot) => Path.Combine(Folder, $"squirrel_{slot}.json");

        public static bool Exists(string slot) => File.Exists(PathFor(slot));

        public static void Write(string slot, SaveData data)
        {
            Directory.CreateDirectory(Folder);
            data.version = SaveData.CurrentVersion;
            data.savedAt = DateTime.Now.ToString("s");
            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
        }

        public static bool TryRead(string slot, out SaveData data)
        {
            data = null;
            string path = PathFor(slot);
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Сохранение {path} не читается: {e.Message}");
                return false;
            }
            if (data == null || data.version != SaveData.CurrentVersion)
            {
                Debug.LogWarning($"Сохранение {path} старого формата (версия {data?.version}), начинаю заново.");
                data = null;
                return false;
            }
            return true;
        }

        public static void Delete(string slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
