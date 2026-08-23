namespace FactuTrust.Application.DTOs;

public sealed record UpdateProductAttributeRequest(string Name, int SortOrder = 0);

public sealed record AddProductAttributeValueRequest(string Code, string Name, int SortOrder = 0);

public sealed record UpdateProductAttributeValueRequest(string Name, int SortOrder = 0);
