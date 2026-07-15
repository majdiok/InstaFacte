import pathlib
p = pathlib.Path(r"c:\Solution\FactuTrust - Copy\src\Backend\tests\FactuTrust.Infrastructure.Tests\Domain\NumberingFormatRendererTests.cs")
text = p.read_text(encoding="utf-8")
if "using Xunit;" not in text:
    text = "using Xunit;\n" + text
    p.write_text(text, encoding="utf-8")
print("fixed")
