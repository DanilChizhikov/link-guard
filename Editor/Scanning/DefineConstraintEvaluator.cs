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

        private static readonly string[] _orSeparator = new[]
        {
            "||"
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

                if (!IsConstraintSatisfied(raw.Trim(), defines))
                {
                    unsatisfied.Add(raw.Trim());
                }
            }

            return unsatisfied;
        }

        public static bool IsSatisfied(IReadOnlyList<string> constraints, ISet<string> defines)
        {
            return GetUnsatisfied(constraints, defines).Count == 0;
        }

        private static bool IsConstraintSatisfied(string constraint, ISet<string> defines)
        {
            string[] parts = constraint.Split(_orSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string clause = part.Trim();

                if (clause.Length == 0)
                {
                    continue;
                }

                bool negated = clause[0] == '!';
                string symbol = negated ? clause.Substring(1).Trim() : clause;

                if (symbol.Length == 0)
                {
                    continue;
                }

                bool defined = defines != null && defines.Contains(symbol);

                if (defined != negated)
                {
                    return true;
                }
            }

            return false;
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
