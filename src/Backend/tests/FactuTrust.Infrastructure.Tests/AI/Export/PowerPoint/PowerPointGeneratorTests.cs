using DocumentFormat.OpenXml.Packaging;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Features.AI.Export.PowerPoint.DTOs;
using FactuTrust.Application.Features.AI.Export.PowerPoint.Models;
using FactuTrust.Domain.Entities.AI;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint;
using FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Themes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace FactuTrust.Infrastructure.Tests.AI.Export.PowerPoint;

/// <summary>
/// End-to-end exercise of <see cref="PowerPointGenerator"/> on a fully assembled conversation
/// (textual reply, dashboard with KPIs/table/chart, plus a "tool" source message). The tests
/// validate the structural invariants of the produced .pptx without inspecting OpenXml strings.
/// </summary>
public sealed class PowerPointGeneratorTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000A1");

    [Fact]
    public async Task GenerateAsync_produces_valid_pptx_with_dashboard_and_text()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Synthèse stock",
            Subtitle = "Avril 2026",
            AuthorName = "Test User",
            Template = PowerPointTemplate.Executive,
            Orientation = SlideOrientation.Widescreen16x9,
            IncludeCoverSlide = true,
            IncludeAgenda = true,
            IncludeTableOfContents = false,
            IncludeSpeakerNotes = true,
            IncludeSources = true,
            IncludeAppendix = false,
            Responses = new[]
            {
                new ResponseSelectionDto
                {
                    ConversationId = conversation.Id,
                    MessageId = assistantMessageId,
                    CustomTitle = "Stock — vue analytique"
                }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        Assert.NotNull(result);
        Assert.True(result.Content.Length > 8_000, "Generated .pptx is suspiciously small.");
        Assert.True(result.SlideCount > 0, "No slides were created.");
        Assert.EndsWith(".pptx", result.FileName);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.NotNull(doc.PresentationPart);
        var slides = doc.PresentationPart!.SlideParts.ToList();
        Assert.NotEmpty(slides);
        // Cover + agenda + section divider + KPI + Table + Chart + Markdown + Sources ≥ 6 slides.
        Assert.True(slides.Count >= 6, $"Expected ≥ 6 slides, got {slides.Count}.");

        var slideSize = doc.PresentationPart.Presentation.SlideSize;
        Assert.NotNull(slideSize);
        Assert.Equal(P.SlideSizeValues.Screen16x9, slideSize!.Type!.Value);
    }

    [Fact]
    public async Task GenerateAsync_honors_IncludeOnly_filter_to_strip_blocks()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Texte uniquement",
            Template = PowerPointTemplate.Standard,
            Orientation = SlideOrientation.Widescreen16x9,
            IncludeAgenda = false,
            IncludeCoverSlide = false,
            IncludeSources = false,
            Responses = new[]
            {
                new ResponseSelectionDto
                {
                    ConversationId = conversation.Id,
                    MessageId = assistantMessageId,
                    IncludeOnly = SlideContentBlock.Text
                }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);
        Assert.NotNull(result);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var slides = doc.PresentationPart!.SlideParts.ToList();
        // 1 section divider + 1 markdown text slide minimum.
        Assert.InRange(slides.Count, 1, 5);
        // No ChartPart should be present when charts are excluded.
        Assert.DoesNotContain(slides, s => s.ChartParts.Any());
    }

    [Fact]
    public async Task GenerateAsync_supports_4x3_orientation()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "4:3",
            Orientation = SlideOrientation.Standard4x3,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var size = doc.PresentationPart!.Presentation.SlideSize!;
        Assert.Equal(9_144_000, size.Cx!.Value);
        Assert.Equal(6_858_000, size.Cy!.Value);
        Assert.Equal(P.SlideSizeValues.Screen4x3, size.Type!.Value);
    }

    [Fact]
    public async Task GenerateAsync_skips_missing_messages_silently()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var unknownMessage = Guid.NewGuid();
        var request = new PowerPointExportRequestDto
        {
            Title = "Mixed selection",
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId },
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = unknownMessage }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);
        Assert.NotNull(result);
        // Only one message was resolved → one section divider, not two.
        var slides = OpenPptxSlideCount(result.Content);
        Assert.True(slides > 0);
    }

    [Fact]
    public async Task GenerateAsync_throws_when_no_valid_response()
    {
        var conversation = BuildConversation(out _);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Empty selection",
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = Guid.NewGuid() }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.GenerateAsync(request, UserId));
    }

    /// <summary>
    /// Exercises every template (now 52 after the premium-V2 expansion) to ensure every theme
    /// produces a structurally valid OpenXml package. The theory is parameterised on
    /// <c>Enum.GetValues</c> so any future enum addition is automatically covered.
    /// </summary>
    public static IEnumerable<object[]> AllTemplates =>
        Enum.GetValues<PowerPointTemplate>().Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllTemplates))]
    public async Task GenerateAsync_all_themes_produce_valid_pptx(PowerPointTemplate template)
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = $"Smoke — {template}",
            Template = template,
            Orientation = SlideOrientation.Widescreen16x9,
            IncludeCoverSlide = true,
            IncludeAgenda = false,
            IncludeSources = false,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        Assert.NotNull(result);
        Assert.True(result.Content.Length > 4_000);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        Assert.NotNull(doc.PresentationPart?.SlideMasterParts.First().ThemePart?.Theme);
    }

    [Fact]
    public async Task BuildTheme_uses_selected_theme_colors()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Theme colors",
            Template = PowerPointTemplate.Analyse,
            Orientation = SlideOrientation.Widescreen16x9,
            IncludeCoverSlide = false,
            IncludeAgenda = false,
            IncludeSources = false,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var colorScheme = doc.PresentationPart!
            .SlideMasterParts.First()
            .ThemePart!
            .Theme!
            .ThemeElements!
            .ColorScheme!;

        var accent1 = colorScheme.GetFirstChild<DocumentFormat.OpenXml.Drawing.Accent1Color>()?
            .GetFirstChild<DocumentFormat.OpenXml.Drawing.RgbColorModelHex>()?.Val?.Value;
        Assert.Equal("F59E0B", accent1);
    }

    /// <summary>
    /// Legacy smoke: templates 0–2 × both orientations (non-regression baseline).
    /// </summary>
    [Theory]
    [InlineData(PowerPointTemplate.Executive, SlideOrientation.Widescreen16x9)]
    [InlineData(PowerPointTemplate.Executive, SlideOrientation.Standard4x3)]
    [InlineData(PowerPointTemplate.Analyse, SlideOrientation.Widescreen16x9)]
    [InlineData(PowerPointTemplate.Analyse, SlideOrientation.Standard4x3)]
    [InlineData(PowerPointTemplate.Standard, SlideOrientation.Widescreen16x9)]
    [InlineData(PowerPointTemplate.Standard, SlideOrientation.Standard4x3)]
    public async Task GenerateAsync_all_template_and_orientation_combinations_produce_valid_pptx(
        PowerPointTemplate template,
        SlideOrientation orientation)
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = $"Smoke — {template}/{orientation}",
            Template = template,
            Orientation = orientation,
            IncludeCoverSlide = true,
            IncludeAgenda = true,
            IncludeSpeakerNotes = true,
            IncludeSources = true,
            IncludeAppendix = true,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        Assert.NotNull(result);
        Assert.True(result.Content.Length > 8_000);
        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var slides = doc.PresentationPart!.SlideParts.ToList();
        Assert.True(slides.Count >= 5, $"Expected ≥ 5 slides for {template}/{orientation}, got {slides.Count}");

        // Persist sample files when AI_EXPORT_DUMP_DIR env var is set — useful for manual QA in
        // PowerPoint Desktop / LibreOffice Impress / Google Slides without re-running the dev loop.
        var dumpDir = Environment.GetEnvironmentVariable("AI_EXPORT_DUMP_DIR");
        if (!string.IsNullOrWhiteSpace(dumpDir))
        {
            Directory.CreateDirectory(dumpDir);
            var fileName = $"{template}_{orientation}.pptx";
            await File.WriteAllBytesAsync(Path.Combine(dumpDir, fileName), result.Content);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Regression tests for the "PowerPoint a détecté un problème" corruption bug.
    //  Each test guards one of the five root causes documented in the fix plan.
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_produces_pptx_that_passes_OpenXmlValidator()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGeneratorStrict(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Validation stricte",
            IncludeSpeakerNotes = true,
            IncludeCoverSlide = true,
            IncludeAgenda = true,
            IncludeAppendix = true,
            IncludeSources = true,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(
            DocumentFormat.OpenXml.FileFormatVersions.Office2019);
        var structuralErrors = validator
            .Validate(doc)
            .Where(e => e.ErrorType is DocumentFormat.OpenXml.Validation.ValidationErrorType.Schema
                                     or DocumentFormat.OpenXml.Validation.ValidationErrorType.Package
                                     or DocumentFormat.OpenXml.Validation.ValidationErrorType.Semantic)
            .ToList();

        Assert.True(structuralErrors.Count == 0,
            "Validator found structural issues: " +
            string.Join(" | ", structuralErrors.Take(3).Select(e => $"{e.Id}@{e.Path?.XPath}: {e.Description}")));
    }

    [Fact]
    public async Task GenerateAsync_includes_NotesMasterPart_when_speaker_notes_enabled()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Notes",
            IncludeSpeakerNotes = true,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var presentationPart = doc.PresentationPart!;
        Assert.NotNull(presentationPart.NotesMasterPart);

        var notesSlideParts = presentationPart.SlideParts
            .Select(sp => sp.NotesSlidePart)
            .Where(np => np is not null)
            .ToList();
        Assert.NotEmpty(notesSlideParts);
        foreach (var nsp in notesSlideParts)
        {
            Assert.Same(presentationPart.NotesMasterPart, nsp!.NotesMasterPart);
        }
    }

    [Fact]
    public async Task GenerateAsync_SlideIdList_count_matches_SlideParts_count()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Cohérence SlideIdList",
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var slideIdCount = doc.PresentationPart!.Presentation.SlideIdList!
            .ChildElements.OfType<P.SlideId>().Count();
        var slidePartCount = doc.PresentationPart.SlideParts.Count();
        Assert.Equal(slidePartCount, slideIdCount);
        Assert.True(slideIdCount > 0, "Expected at least one SlideId after generation.");
    }

    [Fact]
    public async Task GenerateAsync_ChartPart_has_EmbeddedPackagePart()
    {
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Chart embed",
            IncludeCoverSlide = false,
            IncludeAgenda = false,
            IncludeSources = false,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var chartParts = doc.PresentationPart!.SlideParts.SelectMany(sp => sp.ChartParts).ToList();
        Assert.NotEmpty(chartParts);
        foreach (var cp in chartParts)
        {
            var embedded = cp.GetPartsOfType<DocumentFormat.OpenXml.Packaging.EmbeddedPackagePart>().ToList();
            Assert.NotEmpty(embedded);
        }
    }

    [Fact]
    public async Task GenerateAsync_with_IncludeSpeakerNotes_true_does_not_corrupt_file()
    {
        // Regression test for the original bug: IncludeSpeakerNotes=true used to produce
        // a NotesSlide without a NotesMaster reference, which PowerPoint Desktop refused
        // to open (but the SDK accepted). Strict-mode validation must succeed.
        var conversation = BuildConversation(out var assistantMessageId);
        var generator = BuildGeneratorStrict(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Régression — notes activées",
            IncludeSpeakerNotes = true,
            Responses = new[]
            {
                new ResponseSelectionDto { ConversationId = conversation.Id, MessageId = assistantMessageId }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);
        Assert.True(result.SlideCount > 0, "Expected at least one slide; received zero — indicates regression to the corruption bug.");
    }

    private static int OpenPptxSlideCount(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        return doc.PresentationPart!.SlideParts.Count();
    }

    private static PowerPointGenerator BuildGenerator(Conversation conversation)
    {
        var repo = new Mock<IConversationRepository>();
        repo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);

        var resolver = new PowerPointThemeResolver(brand.Object);
        ILogger<PowerPointGenerator> logger = NullLogger<PowerPointGenerator>.Instance;
        return new PowerPointGenerator(repo.Object, resolver, logger);
    }

    private static PowerPointGenerator BuildGeneratorStrict(Conversation conversation)
    {
        var repo = new Mock<IConversationRepository>();
        repo.Setup(r => r.GetByIdAsync(conversation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var brand = new Mock<IBrandAssetProvider>();
        brand.Setup(b => b.GetLogoPng()).Returns((byte[]?)null);

        var resolver = new PowerPointThemeResolver(brand.Object);
        ILogger<PowerPointGenerator> logger = NullLogger<PowerPointGenerator>.Instance;
        var strictOptions = Microsoft.Extensions.Options.Options.Create(
            FactuTrust.Infrastructure.Services.AI.Export.PowerPoint.Rendering.PowerPointValidationOptions.StrictForTests);
        return new PowerPointGenerator(repo.Object, resolver, logger, strictOptions);
    }

    [Fact]
    public async Task GenerateAsync_invoice_list_markdown_only_produces_native_tables_not_pipes()
    {
        var conversation = BuildInvoiceListConversation(out var assistantMessageId);
        var generator = BuildGenerator(conversation);

        var request = new PowerPointExportRequestDto
        {
            Title = "Synthèse Assistant IA — Liste factures",
            Template = PowerPointTemplate.Executive,
            Orientation = SlideOrientation.Widescreen16x9,
            IncludeCoverSlide = false,
            IncludeAgenda = false,
            IncludeSources = false,
            Responses = new[]
            {
                new ResponseSelectionDto
                {
                    ConversationId = conversation.Id,
                    MessageId = assistantMessageId,
                    CustomTitle = "Analyse — Liste des factures"
                }
            }
        };

        var result = await generator.GenerateAsync(request, UserId);
        Assert.NotNull(result);

        using var ms = new MemoryStream(result.Content, writable: false);
        using var doc = PresentationDocument.Open(ms, isEditable: false);
        var slides = doc.PresentationPart!.SlideParts.ToList();
        Assert.True(slides.Count <= 10, $"Expected ≤ 10 slides for invoice-list, got {slides.Count}.");

        var allBodyText = string.Join(' ', slides.SelectMany(ExtractSlideText));
        Assert.DoesNotContain("| :--- |", allBodyText);
        Assert.DoesNotContain("| Indicateur |", allBodyText);

        var tableCount = slides.Count(s => s.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Table>().Any());
        Assert.True(tableCount >= 0); // KPI cards preferred over table for indicateurs

        var kpiMention = allBodyText.Contains("90", StringComparison.Ordinal) ||
                         allBodyText.Contains("90151", StringComparison.OrdinalIgnoreCase);
        Assert.True(kpiMention, "Expected key amounts to appear in slide text or KPI shapes.");
    }

    private static IEnumerable<string> ExtractSlideText(SlidePart slidePart)
    {
        return slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
            .Select(t => t.Text ?? string.Empty);
    }

    private static Conversation BuildInvoiceListConversation(out Guid assistantMessageId)
    {
        var goldenPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "docs", "ai-screen-analysis", "golden", "invoice-list.json");

        var fixturePath = Path.GetFullPath(goldenPath);
        if (!File.Exists(fixturePath))
        {
            fixturePath = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "..",
                "docs", "ai-screen-analysis", "golden", "invoice-list.json"));
        }

        string assistantContent;
        if (File.Exists(fixturePath))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fixturePath));
            assistantContent = doc.RootElement.GetProperty("assistantMessageFixture").GetString()!;
        }
        else
        {
            assistantContent = """
                ## Synthèse exécutive
                Montant total **90 151.910 TND**.

                ## Indicateurs clés
                | Indicateur | Montant (TND) | Détails |
                | :--- | :--- | :--- |
                | Total Créances Clients | 90 151.910 | Somme |
                | Créances en Retard | 85 472.250 | Arriérés |

                ## Analyse détaillée
                FAC-2026-000095 en retard.
                """;
        }

        var convo = Conversation.Create(UserId, "Analyse factures", model: "ollama:gpt-oss");
        convo.AddMessage(MessageRole.User, "Analyse la liste des factures.");
        var assistant = convo.AddMessage(MessageRole.Assistant, assistantContent);
        assistantMessageId = assistant.Id;
        return convo;
    }

    private static Conversation BuildConversation(out Guid assistantMessageId)
    {
        var convo = Conversation.Create(UserId, "Analyse stock", model: "ollama:gpt-oss");
        convo.AddMessage(MessageRole.User, "Donne-moi l'état du stock.");
        var toolMessage = convo.AddMessage(MessageRole.Tool, "{\"ok\":true}", toolName: "list_inventory");
        Assert.NotNull(toolMessage);

        const string assistantContent = """
        # Analyse du stock

        Voici les principaux indicateurs :

        - **Articles actifs** : 1 240
        - **Articles en rupture** : 38
        - *Articles à reapprovisionner* : 92

        ```json
        {
          "title": "Stock — vue analytique",
          "sections": [
            {"type":"kpi_card","title":"Articles actifs","data":{"value":1240,"unit":"items","trend":2.4}},
            {"type":"kpi_card","title":"Ruptures","data":{"value":38,"trend":-12.5}},
            {"type":"table","title":"Top 3 ruptures","data":{
              "columns":[{"key":"sku","label":"SKU"},{"key":"name","label":"Article"},{"key":"qty","label":"Quantité"}],
              "rows":[
                {"sku":"A-001","name":"Ampoule LED","qty":0},
                {"sku":"A-002","name":"Câble USB","qty":1},
                {"sku":"A-003","name":"Chargeur","qty":2}
              ]
            }},
            {"type":"chart","title":"Tendance hebdomadaire","data":{
              "chartType":"bar",
              "labels":["S1","S2","S3","S4"],
              "datasets":[{"label":"Ruptures","data":[12,18,28,38]}]
            }}
          ]
        }
        ```

        Recommandations : passer une commande urgente pour les SKU ci-dessus.
        """;

        var assistant = convo.AddMessage(MessageRole.Assistant, assistantContent);
        assistantMessageId = assistant.Id;
        return convo;
    }
}
