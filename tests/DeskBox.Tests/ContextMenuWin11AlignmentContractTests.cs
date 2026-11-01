namespace DeskBox.Tests;

/// <summary>
/// Pins the Windows 11-aligned context menu shape: naming keys, item
/// ordering, the split View / Sort by submenus, and the process-isolation
/// boundary (Open With and New go through association/registry data only;
/// the classic IContextMenu path stays with the out-of-process proxy).
/// </summary>
public sealed class ContextMenuWin11AlignmentContractTests
{
    [Fact]
    public void ItemMenu_UsesWin11NamingKeys()
    {
        string builder = ReadRepositoryFile(
            "src/DeskBox/Controls/FileItemMenuBuilder.cs");

        foreach (string key in new[]
                 {
                     "Widget.Open",
                     "Widget.RunAsAdministrator",
                     "Widget.ExtractAll",
                     "Common.Cut",
                     "Common.Copy",
                     "Common.Rename",
                     "Widget.CompressTo",
                     "Widget.CompressTo.Zip",
                     "Widget.CompressTo.Tar",
                     "Widget.CopyPath",
                     "Widget.MoveToRecycleBin",
                     "Widget.ShowInExplorer",
                     "Common.Properties",
                     "Widget.MoreSystemOperations",
                 })
        {
            Assert.Contains(key, builder, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ItemMenu_Win11Shape_StripOrder_RowOrder_EscapeHatchLast()
    {
        string builder = ReadRepositoryFile(
            "src/DeskBox/Controls/FileItemMenuBuilder.cs");

        // CommandBarFlyout shape: the compact strip renders cut / copy /
        // rename / delete (Windows 11 command order); the secondary rows
        // lead with the open family and end with the system-menu escape
        // hatch. Scoped to the single-item builder region; the shared strip
        // helpers sit after both builders in the file, so their literal
        // order is pinned separately below.
        int singleStart = builder.IndexOf(
            "public static CommandBarFlyout CreateItemFlyout(",
            StringComparison.Ordinal);
        int singleEnd = builder.IndexOf(
            "public static CommandBarFlyout CreateMultiSelectionFlyout(",
            StringComparison.Ordinal);
        Assert.True(singleStart >= 0 && singleEnd > singleStart);
        string single = builder[singleStart..singleEnd];

        int strip = single.IndexOf(
            "AddClipboardStrip(flyout, actions, item, includeRename: true);",
            StringComparison.Ordinal);
        int deleteStrip = single.IndexOf(
            "AddDeleteStripButton(flyout, actions);",
            StringComparison.Ordinal);
        int openRow = single.IndexOf(
            "AddOpenRow(flyout, item, actions);",
            StringComparison.Ordinal);
        int openWith = single.IndexOf(
            "AddOpenWithRow(flyout, item, actions);",
            StringComparison.Ordinal);
        int extract = single.IndexOf(
            "\"Widget.ExtractAll\"",
            StringComparison.Ordinal);
        int compress = single.IndexOf(
            "AddCompressToZipRow(flyout, actions);",
            StringComparison.Ordinal);
        int copyPath = single.IndexOf(
            "\"Widget.CopyPath\"",
            StringComparison.Ordinal);
        int properties = single.IndexOf(
            "\"Common.Properties\"",
            StringComparison.Ordinal);
        int moreOptions = single.IndexOf(
            "AddShowMoreOptionsRow(flyout, item, actions);",
            StringComparison.Ordinal);

        Assert.True(strip >= 0 && deleteStrip > strip,
            "The strip leads the menu");
        Assert.True(openRow > deleteStrip && openWith > openRow,
            "Open family leads the text rows");
        Assert.True(extract > openWith, "Extract stays in the open family");
        Assert.True(compress > extract && compress < copyPath,
            "Compress sits before Copy as path");
        Assert.True(properties > copyPath,
            "Properties stays visible in the rows");
        Assert.True(moreOptions > properties,
            "Show more options is the last row");

        // Strip helper literal order: cut / copy / rename, then delete.
        int cut = builder.IndexOf("\"Common.Cut\"", StringComparison.Ordinal);
        int copy = builder.IndexOf("\"Common.Copy\"", StringComparison.Ordinal);
        int rename = builder.IndexOf(
            "\"Common.Rename\"",
            StringComparison.Ordinal);
        int delete = builder.IndexOf(
            "\"Widget.MoveToRecycleBin\"",
            StringComparison.Ordinal);
        Assert.True(cut >= 0 && copy > cut && rename > copy && delete > rename,
            "Strip order is cut / copy / rename / delete");

        // AlwaysExpanded kills the overflow/collapse button and the
        // collapsed-then-expand opening animation (the two-step flash);
        // the Open With submenu opens as its own MenuFlyout beside the row.
        Assert.Contains(
            "AlwaysExpanded = true",
            builder,
            StringComparison.Ordinal);
        Assert.Contains(
            "FlyoutBase.ShowAttachedFlyout(row)",
            builder,
            StringComparison.Ordinal);
        Assert.Contains(
            "AppBarButtonHasFlyoutChevronVisibility",
            builder,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MultiSelectionMenu_AddsOpenAndCompress()
    {
        string builder = ReadRepositoryFile(
            "src/DeskBox/Controls/FileItemMenuBuilder.cs");

        string multiStart = builder.Substring(builder.IndexOf(
            "CreateMultiSelectionFlyout",
            StringComparison.Ordinal));
        Assert.Contains("Widget.Open", multiStart, StringComparison.Ordinal);
        Assert.Contains(
            "Widget.CompressTo",
            multiStart,
            StringComparison.Ordinal);
        Assert.Contains(
            "await actions.OpenItemAsync(item);",
            multiStart,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ContentAreaMenu_SplitsViewAndSort_AndAddsNewSubmenu()
    {
        string surface = ReadRepositoryFile(
            "src/DeskBox/Controls/WidgetContents/" +
            "FileSurfaceContent.SelectionAndMenus.cs");

        Assert.Contains(
            "T(\"Widget.View\")",
            surface,
            StringComparison.Ordinal);
        Assert.Contains(
            "T(\"Widget.SortBy\")",
            surface,
            StringComparison.Ordinal);
        Assert.Contains(
            "private MenuFlyoutSubItem CreateNewSubMenu",
            surface,
            StringComparison.Ordinal);
        Assert.Contains(
            "ShellNewMenuHelper.GetEntries()",
            surface,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "T(\"Widget.ViewAndSort\")",
            surface,
            StringComparison.Ordinal);
    }

    [Fact]
    public void OpenWithAndNew_Paths_NeverHostClassicContextMenu()
    {
        string openWithInterop = ReadRepositoryFile(
            "src/DeskBox/Platform/ShellOpenWithInterop.cs");
        string openWithHelper = ReadRepositoryFile(
            "src/DeskBox/Helpers/ShellOpenWithHelper.cs");
        string shellNew = ReadRepositoryFile(
            "src/DeskBox/Helpers/ShellNewMenuHelper.cs");

        foreach (string source in new[] { openWithInterop, openWithHelper })
        {
            Assert.DoesNotContain(
                "IContextMenu",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "QueryContextMenu",
                source,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "TrackPopupMenuEx",
                source,
                StringComparison.Ordinal);
        }

        // The association API entry points stay pinned so the interop can
        // never silently drift onto the classic menu path.
        Assert.Contains(
            "SHAssocEnumHandlers",
            openWithInterop,
            StringComparison.Ordinal);
        Assert.Contains(
            "AssocFilterRecommended",
            openWithInterop,
            StringComparison.Ordinal);
        Assert.Contains(
            "Registry.ClassesRoot",
            shellNew,
            StringComparison.Ordinal);

        // 2026-10-16 crash pins: interface-method arrays default to
        // SAFEARRAY marshaling and the SafeArray marshaler hard-crashes the
        // CLR on IntPtr[]; Invoke requires a real IDataObject obtained via
        // BindToHandler(BHID_DataObject).
        Assert.Contains(
            "[MarshalAs(UnmanagedType.LPArray), Out] IntPtr[] rgelt",
            openWithInterop,
            StringComparison.Ordinal);
        Assert.Contains(
            "b8c0bd9f-ed24-455c-83e6-d5390c4fe8c4",
            openWithInterop,
            StringComparison.Ordinal);
        Assert.Contains(
            "BindToHandler",
            openWithInterop,
            StringComparison.Ordinal);

        string surface = ReadRepositoryFile(
            "src/DeskBox/Controls/WidgetContents/" +
            "FileSurfaceContent.SelectionAndMenus.cs");
        // The row label is built via Localize in the builder; the host
        // supplies the submenu entries including the choose-app tail.
        Assert.Contains(
            "\"Widget.OpenWith\"",
            ReadRepositoryFile("src/DeskBox/Controls/FileItemMenuBuilder.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "T(\"Widget.OpenWith.ChooseApp\")",
            surface,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SearchResultsMenu_KeepsRunAsAdministratorEntry()
    {
        string searchWindow = ReadRepositoryFile(
            "src/DeskBox/Views/SearchPopupWindow.xaml.cs");

        Assert.Contains(
            "ElevatedFileLauncher.CanRunAsAdministrator(item.DetailPath",
            searchWindow,
            StringComparison.Ordinal);
        Assert.Contains(
            "Widget.RunAsAdministrator",
            searchWindow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Localization_NewKeysPresent_RetiredKeyGone_AllLanguages()
    {
        string stringsDir = TestPaths.FromRepository("src/DeskBox/Strings");
        string[] files = Directory.GetFiles(stringsDir, "*.json");
        Assert.True(files.Length >= 12, "expected the full language set");

        string[] newKeys =
        [
            "Widget.OpenWith",
            "Widget.OpenWith.ChooseApp",
            "Widget.ExtractAll",
            "Widget.CompressTo",
            "Widget.CompressTo.Zip",
            "Widget.CompressTo.Tar",
            "Widget.New",
            "Widget.View",
            "Widget.SortBy",
            "Widget.NewFileName",
            "Widget.Zip.CompressFailed",
            "Widget.Zip.ExtractFailed",
        ];

        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            foreach (string key in newKeys)
            {
                Assert.Contains($"\"{key}\":", text, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(
                "\"Widget.ViewAndSort\":",
                text,
                StringComparison.Ordinal);
        }
    }

    private static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(TestPaths.FromRepository(relativePath));
}
