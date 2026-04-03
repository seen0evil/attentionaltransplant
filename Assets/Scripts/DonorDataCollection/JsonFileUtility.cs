using System.IO;
using System.Text;
using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    public static class JsonFileUtility
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        public static void WriteJson<T>(string path, T payload, bool prettyPrint = true)
        {
            EnsureDirectory(path);
            File.WriteAllText(path, JsonUtility.ToJson(payload, prettyPrint), Utf8NoBom);
        }

        public static void AppendJsonLine<T>(string path, T payload)
        {
            EnsureDirectory(path);
            File.AppendAllText(path, JsonUtility.ToJson(payload) + "\n", Utf8NoBom);
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
