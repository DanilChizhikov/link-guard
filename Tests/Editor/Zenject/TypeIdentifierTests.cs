#if LINKGUARD_ZENJECT_ENABLED
using System;
using Mono.Cecil;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Zenject.Tests
{
    [TestFixture]
    public sealed class TypeIdentifierTests
    {
        [Test]
        public void From_StringPair_NullOrEmpty_ReturnsNull()
        {
            Assert.That(TypeIdentifier.From(null, "Foo"), Is.Null);
            Assert.That(TypeIdentifier.From("Asm", null), Is.Null);
            Assert.That(TypeIdentifier.From("", "Foo"), Is.Null);
            Assert.That(TypeIdentifier.From("Asm", ""), Is.Null);
        }

        [Test]
        public void From_StringPair_ReturnsInstance_WithExpectedFields()
        {
            TypeIdentifier id = TypeIdentifier.From("Game.Core", "Ns.Foo");

            Assert.That(id, Is.Not.Null);
            Assert.That(id.AssemblyName, Is.EqualTo("Game.Core"));
            Assert.That(id.TypeFullname, Is.EqualTo("Ns.Foo"));
            Assert.That(id.IsGenericParameter, Is.False);
        }

        [Test]
        public void From_TypeReference_Null_ReturnsNull()
        {
            Assert.That(TypeIdentifier.From((TypeReference)null), Is.Null);
        }

        [Test]
        public void From_TypeReference_GenericParameter_FlagsIsGenericParameter()
        {
            using ModuleDefinition module = ModuleDefinition.CreateModule("TestAsm", ModuleKind.Dll);
            TypeDefinition declaringType = new TypeDefinition("Ns", "MyGeneric`1", TypeAttributes.Public, module.TypeSystem.Object);
            GenericParameter genericParam = new GenericParameter("T", declaringType);
            declaringType.GenericParameters.Add(genericParam);
            module.Types.Add(declaringType);

            TypeIdentifier id = TypeIdentifier.From(genericParam);

            Assert.That(id, Is.Not.Null);
            Assert.That(id.IsGenericParameter, Is.True);
            Assert.That(id.AssemblyName, Is.EqualTo(string.Empty));
            Assert.That(id.TypeFullname, Is.EqualTo("T"));
        }

        [Test]
        public void From_TypeReference_StripsDllSuffixFromAssemblyScope()
        {
            using ModuleDefinition module = ModuleDefinition.CreateModule("Game.Core.dll", ModuleKind.Dll);
            TypeDefinition typeDef = new TypeDefinition("Ns", "Foo", TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(typeDef);

            TypeIdentifier id = TypeIdentifier.From(typeDef);

            Assert.That(id, Is.Not.Null);
            Assert.That(id.AssemblyName, Is.EqualTo("Game.Core"));
            Assert.That(id.TypeFullname, Is.EqualTo("Ns.Foo"));
        }

        [Test]
        public void From_TypeReference_PreservesScopeNameWithoutDllSuffix()
        {
            using ModuleDefinition module = ModuleDefinition.CreateModule("Game.Core", ModuleKind.Dll);
            TypeDefinition typeDef = new TypeDefinition("Ns", "Foo", TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(typeDef);

            TypeIdentifier id = TypeIdentifier.From(typeDef);

            Assert.That(id.AssemblyName, Is.EqualTo("Game.Core"));
        }

        [Test]
        public void From_TypeReference_UnwrapsGenericInstanceType_ToElementType()
        {
            using ModuleDefinition module = ModuleDefinition.CreateModule("Game.Core.dll", ModuleKind.Dll);
            TypeDefinition openGeneric = new TypeDefinition("Ns", "Container`1", TypeAttributes.Public, module.TypeSystem.Object);
            openGeneric.GenericParameters.Add(new GenericParameter("T", openGeneric));
            module.Types.Add(openGeneric);

            GenericInstanceType closed = new GenericInstanceType(openGeneric);
            closed.GenericArguments.Add(module.TypeSystem.Int32);

            TypeIdentifier id = TypeIdentifier.From(closed);

            Assert.That(id, Is.Not.Null);
            Assert.That(id.AssemblyName, Is.EqualTo("Game.Core"));
            Assert.That(id.TypeFullname, Is.EqualTo("Ns.Container`1"));
        }

        [Test]
        public void From_TypeReference_DropsAngleBracketSuffix_FromFullname()
        {
            using ModuleDefinition module = ModuleDefinition.CreateModule("Game.Core.dll", ModuleKind.Dll);
            TypeReference reference = new TypeReference(
                "Ns",
                "Foo<Bar>",
                module,
                module);

            TypeIdentifier id = TypeIdentifier.From(reference);

            Assert.That(id, Is.Not.Null);
            Assert.That(id.TypeFullname, Is.EqualTo("Ns.Foo"));
        }

        [Test]
        public void Equals_MatchesByAssemblyAndFullnameOnly()
        {
            TypeIdentifier a = TypeIdentifier.From("Asm", "Ns.Foo");
            TypeIdentifier b = TypeIdentifier.From("Asm", "Ns.Foo");

            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals((object)b), Is.True);
        }

        [Test]
        public void Equals_DifferentAssemblyName_NotEqual()
        {
            TypeIdentifier a = TypeIdentifier.From("AsmA", "Ns.Foo");
            TypeIdentifier b = TypeIdentifier.From("AsmB", "Ns.Foo");

            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void Equals_NullOther_ReturnsFalse()
        {
            TypeIdentifier a = TypeIdentifier.From("Asm", "Ns.Foo");

            Assert.That(a.Equals(null), Is.False);
            Assert.That(a.Equals((object)null), Is.False);
        }

        [Test]
        public void GetHashCode_IsStableForEqualInstances()
        {
            TypeIdentifier a = TypeIdentifier.From("Asm", "Ns.Foo");
            TypeIdentifier b = TypeIdentifier.From("Asm", "Ns.Foo");

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ToString_FormatsAsTypeAtAssembly()
        {
            TypeIdentifier id = TypeIdentifier.From("Game.Core", "Ns.Foo");

            Assert.That(id.ToString(), Is.EqualTo("Ns.Foo@Game.Core"));
        }
    }
}
#endif
