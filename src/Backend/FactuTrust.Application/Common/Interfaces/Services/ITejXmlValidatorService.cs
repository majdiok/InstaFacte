namespace FactuTrust.Application.Common.Interfaces.Services;

public interface ITejXmlValidatorService
{
    List<string> Validate(byte[] xmlContent);
}
