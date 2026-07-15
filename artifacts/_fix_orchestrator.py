from pathlib import Path
import re

p = Path(r"c:\Solution\FactuTrust - Copy\src\Backend\FactuTrust.Infrastructure\Services\Studio\StudioAiSystemOrchestrator.cs")
text = p.read_text(encoding="utf-8")
text2, n1 = re.subn(
    r"new SaveFormLayoutRequest\(layout\)\)",
    "new SaveFormLayoutRequest(layout, null))",
    text,
    count=1,
)
text3, n2 = re.subn(
    r'report\("failed", ".*?", "error", detail: error\);',
    'report("failed", "Échec – annulation", "error", null, error);',
    text2,
    count=1,
)
print(n1, n2)
p.write_text(text3, encoding="utf-8", newline="\n")
