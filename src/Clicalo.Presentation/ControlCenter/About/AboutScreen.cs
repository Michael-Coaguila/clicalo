using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>Everything «Acerca de y contacto» shows (docs/05 §6), projected at once.</summary>
/// <param name="Title">[aboutTitle].</param>
/// <param name="Subtitle">[aboutSub].</param>
/// <param name="Story1">[story1], at 18.</param>
/// <param name="Story2">[story2].</param>
/// <param name="CreatorName">[creatorName].</param>
/// <param name="CreatorInitials">[creatorInitials].</param>
/// <param name="CreatorRole">[creatorRole].</param>
/// <param name="AppName">[appName], drawn as the logo word.</param>
/// <param name="VersionLine">«v{version} · MIT · código abierto».</param>
/// <param name="GitHubText">[gitHub].</param>
/// <param name="ShareText">[shareShort].</param>
/// <param name="FeedbackTitle">[fbTitle].</param>
/// <param name="Kinds">The four kinds of feedback.</param>
/// <param name="MessageName">[fbMsg], the accessible name of the message.</param>
/// <param name="MessagePlaceholder">[fbPh].</param>
/// <param name="Message">What the person wrote.</param>
/// <param name="DictateName">[searchDictate], the name of 🎤.</param>
/// <param name="LogTitle">[fbLog].</param>
/// <param name="LogDescription">[fbLogFile].</param>
/// <param name="AttachLog">Whether the log is attached.</param>
/// <param name="SystemTitle">[fbSys].</param>
/// <param name="SystemDescription">[fbSysD].</param>
/// <param name="IncludeSystem">Whether the versions are included.</param>
/// <param name="PreviewTitle">[logPvT].</param>
/// <param name="PreviewOpen">Whether the preview of the log is open.</param>
/// <param name="PreviewText">The log exactly as it would be sent, or why there is none; empty while it loads.</param>
/// <param name="SendText">[fbSend].</param>
/// <param name="SendNote">[fbSendD], or [fbSendLogD] when the log is attached.</param>
/// <param name="Sending">Whether a send is under way ([Enviar por correo] waits).</param>
/// <param name="DirectTitle">[fbDirect].</param>
/// <param name="Email">The contact email, or the visible marker [contactPending].</param>
/// <param name="EmailPending">Whether there is no contact email yet (nothing to copy).</param>
/// <param name="CopyName">[copy], the name of the copy button.</param>
/// <param name="Promise">[fbPromise].</param>
/// <param name="IssuesTitle">[fbGh].</param>
/// <param name="IssuesDescription">[fbGhD].</param>
/// <param name="ContributeTitle">[fbContrib].</param>
/// <param name="ContributeDescription">[fbContribD].</param>
public sealed record AboutScreen(
    string Title,
    string Subtitle,
    string Story1,
    string Story2,
    string CreatorName,
    string CreatorInitials,
    string CreatorRole,
    string AppName,
    string VersionLine,
    string GitHubText,
    string ShareText,
    string FeedbackTitle,
    ValueList<AboutOption> Kinds,
    string MessageName,
    string MessagePlaceholder,
    string Message,
    string DictateName,
    string LogTitle,
    string LogDescription,
    bool AttachLog,
    string SystemTitle,
    string SystemDescription,
    bool IncludeSystem,
    string PreviewTitle,
    bool PreviewOpen,
    string PreviewText,
    string SendText,
    string SendNote,
    bool Sending,
    string DirectTitle,
    string Email,
    bool EmailPending,
    string CopyName,
    string Promise,
    string IssuesTitle,
    string IssuesDescription,
    string ContributeTitle,
    string ContributeDescription
);
