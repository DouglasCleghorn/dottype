using System.Reflection.Metadata;

namespace dottype.tests;

public sealed class BuildPrivacyTests
{
    [Fact]
    public void Application_symbols_use_neutral_paths_and_do_not_embed_untracked_source()
    {
        string pdbPath = Path.ChangeExtension(typeof(App).Assembly.Location, ".pdb");
        using var input = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(input);
        MetadataReader metadata = provider.GetMetadataReader();
        Assert.NotEmpty(metadata.Documents);
        foreach (DocumentHandle handle in metadata.Documents)
        {
            Document document = metadata.GetDocument(handle);
            string path = metadata.GetString(document.Name);
            Assert.StartsWith("/_/", path.Replace('\\', '/'));
        }

        var embeddedSourceKind = new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
        foreach (CustomDebugInformationHandle handle in metadata.CustomDebugInformation)
        {
            CustomDebugInformation info = metadata.GetCustomDebugInformation(handle);
            Assert.NotEqual(embeddedSourceKind, metadata.GetGuid(info.Kind));
        }
    }
}
