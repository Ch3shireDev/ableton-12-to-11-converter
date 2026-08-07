using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AbletonAlsDowngrader.Models;

namespace AbletonAlsDowngrader.Services;

public sealed class AlsDowngradeService
{
    private static readonly string[] ElementsRemovedForLive11 =
    [
        "ContentLanes",
        "ExpressionLanes",
        "InstrumentMeld",
        "Roar",
        "MxPatchRef"
    ];

    private static readonly string[] AttributesRemovedForLive11 =
    [
        "SelectedToolPanel",
        "SelectedTransformationName",
        "SelectedGeneratorName",
        "InitUpdateAreSlicesFromOnsetsEditableAfterRead"
    ];

    public async Task<ConversionResult> DowngradeAsync(Stream alsStream, CancellationToken cancellationToken = default)
    {
        await using var xmlStream = new MemoryStream();

        try
        {
            await using var gzip = new GZipStream(alsStream, CompressionMode.Decompress, leaveOpen: true);
            await gzip.CopyToAsync(xmlStream, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException("The selected file isn't a valid gzip-compressed Ableton Live Set (.als).", ex);
        }

        xmlStream.Position = 0;

        XDocument document;
        try
        {
            document = await XDocument.LoadAsync(
                xmlStream,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo,
                cancellationToken);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            throw new InvalidDataException("The .als payload isn't valid Ableton XML.", ex);
        }

        var ableton = document.Root
            ?? throw new InvalidDataException("The .als XML has no root element.");

        if (!string.Equals(ableton.Name.LocalName, "Ableton", StringComparison.Ordinal))
            throw new InvalidDataException("The XML root is not <Ableton>.");

        var sourceCreator = (string?)ableton.Attribute("Creator") ?? "Unknown";
        var sourceMinorVersion = (string?)ableton.Attribute("MinorVersion") ?? "Unknown";

        if (!sourceMinorVersion.StartsWith("12.", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"This micro-converter accepts Live 12 sets only. Detected MinorVersion='{sourceMinorVersion}'.");
        }

        // Values used by the open-source live_set 0.2.3 downgrade implementation.
        ableton.SetAttributeValue("Creator", "Ableton Live 11.3.21");
        ableton.SetAttributeValue("MajorVersion", "5");
        ableton.SetAttributeValue("MinorVersion", "11.0_11300");
        ableton.SetAttributeValue("Revision", "5ac24cad7c51ea0671d49e6b4885371f15b57c1e");
        ableton.SetAttributeValue("SchemaChangeCount", "3");

        var removed = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var elementName in ElementsRemovedForLive11)
        {
            var elements = document
                .Descendants()
                .Where(e => string.Equals(e.Name.LocalName, elementName, StringComparison.Ordinal))
                .ToList();

            removed[elementName] = elements.Count;
            foreach (var element in elements)
                element.Remove();
        }

        var removedAttributes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var attributeName in AttributesRemovedForLive11)
        {
            var attributes = ableton
                .DescendantsAndSelf()
                .Attributes()
                .Where(a => string.Equals(a.Name.LocalName, attributeName, StringComparison.Ordinal))
                .ToList();

            removedAttributes[attributeName] = attributes.Count;
            foreach (var attribute in attributes)
                attribute.Remove();
        }

        // Live 12 renamed the lane model used by the MIDI editor. Live 11 expects
        // the older ExpressionLane element name. Preserve the complete element
        // contents and attributes; only translate the XML element name.
        var midiEditorLaneModels = document
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "MidiEditorLaneModel", StringComparison.Ordinal))
            .ToList();

        foreach (var element in midiEditorLaneModels)
            element.Name = element.Name.Namespace + "ExpressionLane";

        string xml;
        using (var writer = new Utf8StringWriter())
        {
            document.Save(writer, SaveOptions.DisableFormatting);
            xml = writer.ToString();
        }

        const string live12Routing = "AudioOut/Main";
        const string live11Routing = "AudioOut/Master";
        var routingReplacements = CountOccurrences(xml, live12Routing);
        xml = xml.Replace(live12Routing, live11Routing, StringComparison.Ordinal);

        await using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        await using (var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 1024, leaveOpen: true))
        {
            await writer.WriteAsync(xml.AsMemory(), cancellationToken);
        }

        return new ConversionResult(
            output.ToArray(),
            sourceCreator,
            sourceMinorVersion,
            removed,
            removedAttributes,
            midiEditorLaneModels.Count,
            routingReplacements);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var position = 0;

        while ((position = text.IndexOf(value, position, StringComparison.Ordinal)) >= 0)
        {
            count++;
            position += value.Length;
        }

        return count;
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
