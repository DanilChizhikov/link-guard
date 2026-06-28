using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DTech.LinkGuard.Editor.Tests
{
    [TestFixture]
    internal sealed class DefineConstraintEvaluatorTests
    {
        [Test]
        public void GetUnsatisfied_NullOrEmptyConstraints_ReturnsEmpty()
        {
            Assert.That(DefineConstraintEvaluator.GetUnsatisfied(null, Defines()), Is.Empty);
            Assert.That(DefineConstraintEvaluator.GetUnsatisfied(Array.Empty<string>(), Defines()), Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_DefinedSymbol_IsSatisfied()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "FOO" },
                Defines("FOO"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_MissingSymbol_ReturnsThatSymbol()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "FOO" },
                Defines("BAR"));

            Assert.That(result, Is.EqualTo(new[] { "FOO" }));
        }

        [Test]
        public void GetUnsatisfied_NegatedSymbolNotDefined_IsSatisfied()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "!FOO" },
                Defines("BAR"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_NegatedSymbolDefined_ReturnsNegatedEntry()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "!FOO" },
                Defines("FOO"));

            Assert.That(result, Is.EqualTo(new[] { "!FOO" }));
        }

        [Test]
        public void GetUnsatisfied_MultipleConstraints_RequiresAll()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "A", "B", "C" },
                Defines("A", "C"));

            Assert.That(result, Is.EqualTo(new[] { "B" }));
        }

        [Test]
        public void GetUnsatisfied_OrConstraintSatisfiedByFirstSymbol_ReturnsEmpty()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "A || B" },
                Defines("A"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_OrConstraintSatisfiedBySecondSymbol_ReturnsEmpty()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "A || B" },
                Defines("B"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_OrConstraintUnsatisfied_ReturnsOriginalConstraint()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "A || B" },
                Defines("C"));

            Assert.That(result, Is.EqualTo(new[] { "A || B" }));
        }

        [Test]
        public void GetUnsatisfied_NegatedOrConstraintSatisfiedByMissingFirstSymbol_ReturnsEmpty()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "!A || B" },
                Defines("C"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_NegatedOrConstraintSatisfiedBySecondSymbol_ReturnsEmpty()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "!A || B" },
                Defines("A", "B"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnsatisfied_NegatedOrConstraintUnsatisfied_ReturnsOriginalConstraint()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "!A || B" },
                Defines("A"));

            Assert.That(result, Is.EqualTo(new[] { "!A || B" }));
        }

        [Test]
        public void GetUnsatisfied_IgnoresBlankEntries()
        {
            List<string> result = DefineConstraintEvaluator.GetUnsatisfied(
                new[] { "", "  ", "FOO" },
                Defines("FOO"));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void IsSatisfied_AllConstraintsMet_ReturnsTrue()
        {
            Assert.That(
                DefineConstraintEvaluator.IsSatisfied(new[] { "A", "!B" }, Defines("A")),
                Is.True);
        }

        [Test]
        public void IsSatisfied_AnyConstraintFails_ReturnsFalse()
        {
            Assert.That(
                DefineConstraintEvaluator.IsSatisfied(new[] { "A", "!B" }, Defines("A", "B")),
                Is.False);
        }

        [Test]
        public void ParseDefines_SplitsAndTrims()
        {
            HashSet<string> result = DefineConstraintEvaluator.ParseDefines(" A ; B;;C , D ");

            Assert.That(result, Is.EquivalentTo(new[] { "A", "B", "C", "D" }));
        }

        [Test]
        public void ParseDefines_NullOrEmpty_ReturnsEmptySet()
        {
            Assert.That(DefineConstraintEvaluator.ParseDefines(null), Is.Empty);
            Assert.That(DefineConstraintEvaluator.ParseDefines(string.Empty), Is.Empty);
        }
        
        private static HashSet<string> Defines(params string[] symbols)
        {
            return new HashSet<string>(symbols, StringComparer.Ordinal);
        }
    }
}
