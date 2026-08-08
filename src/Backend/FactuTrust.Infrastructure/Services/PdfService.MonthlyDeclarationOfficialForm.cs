using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.OfficialForm;
using FactuTrust.Infrastructure.Services.OfficialForms;

namespace FactuTrust.Infrastructure.Services;

public partial class PdfService
{
    /// <summary>
    /// Millésime du formulaire officiel utilisé. Le couple (gabarit PDF, carte de coordonnées) est
    /// embarqué sous <c>Resources/OfficialForms</c> ; un nouveau millésime DGI se traite en
    /// déposant le couple correspondant et en faisant évoluer cette constante.
    /// </summary>
    private const string MonthlyDeclarationFormVersion = "mensuelle-2026";

    /// <inheritdoc />
    public Task<byte[]> GenerateMonthlyDeclarationOfficialFormPdfAsync(
        VatDeclarationDto declaration,
        IReadOnlyDictionary<string, decimal>? withholdingLines = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        cancellationToken.ThrowIfCancellationRequested();

        // Contrairement aux autres exports, aucune mise en page n'est produite ici : le document
        // EST le formulaire officiel. Le binder traduit la déclaration en valeurs de cases, le
        // stamper les dépose aux coordonnées du millésime.
        var map = OfficialFormFieldMap.Load(MonthlyDeclarationFormVersion);
        var values = MonthlyDeclarationFormBinder.Bind(declaration, withholdingLines);

        return Task.FromResult(_officialFormStamper.Stamp(map, values));
    }
}
