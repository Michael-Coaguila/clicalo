namespace Clicalo.Domain.Templates;

/// <summary>The final button of the preview of Plantillas (PLA-017).</summary>
public enum PreviewAction
{
    /// <summary>«Instalar N atajos»: a template or a shared profile that is not installed.</summary>
    Install,

    /// <summary>«[createWith] N atajos»: an AI proposal.</summary>
    CreateWith,

    /// <summary>«[addMissing]»: the template is installed and some of its shortcuts are missing.</summary>
    AddMissing,

    /// <summary>«[editShortcuts]» (secondary): the template is installed with all its shortcuts.</summary>
    EditShortcuts,
}
