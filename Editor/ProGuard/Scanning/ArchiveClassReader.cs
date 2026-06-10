using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace DTech.LinkGuard.Editor.ProGuard
{
    internal static class ArchiveClassReader
    {
        public static List<string> ReadClassEntryPaths(string archivePath)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
            {
                return result;
            }

            bool isAar = Path.GetExtension(archivePath).Equals(".aar", StringComparison.OrdinalIgnoreCase);

            try
            {
                using FileStream stream = File.OpenRead(archivePath);
                result = ReadClassEntryPaths(stream, isAar);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LinkGuard] [proguard] Failed to read archive '{archivePath}': {ex.Message}");
            }

            return result;
        }

        public static List<string> ReadClassEntryPaths(Stream archiveStream, bool isAar)
        {
            List<string> result = new List<string>();

            using ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);

            if (!isAar)
            {
                CollectClassEntries(archive, result);
                return result;
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (IsNestedJar(entry.FullName))
                {
                    ReadNestedJar(entry, result);
                }
                else if (entry.FullName.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry.FullName);
                }
            }

            return result;
        }

        private static void CollectClassEntries(ZipArchive archive, List<string> result)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(entry.FullName);
                }
            }
        }

        private static bool IsNestedJar(string name)
        {
            string normalized = name.Replace('\\', '/');

            if (!normalized.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return normalized.Equals("classes.jar", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("libs/", StringComparison.OrdinalIgnoreCase);
        }

        private static void ReadNestedJar(ZipArchiveEntry entry, List<string> result)
        {
            using MemoryStream memory = new MemoryStream();

            using (Stream entryStream = entry.Open())
            {
                entryStream.CopyTo(memory);
            }

            memory.Position = 0;

            using ZipArchive nested = new ZipArchive(memory, ZipArchiveMode.Read);
            CollectClassEntries(nested, result);
        }
    }
}
