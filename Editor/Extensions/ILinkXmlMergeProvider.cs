namespace DTech.LinkGuard.Editor
{
    public interface ILinkXmlMergeProvider
    {
        string Id { get; }
        string ButtonLabel { get; }
        string Tooltip { get; }
        LinkXmlProviderResult Provide();
    }
}
