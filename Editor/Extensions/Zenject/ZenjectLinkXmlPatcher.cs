#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor.Zenject
{
    public static class ZenjectLinkXmlPatcher
    {
        public const string DefaultPath = "Assets/link.xml";
        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";

        public static ZenjectPatchReport Patch(string linkXmlPath = DefaultPath)
        {
            string normalizedPath = string.IsNullOrEmpty(linkXmlPath) ? DefaultPath : linkXmlPath;
            ZenjectScanResult scan = ZenjectMergeProvider.Run();

            XDocument document = LoadOrCreateDocument(normalizedPath);
            XElement linker = document.Root;

            int added = 0;
            int alreadyCovered = 0;
            int reachableInstallers = 0;

            HashSet<string> installerKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (TypeIdentifier id in scan.LinkEntries)
            {
                if (id == null
                    || id.IsGenericParameter
                    || string.IsNullOrEmpty(id.AssemblyName)
                    || string.IsNullOrEmpty(id.TypeFullname))
                {
                    continue;
                }

                installerKeys.Add($"{id.AssemblyName}::{id.TypeFullname}");

                XElement assembly = FindOrCreateAssembly(linker, id.AssemblyName);
                if (PreservesAll(assembly))
                {
                    alreadyCovered++;
                    continue;
                }

                XElement existingType = FindType(assembly, id.TypeFullname);
                if (existingType == null)
                {
                    assembly.Add(new XElement(
                        TypeElement,
                        new XAttribute(FullnameAttribute, id.TypeFullname),
                        new XAttribute(PreserveAttribute, "all")));
                    added++;
                    continue;
                }

                if (PreservesAll(existingType))
                {
                    alreadyCovered++;
                    continue;
                }

                XAttribute existingPreserve = existingType.Attribute(PreserveAttribute);
                if (existingPreserve == null)
                {
                    existingType.Add(new XAttribute(PreserveAttribute, "all"));
                }
                else
                {
                    existingPreserve.Value = "all";
                }
                added++;
            }

            reachableInstallers = installerKeys.Count;

            string xml = LinkXmlBuilder.Serialize(document);
            File.WriteAllText(normalizedPath, xml);

            if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log(
                $"[LinkXmlGenerator] [zenject] patcher wrote {normalizedPath}: +{added} added, {alreadyCovered} already covered.");

            return new ZenjectPatchReport(normalizedPath, added, alreadyCovered, reachableInstallers, scan.Warnings);
        }

        private static XDocument LoadOrCreateDocument(string path)
        {
            if (!File.Exists(path))
            {
                return new XDocument(new XElement(LinkerElement));
            }

            try
            {
                XDocument document = XDocument.Load(path);
                if (document.Root == null
                    || !string.Equals(document.Root.Name.LocalName, LinkerElement, StringComparison.Ordinal))
                {
                    return new XDocument(new XElement(LinkerElement));
                }

                return document;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[LinkXmlGenerator] [zenject] failed to load existing link.xml at {path}: {ex.Message}. Creating fresh document.");
                return new XDocument(new XElement(LinkerElement));
            }
        }

        private static XElement FindOrCreateAssembly(XElement linker, string assemblyName)
        {
            foreach (XElement child in linker.Elements(AssemblyElement))
            {
                if (string.Equals(child.Attribute(FullnameAttribute)?.Value, assemblyName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            XElement created = new XElement(AssemblyElement, new XAttribute(FullnameAttribute, assemblyName));
            linker.Add(created);
            return created;
        }

        private static XElement FindType(XElement assembly, string typeFullname)
        {
            foreach (XElement child in assembly.Elements(TypeElement))
            {
                if (string.Equals(child.Attribute(FullnameAttribute)?.Value, typeFullname, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool PreservesAll(XElement element)
        {
            string value = element.Attribute(PreserveAttribute)?.Value;
            return string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
