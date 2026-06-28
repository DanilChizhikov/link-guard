using System;

namespace DTech.LinkGuard.Editor.Tests
{
    internal sealed class FakeLinkXmlMergeProvider : ILinkXmlMergeProvider
    {
        private readonly Func<LinkXmlProviderResult> _provide;

        public string Id { get; }
        public string ButtonLabel => Id;
        public string Tooltip => string.Empty;

        public FakeLinkXmlMergeProvider(string id, Func<LinkXmlProviderResult> provide)
        {
            Id = id;
            _provide = provide;
        }

        public LinkXmlProviderResult Provide()
        {
            return _provide();
        }
    }
}
