#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace DTech.LinkGuard.Editor.Zenject
{
    /// <summary>
    /// Discovers reachable Zenject installers and their bound types, then merges the
    /// corresponding entries into an existing link.xml. Aborts without writing if the
    /// existing file is malformed. Intended as a build-time entry point.
    /// </summary>
    public static class ZenjectLinkXmlPatcher
    {
        /// <summary>Default link.xml path (<c>Assets/link.xml</c>) used when none is supplied.</summary>
        public const string DefaultPath = "Assets/link.xml";

        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";

        /// <summary>
        /// Merges Zenject-reachable type entries into the link.xml at the given path.
        /// </summary>
        /// <param name="linkXmlPath">
        /// Target link.xml path; falls back to <see cref="DefaultPath"/> when null or empty.
        /// </param>
        /// <returns>A report of added and already-covered types and the installer graph size.</returns>
        public static ZenjectPatchReport Patch(string linkXmlPath = DefaultPath)
        {
            string normalizedPath = string.IsNullOrEmpty(linkXmlPath) ? DefaultPath : linkXmlPath;
            ZenjectScanResult scan = ZenjectMergeProvider.Run();

            XDocument document = LoadOrCreateDocument(normalizedPath, out string loadFailure);

            if (document == null)
            {
                string reason = $"Existing link.xml at {normalizedPath} could not be used: {loadFailure}. Nothing written.";
                Debug.LogError($"[LinkXmlGenerator] [zenject] {reason}");

                List<string> warnings = new List<string>(scan.Warnings) { reason };
                return new ZenjectPatchReport(
                    normalizedPath,
                    0,
                    0,
                    scan.ReachableInstallerCount,
                    scan.IgnoredInstallerCount,
                    warnings);
            }

            XElement linker = document.Root;

            int added = 0;
            int alreadyCovered = 0;

            foreach (TypeIdentifier id in scan.LinkEntries)
            {
                if (id == null
                    || id.IsGenericParameter
                    || string.IsNullOrEmpty(id.AssemblyName)
                    || string.IsNullOrEmpty(id.TypeFullname))
                {
                    continue;
                }

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

            string xml = LinkXmlBuilder.Serialize(document);
            File.WriteAllText(normalizedPath, xml);

            if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(normalizedPath, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log(
                $"[LinkXmlGenerator] [zenject] patcher wrote {normalizedPath}: "
                + $"+{added} added, {alreadyCovered} already covered.");

            return new ZenjectPatchReport(
                normalizedPath,
                added,
                alreadyCovered,
                scan.ReachableInstallerCount,
                scan.IgnoredInstallerCount,
                scan.Warnings);
        }

        private static XDocument LoadOrCreateDocument(string path, out string failureReason)
        {
            failureReason = string.Empty;

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
                    failureReason = "root element is not <linker>";
                    return null;
                }

                return document;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                return null;
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
