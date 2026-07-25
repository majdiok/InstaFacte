using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.FirmGovernance;

namespace FactuTrust.Infrastructure.Services;

internal static class PermanentFileCompanyProfileSeeder
{
    internal static void Apply(CompanyProfileSnapshotDto profile, PermanentFile file)
    {
        file.UpdateIdentity(
            profile.CompanyName,
            profile.Nif,
            profile.RneIdentifier,
            legalForm: null,
            incorporationDate: null,
            shareCapital: null);

        file.UpdateTaxAdministration(
            taxOffice: null,
            (TaxRegime)profile.TaxRegime,
            hasTaxCertificate: false);

        file.UpdateRegisteredOffice(
            profile.Street,
            profile.City,
            profile.Governorate,
            profile.PostalCode);
    }
}
