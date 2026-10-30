using System.Buffers;
using System.Text;
using System.Text.Json;
using Clicalo.Application.Ports;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Timing;

namespace Clicalo.Infrastructure.Ai;

/// <summary>
/// The versioned prompt of a template generation (ADR-0014): a fixed instruction, the same for everybody, and a user
/// message with <b>exactly</b> the four values of <see cref="TemplateRequest"/> (PLA-008). Nothing else of the person
/// (documents, window titles, shortcuts) is ever added.
/// </summary>
internal static class AiPrompt
{
    /// <summary>The version of the instruction and of the answer contract.</summary>
    public const string Version = "ai-template.v1";

    /// <summary>The fixed instruction.</summary>
    public static string Instruction { get; } = BuildInstruction();

    /// <summary>The user message: a JSON object with the four values and nothing else.</summary>
    /// <param name="request">The request.</param>
    public static string UserMessage(TemplateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("app", request.AppName);
            writer.WriteString("layout", request.Layout);
            writer.WriteString("programsLang", request.ProgramsLang.Value);
            writer.WriteString("uiLang", request.UiLang.Value);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string BuildInstruction()
    {
        var keys = string.Join(", ", KeyDefinitions.All.Select(static d => d.Id.Value));
        return "You prepare keyboard shortcut templates for Clícalo, an accessibility app that presses key "
            + "combinations for people who cannot use a physical keyboard. The user message is a JSON object with "
            + "the program name (app), the keyboard layout (layout), the language of the person's programs "
            + "(programsLang) and the interface language (uiLang). Answer with one JSON object and nothing else, in "
            + "this exact shape (contract "
            + Version
            + "): {\"known\": true, \"app\": \"WhatsApp\", \"process\": \"WhatsApp.exe\", \"icon\": \"chat\", "
            + "\"buttons\": [{\"name\": {\"es\": \"Nuevo chat\", \"en\": \"New chat\"}, \"icon\": \"add\", "
            + "\"keys\": [\"ctrl\", \"n\"], \"cat\": \"file\", \"confidence\": 0.9}]}. Rules: propose the 6 to "
            + "12 most useful keyboard shortcuts of the program as it works in Windows with that layout and with the "
            + "program in programsLang (combinations change with the program language); only key combinations, "
            + "never text to type, web addresses or programs to open; at most "
            + Timings.Ai.AiMaxShortcuts
            + " buttons; every name at most "
            + Timings.Ai.AiNameMaxLength
            + " characters, in Spanish (es) and English (en); icons are Material Symbols names in lower case; "
            + "cat is one of edit, hist, file, sel, win, voice, nav, fmt, web, text; keys are, in press order, "
            + "only these ids: "
            + keys
            + ". If you do not know the program reliably, set known to false, leave process empty and propose "
            + "common Windows shortcuts that usually work. process is the executable name without path.";
    }
}
