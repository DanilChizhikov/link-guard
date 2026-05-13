#if LINKGUARD_ZENJECT_ENABLED
using System;
using System.Collections.Generic;
using System.Reflection;
using ZN = global::Zenject;

namespace DTech.LinkGuard.Editor.Zenject
{
    internal static class ZenjectInjectAttributeScanner
    {
        private const BindingFlags MemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.DeclaredOnly;

        public static HashSet<Type> Scan(IReadOnlyCollection<Assembly> reachableAssemblies, List<string> warnings)
        {
            HashSet<Type> result = new HashSet<Type>();

            if (reachableAssemblies == null || reachableAssemblies.Count == 0)
            {
                return result;
            }

            HashSet<Assembly> filter = new HashSet<Assembly>(reachableAssemblies);

            foreach (Assembly assembly in filter)
            {
                if (assembly == null)
                {
                    continue;
                }

                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                    warnings.Add($"Reflection load issues in '{assembly.GetName().Name}': {ex.Message}");
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to enumerate types in '{assembly.GetName().Name}': {ex.Message}");
                    continue;
                }

                if (types == null)
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null || !type.IsClass)
                    {
                        continue;
                    }

                    if (HasInjectAttribute(type))
                    {
                        result.Add(type);
                    }
                }
            }

            return result;
        }

        private static bool HasInjectAttribute(Type type)
        {
            try
            {
                foreach (ConstructorInfo ctor in type.GetConstructors(MemberFlags))
                {
                    if (ctor.IsDefined(typeof(ZN.InjectAttribute), false))
                    {
                        return true;
                    }
                }

                foreach (MethodInfo method in type.GetMethods(MemberFlags))
                {
                    if (method.IsDefined(typeof(ZN.InjectAttribute), false))
                    {
                        return true;
                    }
                }

                foreach (FieldInfo field in type.GetFields(MemberFlags))
                {
                    if (field.IsDefined(typeof(ZN.InjectAttribute), false))
                    {
                        return true;
                    }
                }

                foreach (PropertyInfo property in type.GetProperties(MemberFlags))
                {
                    if (property.IsDefined(typeof(ZN.InjectAttribute), false))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
#endif
