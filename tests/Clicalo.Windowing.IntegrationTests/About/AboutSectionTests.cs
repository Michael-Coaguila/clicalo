using System.IO;
using System.Windows;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.About;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace.About;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.About;

/// <summary>
/// «Acerca de y contacto» (docs/05 §6) with fake ports: what each tap opens, copies, saves and says (ACE-001 to
/// ACE-005), and the view built headless. With <c>CLICALO_CC_PREVIEW=1</c> it also writes PNG previews to
/// <c>artifacts/cc-preview</c>.
/// </summary>
public sealed class AboutSectionTests
{
    /// <summary>A contact address for the tests: the real one does not exist yet (D11).</summary>
    private const string Mail = "contacto@clicalo.example";

    private const string Log =
        "2026-10-09 09:12:03 INF app.started\n2026-10-09 09:15:02 INF text.sent [oculto · 23 caracteres]";

    [Fact]
    [Trait("Req", "ACE-001")]
    [Trait("Req", "ACE-005")]
    public void The_cards_show_the_story_the_real_version_and_the_links()
    {
        var world = new World();
        var screen = new AboutViewModel(world.Services).Screen;

        screen.Title.ShouldBe("Acerca de y contacto");
        screen.CreatorName.ShouldBe("Michael Coaguila");
        screen.CreatorRole.ShouldBe("Creador de Clícalo");
        screen.VersionLine.ShouldBe("v2.0.0 · MIT · código abierto");
        screen
            .Kinds.Select(k => k.Label)
            .ShouldBe(["Sugerencia", "Algo falla", "Nueva función", "Agradecimiento"]);
        screen.Kinds[0].Selected.ShouldBeTrue("Sugerencia is the default");
        screen.AttachLog.ShouldBeTrue();
        screen.IncludeSystem.ShouldBeTrue();
        AboutLinks.Current.Repository.AbsoluteUri.ShouldBe(
            "https://github.com/Michael-Coaguila/clicalo"
        );
        screen.GuideTitle.ShouldBe("Guía de usuario");
        AboutLinks.Current.Guide.AbsoluteUri.ShouldBe(
            "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/guia-de-usuario.md"
        );
    }

    [Fact]
    [Trait("Req", "ACE-001")]
    [Trait("Req", "ACE-005")]
    public void The_email_and_LinkedIn_are_hidden_while_they_do_not_exist()
    {
        // User decision D11: nothing invented is ever shown in their place.
        AboutLinks.Current.LinkedIn.ShouldBeNull("there is no real LinkedIn address yet");
        AboutLinks.Current.Email.ShouldBeNull("there is no contact email yet");
        var hidden = new AboutViewModel(new World().Services).Screen;
        hidden.Email.ShouldBeNull();
        hidden.LinkedInText.ShouldBeNull();
        hidden.SendNote.ShouldStartWith("Aún no hay un correo de contacto");

        var shown = new AboutViewModel(
            new World(
                AboutLinks.Current with
                {
                    Email = Mail,
                    LinkedIn = new Uri("https://www.linkedin.com/in/ejemplo"),
                }
            ).Services
        ).Screen;
        shown.Email.ShouldBe(Mail);
        shown.LinkedInText.ShouldBe("LinkedIn");
        shown.SendNote.ShouldStartWith("Se abrirá tu app de correo");
    }

    [Fact]
    [Trait("Req", "ACE-001")]
    [Trait("Req", "ACE-005")]
    public void The_view_only_builds_the_buttons_that_have_an_address()
    {
        static List<string> Ids(World world)
        {
            var (view, theme, _) = Build(world);
            try
            {
                return WpfThread.Invoke(() =>
                {
                    view.Measure(new Size(1300, 2000));
                    view.Arrange(new Rect(0, 0, 1300, 2000));
                    view.UpdateLayout();
                    var ids = new List<string>();
                    Collect(view, ids);
                    return ids;
                });
            }
            finally
            {
                Close(view, theme);
            }
        }

        var without = Ids(new World());
        without.ShouldContain("about.github", StringComparer.Ordinal);
        without.ShouldContain("about.guide", StringComparer.Ordinal);
        without.ShouldNotContain("about.linkedin", StringComparer.Ordinal);
        without.ShouldNotContain("about.copyEmail", StringComparer.Ordinal);

        var with = Ids(
            new World(
                AboutLinks.Current with
                {
                    Email = Mail,
                    LinkedIn = new Uri("https://www.linkedin.com/in/ejemplo"),
                }
            )
        );
        with.ShouldContain("about.linkedin", StringComparer.Ordinal);
        with.ShouldContain("about.copyEmail", StringComparer.Ordinal);
    }

    private static void Collect(DependencyObject root, List<string> ids)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            var id = System.Windows.Automation.AutomationProperties.GetAutomationId(child);
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id);
            }

            Collect(child, ids);
        }
    }

    [Fact]
    [Trait("Req", "ACE-001")]
    [Trait("Req", "ACE-005")]
    public async Task The_links_open_the_repository_and_share_copies_it()
    {
        var world = new World();
        var viewModel = new AboutViewModel(world.Services);

        await viewModel.OpenRepositoryAsync();
        await viewModel.OpenIssuesAsync();
        await viewModel.OpenGuideAsync();
        await viewModel.OpenLinkedInAsync();
        viewModel.Share();
        viewModel.CopyEmail();

        world
            .Opened.Select(u => u.AbsoluteUri)
            .ShouldBe([
                "https://github.com/Michael-Coaguila/clicalo",
                "https://github.com/Michael-Coaguila/clicalo/issues",
                "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/guia-de-usuario.md",
            ]);
        world.Copied.ShouldBe(
            ["https://github.com/Michael-Coaguila/clicalo"],
            "without an email there is nothing to copy"
        );
        world.Notices.ShouldHaveSingleItem().Text.ShouldBe(Clicalo.Domain.Messages.L.Shared);
    }

    [Fact]
    [Trait("Req", "ACE-003")]
    public async Task The_preview_shows_the_log_exactly_as_it_would_be_attached()
    {
        var world = new World();
        var viewModel = new AboutViewModel(world.Services);

        await viewModel.TogglePreviewAsync();

        viewModel.Screen.PreviewOpen.ShouldBeTrue();
        viewModel.Screen.PreviewText.ShouldBe(Log);
        world.Log = string.Empty;
        await viewModel.TogglePreviewAsync();
        await viewModel.TogglePreviewAsync();
        viewModel.Screen.PreviewText.ShouldBe("El registro está vacío.");
        world.Log = null;
        await viewModel.TogglePreviewAsync();
        await viewModel.TogglePreviewAsync();
        viewModel.Screen.PreviewText.ShouldBe("No se pudo preparar el registro.");
    }

    [Fact]
    [Trait("Req", "ACE-002")]
    [Trait("Req", "ACE-004")]
    public async Task Send_saves_the_log_and_opens_the_email_with_subject_and_body()
    {
        var world = new World(AboutLinks.Current with { Email = Mail });
        var viewModel = new AboutViewModel(world.Services);
        viewModel.SetKind(FeedbackKind.Bug);
        viewModel.SetMessage("El botón Copiar no responde");

        await viewModel.SendAsync();

        world.Saved.ShouldBe([Log]);
        var mail = world.Opened.ShouldHaveSingleItem();
        mail.Scheme.ShouldBe("mailto");
        mail.OriginalString.ShouldStartWith("mailto:" + Mail + "?subject=");
        Clicalo
            .Domain.Execution.LaunchSafety.CheckMail(mail, Mail)
            .ShouldBe(Clicalo.Domain.Execution.LaunchVerdict.Allowed);
        var query = Uri.UnescapeDataString(mail.Query);
        query.ShouldContain("subject=[Clícalo] Algo falla");
        query.ShouldContain(
            "El botón Copiar no responde\r\n\r\n—\r\nClícalo v2.0.0 · Windows 11 (26300)"
        );
        query.ShouldContain("Registro para adjuntar: clicalo-registro.log");
        world.Notices.ShouldHaveSingleItem().Text.ShouldBe(Clicalo.Domain.Messages.L.SentMail);
        viewModel.Screen.Sending.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "ACE-003")]
    [Trait("Req", "ACE-004")]
    public async Task Without_log_and_system_the_body_is_the_message_alone()
    {
        var world = new World(AboutLinks.Current with { Email = Mail });
        var viewModel = new AboutViewModel(world.Services);
        viewModel.ToggleLog();
        viewModel.ToggleSystem();
        viewModel.SetMessage("Gracias");

        await viewModel.SendAsync();

        world.Saved.ShouldBeEmpty();
        Uri.UnescapeDataString(world.Opened.Single().Query).ShouldEndWith("body=Gracias");
        viewModel.Screen.SendNote.ShouldBe("Se abrirá tu app de correo con el mensaje listo.");
    }

    [Fact]
    [Trait("Req", "ACE-004")]
    public async Task Without_an_email_app_the_message_is_copied()
    {
        var world = new World(AboutLinks.Current with { Email = Mail }) { CanOpen = false };
        var viewModel = new AboutViewModel(world.Services);
        viewModel.ToggleLog();
        viewModel.SetMessage("Idea");

        await viewModel.SendAsync();

        world.Copied.ShouldHaveSingleItem().ShouldStartWith("[Clícalo] Sugerencia\n\nIdea");
        world.Notices.ShouldHaveSingleItem().Text.ShouldBe(Clicalo.Domain.Messages.L.FbMailCopied);
    }

    [Fact]
    [Trait("Req", "ACE-004")]
    public async Task Without_a_contact_email_nothing_opens_and_the_message_is_copied()
    {
        // User decision D11 and ADR-0029: the email app only ever opens towards the address of the project.
        var world = new World();
        var viewModel = new AboutViewModel(world.Services);
        viewModel.ToggleLog();
        viewModel.SetMessage("Idea");

        await viewModel.SendAsync();

        world.Opened.ShouldBeEmpty();
        world.Copied.ShouldHaveSingleItem().ShouldStartWith("[Clícalo] Sugerencia\n\nIdea");
        world.Notices.ShouldHaveSingleItem().Text.ShouldBe(Clicalo.Domain.Messages.L.FbMailCopied);
    }

    [Fact]
    [Trait("Req", "ACE-001")]
    [Trait("Req", "ACE-002")]
    public void The_view_is_built_and_keeps_the_field_while_typing()
    {
        var world = new World();
        var (view, theme, viewModel) = Build(world);
        try
        {
            WpfThread.Invoke(() =>
            {
                var before = ((FrameworkElement)view.Child).GetValue(
                    System.Windows.Controls.ContentControl.ContentProperty
                );
                viewModel.SetMessage("Hola");
                ((FrameworkElement)view.Child)
                    .GetValue(System.Windows.Controls.ContentControl.ContentProperty)
                    .ShouldBeSameAs(before, "typing does not rebuild the section");
                viewModel.SetKind(FeedbackKind.Thanks);
                ((FrameworkElement)view.Child)
                    .GetValue(System.Windows.Controls.ContentControl.ContentProperty)
                    .ShouldNotBeSameAs(before);
            });
        }
        finally
        {
            Close(view, theme);
        }
    }

    [Fact]
    public void Previews_are_written_when_asked()
    {
        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("CLICALO_CC_PREVIEW"),
                "1",
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        var world = new World();
        var (view, theme, viewModel) = Build(world);
        try
        {
            Preview(view, theme, "about-narrow", 900, 1500);
            WpfThread.Invoke(() =>
            {
                _ = viewModel.TogglePreviewAsync();
            });
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            Preview(view, theme, "about-wide", 1180, 1300);
        }
        finally
        {
            Close(view, theme);
        }
    }

    private static (AboutSectionView View, ThemeService Theme, AboutViewModel ViewModel) Build(
        World world
    ) =>
        WpfThread.Invoke(() =>
        {
            var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            var viewModel = new AboutViewModel(world.Services);
            var view = new AboutSectionView(viewModel);
            theme.Attach(view);
            WpfThread.DrainPendingWork();
            return (view, theme, viewModel);
        });

    private static void Preview(
        AboutSectionView view,
        ThemeService theme,
        string name,
        double width,
        double height
    )
    {
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        var png = RenderSnapshot.Render(
            () =>
            {
                var host = new System.Windows.Controls.Border();
                theme.Attach(host);
                host.SetResourceReference(
                    System.Windows.Controls.Border.BackgroundProperty,
                    Clicalo.UI.Wpf.Controls.ThemeBrushKey.For(
                        Clicalo.UI.Wpf.Theming.Generated.ColorToken.Win
                    )
                );
                theme.Detach(view);
                host.Child = view;
                return host;
            },
            new RenderSnapshotOptions { Width = width, Height = height }
        );
        WpfThread.Invoke(() =>
        {
            if (view.Parent is System.Windows.Controls.Border host)
            {
                host.Child = null;
                theme.Detach(host);
            }

            theme.Attach(view);
        });
        var folder = Path.Combine(RepoPaths.Root, "artifacts", "cc-preview");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
    }

    private static void Close(AboutSectionView view, ThemeService theme) =>
        WpfThread.Invoke(() =>
        {
            view.Detach();
            theme.Dispose();
        });

    internal sealed class World
    {
        public World(AboutLinks? links = null) =>
            Services = new AboutServices(
                PanelTestData.Localization("es"),
                "2.0.0",
                "Windows 11 (26300)",
                links ?? AboutLinks.Current,
                (uri, _) =>
                {
                    Opened.Add(uri);
                    return ValueTask.FromResult(CanOpen);
                },
                Copied.Add,
                _ => ValueTask.FromResult(true),
                _ => ValueTask.FromResult<string?>(Log),
                (text, _) =>
                {
                    Saved.Add(text);
                    return ValueTask.FromResult<string?>("clicalo-registro.log");
                },
                Notices.Add
            );

        public AboutServices Services { get; }

        public string? Log { get; set; } = AboutSectionTests.Log;

        public bool CanOpen { get; set; } = true;

        public List<Uri> Opened { get; } = [];

        public List<string> Copied { get; } = [];

        public List<string> Saved { get; } = [];

        public List<WorkspaceNotice> Notices { get; } = [];
    }
}
