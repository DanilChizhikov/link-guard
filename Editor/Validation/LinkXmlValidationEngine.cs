using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace DTech.LinkGuard.Editor
{
    internal static class LinkXmlValidationEngine
    {
        private const string LinkerElement = "linker";
        private const string AssemblyElement = "assembly";
        private const string TypeElement = "type";
        private const string FullnameAttribute = "fullname";
        private const string PreserveAttribute = "preserve";
        private const string IgnoreIfMissingAttribute = "ignoreIfMissing";

        public static LinkXmlValidationOutcome Validate(string xml, IBuildMembershipOracle oracle)
        {
            XDocument document;

            try
            {
                document = XDocument.Parse(xml ?? string.Empty, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                return LinkXmlValidationOutcome.Failed($"Failed to parse link.xml: {ex.Message}", xml);
            }

            if (document.Root == null || document.Root.Name.LocalName != LinkerElement)
            {
                return LinkXmlValidationOutcome.Failed("Root element is not <linker>.", xml);
            }

            List<string> removedAssemblies = new List<string>();
            List<LinkXmlValidationTypeGroup> removedTypes = new List<LinkXmlValidationTypeGroup>();
            List<string> keptIgnoreIfMissing = new List<string>();
            List<LinkXmlValidationSkippedEntry> keptUnknown = new List<LinkXmlValidationSkippedEntry>();

            List<XElement> assemblies = document.Root.Elements()
                .Where(e => string.Equals(e.Name.LocalName, AssemblyElement, StringComparison.Ordinal))
                .ToList();

            foreach (XElement assembly in assemblies)
            {
                var info = new AssemblyProcessInfo(assembly, oracle, removedAssemblies, removedTypes, keptIgnoreIfMissing, keptUnknown);
                ProcessAssembly(info);
            }

            bool changed = removedAssemblies.Count > 0 || removedTypes.Count > 0;
            string result = changed ? Serialize(document, xml) : xml;

            return LinkXmlValidationOutcome.Completed(
                result,
                changed,
                removedAssemblies,
                removedTypes,
                keptIgnoreIfMissing,
                keptUnknown);
        }

        private static void ProcessAssembly(AssemblyProcessInfo info)
        {
            string fullname = info.Assembly.Attribute(FullnameAttribute)?.Value;

            if (string.IsNullOrEmpty(fullname))
            {
                var entry = new LinkXmlValidationSkippedEntry(string.Empty, string.Empty, "Assembly element has no fullname attribute.");
                info.KeptUnknown.Add(entry);
                return;
            }

            BuildPresence presence = info.Oracle.ResolveAssembly(fullname);
            if (presence == BuildPresence.Missing)
            {
                if (IsTruthy(info.Assembly.Attribute(IgnoreIfMissingAttribute)?.Value))
                {
                    info.KeptIgnoreIfMissing.Add(fullname);
                    return;
                }

                RemoveWithLeadingWhitespace(info.Assembly);
                info.RemovedAssemblies.Add(fullname);
                return;
            }

            if (presence == BuildPresence.Unknown)
            {
                var entry = new LinkXmlValidationSkippedEntry(fullname, string.Empty, "Assembly presence in the build could not be determined.");
                info.KeptUnknown.Add(entry);
                return;
            }

            var validateInfo = new ValidateTypeInfo(info.Assembly, fullname, info.Oracle, info.RemovedAssemblies, info.RemovedTypes, info.KeptUnknown);
            ValidateTypes(validateInfo);
        }

        private static void ValidateTypes(ValidateTypeInfo info)
        {
            List<XElement> typeElements = info.Assembly.Elements()
                .Where(e => string.Equals(e.Name.LocalName, TypeElement, StringComparison.Ordinal))
                .ToList();

            List<string> removedTypeNames = new List<string>();

            foreach (XElement typeElement in typeElements)
            {
                var typeInfo = new TypeProcessInfo(typeElement, info.AssemblyName, info.Oracle, removedTypeNames, info.KeptUnknown);
                ProcessType(typeInfo);
            }

            if (removedTypeNames.Count == 0)
            {
                return;
            }
            
            bool hasPreserve = info.Assembly.Attribute(PreserveAttribute) != null;
            bool hasChildElements = info.Assembly.Elements().Any();

            if (!hasPreserve && !hasChildElements)
            {
                RemoveWithLeadingWhitespace(info.Assembly);
                info.RemovedAssemblies.Add(info.AssemblyName);
                return;
            }

            info.RemovedTypeNames.Add(new LinkXmlValidationTypeGroup(info.AssemblyName, removedTypeNames));
        }

        private static void ProcessType(TypeProcessInfo info)
        {
            string fullname = info.TypeElement.Attribute(FullnameAttribute)?.Value;

            if (string.IsNullOrEmpty(fullname))
            {
                var entry = new LinkXmlValidationSkippedEntry(info.AssemblyName, string.Empty, "Type element has no fullname attribute.");
                info.KeptUnknown.Add(entry);
                return;
            }
            
            if (fullname.IndexOf('*') >= 0)
            {
                return;
            }

            BuildPresence presence = info.Oracle.ResolveType(info.AssemblyName, fullname);
            switch (presence)
            {
                case BuildPresence.Missing:
                {
                    RemoveWithLeadingWhitespace(info.TypeElement);
                    info.RemovedTypeNames.Add(fullname);
                } break;
                
                case BuildPresence.Unknown:
                {
                    var entry = new LinkXmlValidationSkippedEntry(info.AssemblyName, fullname, "Type presence in the build could not be determined.");
                    info.KeptUnknown.Add(entry);
                } break;
            }
        }

        private static void RemoveWithLeadingWhitespace(XElement element)
        {
            if (element.PreviousNode is XText text && string.IsNullOrWhiteSpace(text.Value))
            {
                text.Remove();
            }

            element.Remove();
        }

        private static string Serialize(XDocument document, string originalXml)
        {
            string body = document.ToString(SaveOptions.DisableFormatting);

            if (document.Declaration != null)
            {
                body = document.Declaration + "\n" + body.TrimStart('\r', '\n');
            }

            return body.TrimEnd('\r', '\n') + GetTrailingNewline(originalXml);
        }

        private static string GetTrailingNewline(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return string.Empty;
            }

            if (xml.EndsWith("\r\n", StringComparison.Ordinal))
            {
                return "\r\n";
            }

            return xml.EndsWith("\n", StringComparison.Ordinal) ? "\n" : string.Empty;
        }

        private static bool IsTruthy(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
        
        private readonly struct AssemblyProcessInfo
        {
            public XElement Assembly { get; }
            public IBuildMembershipOracle Oracle { get; }
            public List<string> RemovedAssemblies { get; }
            public List<LinkXmlValidationTypeGroup> RemovedTypes { get; }
            public List<string> KeptIgnoreIfMissing { get; }
            public List<LinkXmlValidationSkippedEntry> KeptUnknown { get; }

            public AssemblyProcessInfo(
                XElement assembly,
                IBuildMembershipOracle oracle,
                List<string> removedAssemblies,
                List<LinkXmlValidationTypeGroup> removedTypes,
                List<string> keptIgnoreIfMissing,
                List<LinkXmlValidationSkippedEntry> keptUnknown)
            {
                Assembly = assembly;
                Oracle = oracle;
                RemovedAssemblies = removedAssemblies;
                RemovedTypes = removedTypes;
                KeptIgnoreIfMissing = keptIgnoreIfMissing;
                KeptUnknown = keptUnknown;
            }
        }
        
        private readonly struct ValidateTypeInfo
        {
            public XElement Assembly { get; }
            public string AssemblyName { get; }
            public IBuildMembershipOracle Oracle { get; }
            public List<string> RemovedAssemblies { get; }
            public List<LinkXmlValidationTypeGroup> RemovedTypeNames { get; }
            public List<LinkXmlValidationSkippedEntry> KeptUnknown { get; }

            public ValidateTypeInfo(
                XElement assembly,
                string assemblyName,
                IBuildMembershipOracle oracle,
                List<string> removedAssemblies,
                List<LinkXmlValidationTypeGroup> removedTypeNames,
                List<LinkXmlValidationSkippedEntry> keptUnknown)
            {
                Assembly = assembly;
                AssemblyName = assemblyName;
                Oracle = oracle;
                RemovedAssemblies = removedAssemblies;
                RemovedTypeNames = removedTypeNames;
                KeptUnknown = keptUnknown;
            }
        }
        
        private readonly struct TypeProcessInfo
        {
            public XElement TypeElement { get; }
            public string AssemblyName { get; }
            public IBuildMembershipOracle Oracle { get; }
            public List<string> RemovedTypeNames { get; }
            public List<LinkXmlValidationSkippedEntry> KeptUnknown { get; }

            public TypeProcessInfo(
                XElement typeElement,
                string assemblyName,
                IBuildMembershipOracle oracle,
                List<string> removedTypeNames,
                List<LinkXmlValidationSkippedEntry> keptUnknown)
            {
                TypeElement = typeElement;
                AssemblyName = assemblyName;
                Oracle = oracle;
                RemovedTypeNames = removedTypeNames;
                KeptUnknown = keptUnknown;
            }
        }
    }
}
