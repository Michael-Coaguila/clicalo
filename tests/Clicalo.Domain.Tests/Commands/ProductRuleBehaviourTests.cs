using System.Collections.Concurrent;
using System.Text.Json;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Library;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using Clicalo.TestKit;
using CsCheck;

namespace Clicalo.Domain.Tests.Commands;

/// <summary>
/// The behavioural side of the product rules R4 and R7 (blueprint §4.4, mechanism 6; docs/architecture/enforcement.md):
/// every document command of the Domain is applied to 10 000 generated documents. R7: every command records an undo
/// step except the exemptions of <c>architecture/undo-exemptions.json</c>. R4: only the closed list of
/// <c>architecture/destructive-operations.json</c> removes profiles or shortcuts (a blank draft is not a user entity,
/// ATJ-011), and every listed command exists. The structural side stays in Architecture.Tests (ProductRuleTests).
/// </summary>
public sealed class ProductRuleBehaviourTests
{
    private const int Iterations = 10_000;

    private static readonly Type[] DomainCommands =
    [
        .. typeof(IDocumentCommand)
            .Assembly.GetTypes()
            .Where(t =>
                typeof(IDocumentCommand).IsAssignableFrom(t)
                && t is { IsInterface: false, IsAbstract: false }
            ),
    ];

    [Fact]
    [Trait("Req", "REG-04")]
    public void Every_listed_destructive_command_exists_and_needs_a_confirmation()
    {
        var listed = Names("destructive-operations.json", "commands", "name");

        listed.ShouldNotBeEmpty();
        foreach (var name in listed)
        {
            var type = DomainCommands.SingleOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.Ordinal)
            );
            type.ShouldNotBeNull(name);
            typeof(IDestructiveCommand).IsAssignableFrom(type).ShouldBeTrue(name);
        }

        DomainCommands
            .Where(typeof(IDestructiveCommand).IsAssignableFrom)
            .Select(t => t.Name)
            .ShouldBe(listed, ignoreOrder: true);
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void The_generated_commands_cover_every_document_command_of_the_domain() =>
        CommandFactory.Kinds.ShouldBe(DomainCommands, ignoreOrder: true);

    [Fact]
    [Trait("Req", "REG-07")]
    public void Every_generated_kind_succeeds_on_some_document()
    {
        var succeeded = new ConcurrentDictionary<Type, bool>();
        Gen.Select(DomainGen.Document, CommandFactory.Seed)
            .Sample(
                (document, seed) =>
                {
                    var command = CommandFactory.Create(seed, document);
                    if (command.Apply(document, Contexts.Fresh()).IsSuccess)
                    {
                        succeeded[command.GetType()] = true;
                    }
                },
                iter: Iterations
            );

        // Without this the rules below could pass on commands that always fail.
        succeeded.Keys.ShouldBe(CommandFactory.Kinds, ignoreOrder: true);
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void Every_command_records_an_undo_step_except_the_justified_exemptions()
    {
        var exemptions = Exemptions();
        Gen.Select(DomainGen.Document, CommandFactory.Seed)
            .Sample(
                (document, seed) =>
                {
                    var command = CommandFactory.Create(seed, document);
                    var result = command.Apply(document, Contexts.Fresh());
                    if (!result.TryGetValue(out var change))
                    {
                        return;
                    }

                    var name = command.GetType().Name;
                    var records = change.Undo is UndoIntent.Record;
                    switch (exemptions.GetValueOrDefault(name))
                    {
                        case "always":
                            records.ShouldBeFalse(name);
                            break;
                        case "descriptorNotUndoable":
                            var setting = (SetSetting)command;
                            records.ShouldBe(
                                SettingsSchema.Find(setting.Path)!.Undoable,
                                setting.Path
                            );
                            break;
                        default:
                            records.ShouldBeTrue(name);
                            break;
                    }
                },
                iter: Iterations
            );
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public void Only_the_listed_destructive_commands_remove_user_entities() =>
        Gen.Select(DomainGen.Document, CommandFactory.Seed)
            .Sample(
                (document, seed) =>
                {
                    var command = CommandFactory.Create(seed, document);
                    var result = command.Apply(document, Contexts.Fresh());
                    if (!result.TryGetValue(out var change) || command is IDestructiveCommand)
                    {
                        return;
                    }

                    var name = command.GetType().Name;
                    foreach (var profile in document.Library.Profiles)
                    {
                        change.Next.Library.TryGetProfile(profile.Id, out _).ShouldBeTrue(name);
                    }

                    foreach (var located in document.Library.EnumerateShortcuts())
                    {
                        var kept = change.Next.Library.TryLocate(located.Shortcut.Id, out _);
                        var blankDraft =
                            command is DiscardDraft
                            && ShortcutCompleteness.IsBlankDraft(located.Shortcut);
                        (kept || blankDraft).ShouldBeTrue(
                            name + " removed " + located.Shortcut.Id.Value
                        );
                    }
                },
                iter: Iterations
            );

    private static Dictionary<string, string> Exemptions()
    {
        using var json = Read("undo-exemptions.json");
        return json
            .RootElement.GetProperty("exemptions")
            .EnumerateArray()
            .ToDictionary(
                e => e.GetProperty("command").GetString()!,
                e => e.GetProperty("scope").GetString()!,
                StringComparer.Ordinal
            );
    }

    private static string[] Names(string file, string list, string property)
    {
        using var json = Read(file);
        return
        [
            .. json
                .RootElement.GetProperty(list)
                .EnumerateArray()
                .Select(e => e.GetProperty(property).GetString()!),
        ];
    }

    private static JsonDocument Read(string file) =>
        JsonDocument.Parse(File.ReadAllText(RepoPaths.Combine("architecture", file)));
}
