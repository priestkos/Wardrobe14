using Wardrobe14.Logic;
using Xunit;

namespace Wardrobe14.Tests;

public class ModGridTests
{
    private static readonly ModRow[] Rows =
    [
        new("bob", "Bob Cut", "Hair/Short/Bob Cut"),
        new("long", "Long Waves", "Hair/Long Waves"),
        new("boots", "Hairy Boots", "Gear/Hairy Boots"),
        new("loose", "Loose Mod", "Loose Mod"),
    ];

    private static readonly Dictionary<string, ModCell> A = new()
    {
        ["bob"] = new(true, 0, false, false),
        ["long"] = new(false, 5, false, false),
        ["boots"] = new(true, 0, true, false),
    };

    private static readonly Dictionary<string, ModCell> B = new()
    {
        ["bob"] = new(true, 0, false, false),
        ["boots"] = new(false, 0, false, false),
    };

    private static List<string> Dirs(List<ModRow> rows) => rows.Select(r => r.Directory).ToList();

    [Fact]
    public void NoFilterKeepsAllInOrder()
        => Assert.Equal(["bob", "long", "boots", "loose"], Dirs(ModGrid.Filter(Rows, [A, B], "", "", -1, false)));

    [Fact]
    public void TextMatchesNameOrPathCaseInsensitive()
    {
        Assert.Equal(["bob", "long", "boots"], Dirs(ModGrid.Filter(Rows, [A], "hair", "", -1, false)));
        Assert.Equal(["bob"], Dirs(ModGrid.Filter(Rows, [A], "SHORT", "", -1, false)));
    }

    [Fact]
    public void FolderIsPathPrefixNotSubstring()
    {
        Assert.Equal(["bob", "long"], Dirs(ModGrid.Filter(Rows, [A], "", "hair", -1, false)));
        Assert.Equal(["bob"], Dirs(ModGrid.Filter(Rows, [A], "", "/Hair/Short/", -1, false)));
        Assert.Empty(ModGrid.Filter(Rows, [A], "", "Hai", -1, false));
    }

    [Fact]
    public void MissingEntryCountsAsOff()
    {
        Assert.True(ModGrid.IsOn(A, "bob"));
        Assert.False(ModGrid.IsOn(A, "long"));
        Assert.False(ModGrid.IsOn(A, "loose"));
    }

    [Fact]
    public void OnInColumnAndDiffers()
    {
        Assert.Equal(["bob", "boots"], Dirs(ModGrid.Filter(Rows, [A, B], "", "", 0, false)));
        Assert.Equal(["bob"], Dirs(ModGrid.Filter(Rows, [A, B], "", "", 1, false)));
        Assert.Equal(["boots"], Dirs(ModGrid.Filter(Rows, [A, B], "", "", -1, true)));
        Assert.Empty(ModGrid.Filter(Rows, [A], "", "", -1, true)); // one column never differs
        Assert.Equal(4, ModGrid.Filter(Rows, [A], "", "", 7, false).Count); // out-of-range column = no filter
    }

    [Fact]
    public void FoldersIncludeParents()
        => Assert.Equal(["Gear", "Hair", "Hair/Short"], ModGrid.Folders(Rows));

    [Fact]
    public void FoldersMergeCaseAndSkipRootMods()
        => Assert.Equal(["Hair"], ModGrid.Folders([new("a", "A", "Hair/A"), new("b", "B", "hair/B"), new("h", "Hair", "Hair")]));

    [Fact]
    public void FolderFilterSkipsRootModNamedLikeTheFolder()
        => Assert.Equal(["a"], Dirs(ModGrid.Filter([new("a", "A", "Hair/A"), new("h", "Hair", "Hair")], [A], "", "Hair", -1, false)));

    [Fact]
    public void MissingAndDisabledDoNotDiffer()
    {
        // "long" is disabled in A and has no entry in B: both are off, so not a difference.
        Assert.DoesNotContain("long", Dirs(ModGrid.Filter(Rows, [A, B], "", "", -1, true)));
        // Inherited-on in A vs explicit-off in B is a difference.
        Assert.Contains("boots", Dirs(ModGrid.Filter(Rows, [A, B], "", "", -1, true)));
    }

    [Fact]
    public void FiltersCombineAsAnd()
        => Assert.Equal(["bob"], Dirs(ModGrid.Filter(Rows, [A, B], "b", "Hair", 1, false)));
}
