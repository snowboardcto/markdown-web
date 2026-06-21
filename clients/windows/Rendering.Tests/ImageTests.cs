using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace TheMarkdownWeb.Rendering.Tests;

/// <summary>
/// AC10 — images map to a System.Windows.Controls.Image hosted in an InlineUIContainer or
/// BlockUIContainer, with the source URI recorded (Image.Tag and/or BitmapImage.UriSource),
/// the alt text recorded as the accessible name (AutomationProperties.Name), and NO network
/// fetch performed by Render (the test opens no socket).
/// </summary>
public class ImageTests
{
    [StaFact]
    public void Image_RecordsAbsoluteSourceAndAltName_WithoutNetwork()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("![alt](https://host/pic.png)");

        Image image = FindImageOrFail(document);

        Assert.Equal("https://host/pic.png", FlowDocumentTestHelpers.RecordedImageSource(image));
        Assert.Equal("alt", FlowDocumentTestHelpers.AutomationName(image));
        // No socket is opened: Render records the URI but does not fetch/decode. The test never
        // touches the network, and a created BitmapImage (if any) is not downloaded here.
    }

    [StaFact]
    public void Image_NoAlt_HasEmptyName_SourceRecorded_NoThrow()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("![](https://h/p.png)");

        Image image = FindImageOrFail(document);
        Assert.Equal("https://h/p.png", FlowDocumentTestHelpers.RecordedImageSource(image));
        Assert.True(string.IsNullOrEmpty(FlowDocumentTestHelpers.AutomationName(image)));
    }

    [StaFact]
    public void Image_RelativeSource_ProducesElement_NoThrow_NoSocket()
    {
        var renderer = new FlowDocumentRenderer();

        FlowDocument document = renderer.Render("![a](pic.png)");

        Image image = FindImageOrFail(document);
        Assert.Equal("pic.png", FlowDocumentTestHelpers.RecordedImageSource(image));
        Assert.Equal("a", FlowDocumentTestHelpers.AutomationName(image));
    }

    private static Image FindImageOrFail(FlowDocument document)
    {
        Image? image = FlowDocumentTestHelpers.FindFirstImage(document);
        Assert.NotNull(image);
        return image!;
    }
}
