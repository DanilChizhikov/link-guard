using System;
using System.Collections.Generic;

namespace DTech.LinkGuard.Editor
{
    internal static class DefineConstraintEvaluator
    {
        private static readonly char[] _separators = new[]
        {
            ';',
            ','
        };
        
        public static List<string> GetUnsatisfied(
            IReadOnlyList<string> constraints,
            ISet<string> defines)
        {
            List<string> unsatisfied = new List<string>();

            if (constraints == null)
            {
                return unsatisfied;
            }

            foreach (string raw in constraints)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string constraint = raw.Trim();
                bool negated = constraint[0] == '!';
                string symbol = negated ? constraint.Substring(1).Trim() : constraint;

                if (symbol.Length == 0)
                {
                    continue;
                }

                bool defined = defines != null && defines.Contains(symbol);
                bool satisfied = defined != negated;

                if (!satisfied)
                {
                    unsatisfied.Add(constraint);
                }
            }

            return unsatisfied;
        }

        public static bool IsSatisfied(IReadOnlyList<string> constraints, ISet<string> defines)
        {
            return GetUnsatisfied(constraints, defines).Count == 0;
        }
        
        public static HashSet<string> ParseDefines(string scriptingDefineSymbols)
        {
            HashSet<string> defines = new HashSet<string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(scriptingDefineSymbols))
            {
                return defines;
            }

            string[] parts = scriptingDefineSymbols.Split(_separators, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string trimmed = part.Trim();

                if (trimmed.Length > 0)
                {
                    defines.Add(trimmed);
                }
            }

            return defines;
        }
    }
}
