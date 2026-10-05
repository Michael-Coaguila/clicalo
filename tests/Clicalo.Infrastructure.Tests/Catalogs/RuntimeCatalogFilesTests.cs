using System.Text;
using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Catalogs;

/// <summary>
/// The catalogs the panel reads at run time from the shipped <c>catalogs</c> folder: the key labels (CUA-007, CUA-008,
/// R-04) and the adaptive common actions (EJE-018, decision D4). Untrusted data: a broken file gives the empty catalog.
/// </summary>
public sealed class RuntimeCatalogFilesTests
{
    private static readonly string Shipped = Path.Combine(RepoPaths.Data, "catalogs");

    [Fact]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "CUA-008")]
    public void The_shipped_key_labels_write_the_key_lines_of_the_prototype()
    {
        var labels = RuntimeCatalogFiles.LoadKeyLabels(Shipped);
        var chord = KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Shift, new KeyId("t"));

        labels.Count.ShouldBe(KeyDefinitions.All.Length);
        KeyChordFormatter
            .Format(chord, labels, KeyLabelStyle.Full, LangCode.Es, LangCode.Es)
            .ShouldBe("Ctrl + Shift + T");
        KeyChordFormatter
            .Format(chord, labels, KeyLabelStyle.Abbreviated, LangCode.Es, LangCode.Es)
            .ShouldBe("Ctl+⇧+T");
        KeyChordFormatter
            .Format(
                KeyChord.FromKeys(KeyIds.Win, new KeyId("left")),
                labels,
                KeyLabelStyle.Spoken,
                LangCode.Es,
                LangCode.Es
            )
            .ShouldBe("Win + Flecha izquierda");
    }

    public static TheoryData<string, string, string, string> OfficeInSpanish =>
        new()
        {
            // action, app, programs language → combination sent
            { "save", "winword.exe", "es", "ctrl+g" },
            { "save", "olk.exe", "es", "ctrl+g" },
            { "save", "winword.exe", "en", "ctrl+s" },
            { "save", "chrome.exe", "es", "ctrl+s" },
            { "selall", "excel.exe", "es", "ctrl+e" },
            { "selall", "notepad.exe", "es", "ctrl+a" },
            { "find", "winword.exe", "es", "ctrl+b" },
            { "find", "powerpnt.exe", "es", "ctrl+f" },
            { "bold", "powerpnt.exe", "es", "ctrl+n" },
            { "italic", "outlook.exe", "es", "ctrl+k" },
            { "underline", "excel.exe", "es", "ctrl+s" },
        };

    [Theory]
    [Trait("Req", "EJE-018")]
    [MemberData(nameof(OfficeInSpanish))]
    public void The_shipped_common_actions_adapt_to_Office_in_Spanish(
        string action,
        string app,
        string appsLanguage,
        string expected
    )
    {
        var table = RuntimeCatalogFiles.LoadCommonActions(Shipped);
        var shortcut = Seed(action, table);

        table
            .ChordToSend(
                shortcut,
                (TapAction)shortcut.Action,
                new ProcessName(app),
                new LangCode(appsLanguage)
            )
            .ShouldBe(Chord(expected));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData(
        "{\"origins\":[\"seed\"],\"families\":[],\"actions\":[{\"id\":\"save\",\"keys\":[\"ctrl\",\"nope\"],\"exceptions\":[]}]}"
    )]
    [InlineData(
        "{\"origins\":[\"seed\"],\"families\":[],\"actions\":[{\"id\":\"save\",\"keys\":[\"ctrl\",\"s\"],\"exceptions\":[{\"families\":[\"ghost\"],\"appsLanguage\":\"es\",\"keys\":[\"ctrl\",\"g\"]}]}]}"
    )]
    [Trait("Req", "LOG-006")]
    public void A_broken_common_actions_file_is_rejected_whole(string json)
    {
        CommonActionsReader.Read(Encoding.UTF8.GetBytes(json)).ShouldBeNull();
    }

    [Fact]
    public void A_missing_folder_gives_the_empty_catalogs()
    {
        var missing = Path.Combine(RepoPaths.Data, "no-such-catalogs-folder");

        RuntimeCatalogFiles.LoadKeyLabels(missing).ShouldBeSameAs(KeyLabelCatalog.Empty);
        RuntimeCatalogFiles.LoadCommonActions(missing).ShouldBeSameAs(CommonActionTable.Empty);
    }

    private static Shortcut Seed(string action, CommonActionTable table)
    {
        var standard = table
            .Actions.Single(a => string.Equals(a.Id, action, StringComparison.Ordinal))
            .Standard;
        return new Shortcut(
            new ShortcutId("s1"),
            LocalizedText.Same(action, LangCode.Es, LangCode.En),
            new Domain.Catalog.IconRef("bolt"),
            false,
            new Domain.Catalog.CategoryId("edit"),
            new TapAction(standard, []),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            new CatalogRef("seed", "1", action),
            null
        );
    }

    private static KeyChord Chord(string keys) =>
        KeyChord.FromKeys([.. keys.Split('+').Select(static k => new KeyId(k))]);
}
