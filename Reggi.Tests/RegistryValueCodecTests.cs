using Reggi.Core;
using Xunit;

namespace Reggi.Tests;

public sealed class RegistryValueCodecTests
{
    [Fact]
    public void MultiStringRoundTripPreservesEveryEntryExactly()
    {
        string[] original =
        [
            " leading",
            "middle;semicolon",
            @"C:\Temp",
            "trailing ",
            string.Empty,
            @"\0"
        ];

        var editable = RegistryValueCodec.ToEditable(RegistryValueType.MultiString, original);
        var parsed = Assert.IsType<string[]>(RegistryValueCodec.Parse(RegistryValueType.MultiString, editable));

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void MultiStringDistinguishesNoEntriesFromOneEmptyEntry()
    {
        var noEntries = RegistryValueCodec.ToEditable(RegistryValueType.MultiString, Array.Empty<string>());
        var oneEmptyEntry = RegistryValueCodec.ToEditable(RegistryValueType.MultiString, new[] { string.Empty });

        Assert.NotEqual(noEntries, oneEmptyEntry);
        Assert.Empty(Assert.IsType<string[]>(RegistryValueCodec.Parse(RegistryValueType.MultiString, noEntries)));
        Assert.Equal(
            new[] { string.Empty },
            Assert.IsType<string[]>(RegistryValueCodec.Parse(RegistryValueType.MultiString, oneEmptyEntry)));
    }

    [Fact]
    public void NoneRoundTripPreservesBinaryData()
    {
        byte[] original = [0x01, 0x02, 0xFF];

        var editable = RegistryValueCodec.ToEditable(RegistryValueType.None, original);
        var parsed = Assert.IsType<byte[]>(RegistryValueCodec.Parse(RegistryValueType.None, editable));

        Assert.Equal("01 02 FF", editable);
        Assert.Equal(original, parsed);
        Assert.Equal("01 02 FF", RegistryValueCodec.Format(RegistryValueType.None, original));
    }

    [Fact]
    public void UnsupportedKindCannotBeParsedForEditing()
    {
        var error = Assert.Throws<FormatException>(
            () => RegistryValueCodec.Parse(RegistryValueType.Unknown, "01 02 FF"));

        Assert.Contains("cannot be edited safely", error.Message);
    }

    [Fact]
    public void MultiStringSearchUsesDataRatherThanEditorEscapes()
    {
        string[] value = ["part;with;semicolons", @"C:\Temp"];

        var searchable = RegistryValueCodec.SearchableForms(RegistryValueType.MultiString, value).ToArray();

        Assert.Equal(value, searchable);
    }
}
