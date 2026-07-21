using System.Security.Cryptography;
using System.Text;
using OneDriveServerTransfer.Destination;

namespace OneDriveServerTransfer.Tests.Destination;

/// <summary>
/// PathMappingVersion = 1 vectors covering every binding rule of contract section 11:
/// NFC normalization, <c>_xHHHH_</c> encoding, reserved device names, the empty
/// component token, ordinal case-insensitive collision detection including
/// file-versus-folder, deterministic suffix derivation and 10 -&gt; 20 -&gt; full
/// expansion, the 200 UTF-16 code-unit component cap, and idempotent reuse.
/// </summary>
public class PathMapperV1Tests
{
    private static string Hash(string sourceItemId) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceItemId)));

    private static PathMapperV1 CreateMapper(IPathCollisionRegistry registry) => new(registry);

    private static PathMapRequest File(string name, string id, string parent = "") =>
        new(parent, name, id, MappedItemKind.File);

    private static PathMapRequest Folder(string name, string id, string parent = "") =>
        new(parent, name, id, MappedItemKind.Folder);

    [Fact]
    public void VersionIsOne()
    {
        Assert.Equal(1, CreateMapper(new InMemoryPathCollisionRegistry()).Version);
    }

    [Fact]
    public void SimpleNamesPassThroughUnchanged()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        var file = mapper.Map(File("report.pdf", "id-1"));
        var folder = mapper.Map(Folder("Documents", "id-2"));

        Assert.Equal("report.pdf", file.MappedName);
        Assert.Equal("report.pdf", file.RelativePath);
        Assert.False(file.UsedCollisionSuffix);
        Assert.Equal("Documents", folder.MappedName);
        Assert.False(folder.UsedCollisionSuffix);
    }

    [Fact]
    public void NfcNormalizationUnifiesComposedAndDecomposedNames()
    {
        var composed = "café.txt";      // é as the single code unit U+00E9
        var decomposed = "café.txt"; // é as 'e' + combining accent U+0301

        var first = CreateMapper(new InMemoryPathCollisionRegistry()).Map(File(composed, "id-1"));
        var second = CreateMapper(new InMemoryPathCollisionRegistry()).Map(File(decomposed, "id-1"));

        Assert.Equal(first.MappedName, second.MappedName);
        Assert.Equal(composed, first.MappedName);
    }

    [Fact]
    public void WindowsInvalidCharactersAreEncoded()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        var result = mapper.Map(File("a<b>c:d\"e/f\\g|h?i*j", "id-1"));

        Assert.Equal(
            "a_x003C_b_x003E_c_x003A_d_x0022_e_x002F_f_x005C_g_x007C_h_x003F_i_x002A_j",
            result.MappedName);
    }

    [Fact]
    public void AsciiControlCharactersAreEncoded()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        var result = mapper.Map(File("a\u0007b\u001Fc", "id-1"));

        Assert.Equal("a_x0007_b_x001F_c", result.MappedName);
    }

    [Theory]
    [InlineData("name..", "name_x002E__x002E_")]
    [InlineData("name  ", "name_x0020__x0020_")]
    [InlineData("name. ", "name_x002E__x0020_")]
    [InlineData("...", "_x002E__x002E__x002E_")]
    [InlineData("a.b", "a.b")]
    public void TrailingDotsAndSpacesAreEncoded(string source, string expected)
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        Assert.Equal(expected, mapper.Map(File(source, "id-1")).MappedName);
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con", "_con")]
    [InlineData("PRN", "_PRN")]
    [InlineData("AUX", "_AUX")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1", "_COM1")]
    [InlineData("COM9", "_COM9")]
    [InlineData("LPT1", "_LPT1")]
    [InlineData("LPT9", "_LPT9")]
    [InlineData("CONSOLE", "CONSOLE")]
    [InlineData("COM10", "COM10")]
    [InlineData("LPT0", "LPT0")]
    public void ReservedDeviceNamesArePrefixedForFolders(string source, string expected)
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        Assert.Equal(expected, mapper.Map(Folder(source, "id-1")).MappedName);
    }

    [Theory]
    [InlineData("con.txt", "_con.txt")]
    [InlineData("COM1.pdf", "_COM1.pdf")]
    [InlineData("lpt9.csv", "_lpt9.csv")]
    [InlineData("console.txt", "console.txt")]
    public void ReservedDeviceNamesArePrefixedForFiles(string source, string expected)
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        Assert.Equal(expected, mapper.Map(File(source, "id-1")).MappedName);
    }

    [Fact]
    public void EmptyComponentBecomesEmptyTokenWithDeterministicSuffix()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        var result = mapper.Map(File(string.Empty, "id-empty"));

        Assert.Equal("_empty_~" + Hash("id-empty")[..10], result.MappedName);
        Assert.True(result.UsedCollisionSuffix);
    }

    [Fact]
    public void FileCollisionInsertsSuffixBeforeExtension()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(File("report.pdf", "id-a"));

        var result = mapper.Map(File("report.pdf", "id-b"));

        Assert.Equal("report~" + Hash("id-b")[..10] + ".pdf", result.MappedName);
        Assert.True(result.UsedCollisionSuffix);
    }

    [Fact]
    public void FolderCollisionAppendsSuffix()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(Folder("data", "id-a"));

        var result = mapper.Map(Folder("data", "id-b"));

        Assert.Equal("data~" + Hash("id-b")[..10], result.MappedName);
    }

    [Fact]
    public void CollisionDetectionIsOrdinalCaseInsensitive()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(File("Report.PDF", "id-a"));

        var result = mapper.Map(File("report.pdf", "id-b"));

        Assert.True(result.UsedCollisionSuffix);
        Assert.Equal("report~" + Hash("id-b")[..10] + ".pdf", result.MappedName);
    }

    [Fact]
    public void FileVersusFolderConflictsAreCollisions()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(Folder("data", "id-a"));

        var fileResult = mapper.Map(File("data", "id-b"));
        Assert.Equal("data~" + Hash("id-b")[..10], fileResult.MappedName);

        var other = CreateMapper(new InMemoryPathCollisionRegistry());
        other.Map(File("data.txt", "id-a"));

        var folderResult = other.Map(Folder("data.txt", "id-b"));
        Assert.Equal("data.txt~" + Hash("id-b")[..10], folderResult.MappedName);
    }

    [Fact]
    public void RemappingTheSameItemIsIdempotent()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());

        var first = mapper.Map(File("report.pdf", "id-a"));
        var again = mapper.Map(File("report.pdf", "id-a"));

        Assert.Equal(first.MappedName, again.MappedName);
        Assert.False(again.UsedCollisionSuffix);
    }

    [Fact]
    public void RemappingACollidedItemReusesItsDeterministicSuffixName()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(File("report.pdf", "id-a"));
        var collided = mapper.Map(File("report.pdf", "id-b"));

        var again = mapper.Map(File("report.pdf", "id-b"));

        Assert.Equal(collided.MappedName, again.MappedName);
        Assert.True(again.UsedCollisionSuffix);
    }

    [Fact]
    public void ResidualCollisionExpandsSuffixFrom10To20HexCharacters()
    {
        var registry = new InMemoryPathCollisionRegistry();
        var hash = Hash("id-b");
        registry.Register("", "name.txt", new PathCollisionEntry("id-a", MappedItemKind.File));
        registry.Register("", "name~" + hash[..10] + ".txt", new PathCollisionEntry("id-c", MappedItemKind.File));

        var result = CreateMapper(registry).Map(File("name.txt", "id-b"));

        Assert.Equal("name~" + hash[..20] + ".txt", result.MappedName);
    }

    [Fact]
    public void ResidualCollisionExpandsSuffixToTheFullHash()
    {
        var registry = new InMemoryPathCollisionRegistry();
        var hash = Hash("id-b");
        registry.Register("", "name.txt", new PathCollisionEntry("id-a", MappedItemKind.File));
        registry.Register("", "name~" + hash[..10] + ".txt", new PathCollisionEntry("id-c", MappedItemKind.File));
        registry.Register("", "name~" + hash[..20] + ".txt", new PathCollisionEntry("id-d", MappedItemKind.File));

        var result = CreateMapper(registry).Map(File("name.txt", "id-b"));

        Assert.Equal("name~" + hash + ".txt", result.MappedName);
    }

    [Fact]
    public void ComponentsAreCappedAt200Utf16UnitsKeepingTheExtension()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        var source = new string('a', 250) + ".txt";

        var result = mapper.Map(File(source, "id-1"));

        Assert.Equal(200, result.MappedName.Length);
        Assert.Equal(new string('a', 196) + ".txt", result.MappedName);
    }

    [Fact]
    public void TruncationRetainsSuffixAndExtensionOnCollision()
    {
        var registry = new InMemoryPathCollisionRegistry();
        var source = new string('a', 250) + ".txt";
        var mapper = CreateMapper(registry);
        mapper.Map(File(source, "id-a"));

        var result = mapper.Map(File(source, "id-b"));

        var suffix = "~" + Hash("id-b")[..10];
        Assert.Equal(200, result.MappedName.Length);
        Assert.Equal(new string('a', 200 - suffix.Length - 4) + suffix + ".txt", result.MappedName);
    }

    [Fact]
    public void ExtensionIsRetainedOnlyAsMuchAsFits()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        var source = "a." + new string('b', 300);

        var result = mapper.Map(File(source, "id-1"));

        Assert.Equal(200, result.MappedName.Length);
        Assert.Equal("." + new string('b', 199), result.MappedName);
    }

    [Fact]
    public void TruncationNeverSplitsASurrogatePair()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        var source = new string('a', 199) + "\U0001F600" + "more";

        var result = mapper.Map(File(source, "id-1"));

        Assert.Equal(new string('a', 199), result.MappedName);
        Assert.False(char.IsHighSurrogate(result.MappedName[^1]));
    }

    [Fact]
    public void TruncationNeverLeavesATrailingDot()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        var source = new string('a', 195) + "." + new string('b', 100) + ".txt";

        var result = mapper.Map(File(source, "id-1"));

        Assert.Equal(new string('a', 195) + ".txt", result.MappedName);
    }

    [Fact]
    public void MappingIsDeterministicAcrossIndependentRuns()
    {
        var requests = new[]
        {
            File("report.pdf", "id-a"),
            File("report.pdf", "id-b"),
            Folder("data", "id-c"),
            File("data", "id-d"),
            File("café.txt", "id-e"),
            File("name..", "id-f"),
            File("CON", "id-g"),
            File(new string('x', 250) + ".bin", "id-h")
        };

        string[] RunAll()
        {
            var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
            return requests.Select(request => mapper.Map(request).MappedName).ToArray();
        }

        Assert.Equal(RunAll(), RunAll());
    }

    [Fact]
    public void RelativePathJoinsTheMappedParent()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(Folder("parent", "id-p"));

        var child = mapper.Map(File("child.txt", "id-c", parent: "parent"));

        Assert.Equal("child.txt", child.MappedName);
        Assert.Equal("parent\\child.txt", child.RelativePath);
    }

    [Fact]
    public void CollisionsAreScopedToTheirParentDirectory()
    {
        var mapper = CreateMapper(new InMemoryPathCollisionRegistry());
        mapper.Map(File("same.txt", "id-a", parent: "one"));

        var otherParent = mapper.Map(File("same.txt", "id-b", parent: "two"));
        var sameParent = mapper.Map(File("same.txt", "id-c", parent: "one"));

        Assert.False(otherParent.UsedCollisionSuffix);
        Assert.True(sameParent.UsedCollisionSuffix);
    }

    [Fact]
    public void RegistryReusesMappingsBySourceItemId()
    {
        var registry = new InMemoryPathCollisionRegistry();
        var mapper = CreateMapper(registry);
        mapper.Map(File("report.pdf", "id-a"));
        var collided = mapper.Map(File("report.pdf", "id-b"));

        Assert.Equal("report.pdf", registry.FindMappedNameByItemId("", "id-a"));
        Assert.Equal(collided.MappedName, registry.FindMappedNameByItemId("", "id-b"));
        Assert.Null(registry.FindMappedNameByItemId("", "id-unknown"));
    }
}
