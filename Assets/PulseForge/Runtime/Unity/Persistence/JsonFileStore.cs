using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Persistence
{
    public sealed class JsonFileStore
    {
        private readonly string rootDirectory;

        public JsonFileStore(string rootDirectory)
        {
            this.rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        }

        public string RootDirectory => rootDirectory;

        public T Load<T>(string fileName, Func<T> createDefaults, Func<T, T> normalize)
            where T : class
        {
            if (createDefaults == null)
            {
                throw new ArgumentNullException(nameof(createDefaults));
            }

            string mainPath = GetPath(fileName);
            string backupPath = mainPath + ".bak";
            if (TryRead(mainPath, createDefaults, normalize, out T mainData))
            {
                return mainData;
            }

            if (TryRead(backupPath, createDefaults, normalize, out T backupData))
            {
                Debug.LogWarning("PulseForge save recovery loaded backup: " + backupPath);
                return backupData;
            }

            T defaults = normalize == null
                ? createDefaults()
                : normalize(createDefaults());
            Debug.LogWarning("PulseForge save defaults were created for: " + mainPath);
            Save(fileName, defaults, normalize);
            return defaults;
        }

        public bool Save<T>(string fileName, T data, Func<T, T> normalize = null)
            where T : class
        {
            string mainPath = GetPath(fileName);
            string temporaryPath = mainPath + ".tmp";
            string backupPath = mainPath + ".bak";
            try
            {
                Directory.CreateDirectory(rootDirectory);
                T normalized = normalize == null ? data : normalize(data);
                if (normalized == null)
                {
                    throw new InvalidDataException("Normalized save data cannot be null.");
                }

                string json = JsonUtility.ToJson(normalized, true);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(mainPath))
                {
                    ReplaceExistingFile(temporaryPath, mainPath, backupPath);
                }
                else
                {
                    File.Move(temporaryPath, mainPath);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("PulseForge could not save " + mainPath + ": " + exception.Message);
                TryDeleteTemporary(temporaryPath);
                return false;
            }
        }

        private bool TryRead<T>(
            string path,
            Func<T> createDefaults,
            Func<T, T> normalize,
            out T data)
            where T : class
        {
            data = null;
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidDataException("JSON file is empty.");
                }

                T parsed = createDefaults();
                JsonUtility.FromJsonOverwrite(json, parsed);
                if (parsed == null)
                {
                    throw new InvalidDataException("JSON did not produce save data.");
                }

                data = normalize == null ? parsed : normalize(parsed);
                return data != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("PulseForge could not read " + path + ": " + exception.Message);
                return false;
            }
        }

        private static void ReplaceExistingFile(
            string temporaryPath,
            string mainPath,
            string backupPath)
        {
            try
            {
                File.Replace(temporaryPath, mainPath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(mainPath, backupPath, true);
                File.Delete(mainPath);
                File.Move(temporaryPath, mainPath);
            }
        }

        private string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Save file name is required.", nameof(fileName));
            }

            return Path.Combine(rootDirectory, fileName);
        }

        private static void TryDeleteTemporary(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // The original save error is already logged. A stale tmp is safe to overwrite later.
            }
        }
    }
}
