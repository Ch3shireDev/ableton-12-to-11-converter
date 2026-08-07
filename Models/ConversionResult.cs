namespace AbletonAlsDowngrader.Models;

public sealed record ConversionResult(
    byte[] Data,
    string SourceCreator,
    string SourceMinorVersion,
    IReadOnlyDictionary<string, int> RemovedElements,
    IReadOnlyDictionary<string, int> RemovedAttributes,
    int RenamedMidiEditorLaneModels,
    int RoutingReplacements);
