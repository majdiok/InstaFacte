import pathlib
files = [
    r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Infrastructure\Repositories\InvoiceRepository.cs",
    r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Infrastructure\Repositories\DeliveryNoteRepository.cs",
    r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Application\Common\Interfaces\Repositories\IInvoiceRepository.cs",
    r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Application\Common\Interfaces\Repositories\IDeliveryNoteRepository.cs",
]
for f in files:
    p = pathlib.Path(f)
    text = p.read_text(encoding="utf-8")
    if "[Obsolete" not in text and "GetNextNumberAsync" in text:
        text = text.replace(
            "    Task<InvoiceNumber> GetNextNumberAsync",
            "    [Obsolete(\"Use IDocumentNumberService instead.\")]\n    Task<InvoiceNumber> GetNextNumberAsync",
        )
        text = text.replace(
            "    Task<DeliveryNoteNumber> GetNextNumberAsync",
            "    [Obsolete(\"Use IDocumentNumberService instead.\")]\n    Task<DeliveryNoteNumber> GetNextNumberAsync",
        )
        text = text.replace(
            "    public async Task<InvoiceNumber> GetNextNumberAsync",
            "    [Obsolete(\"Use IDocumentNumberService instead.\")]\n    public async Task<InvoiceNumber> GetNextNumberAsync",
        )
        text = text.replace(
            "    public async Task<DeliveryNoteNumber> GetNextNumberAsync",
            "    [Obsolete(\"Use IDocumentNumberService instead.\")]\n    public async Task<DeliveryNoteNumber> GetNextNumberAsync",
        )
        p.write_text(text, encoding="utf-8")
        print("updated", f)
print("done")
