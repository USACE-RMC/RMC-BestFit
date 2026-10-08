# BestFit plugin artwork

`bestfit-icon.png` is the unchanged 256 x 256 PNG frame embedded in
`src/RMC.BestFit.App/Resources/BestFit_Icon.ico`. Both OpenAI interface image fields
reuse this official application artwork. No resizing, recoloring, or redrawing
was applied.

To reproduce the extraction from the repository root using Python's standard library:

```python
from pathlib import Path
import struct

source = Path("src/RMC.BestFit.App/Resources/BestFit_Icon.ico").read_bytes()
count = struct.unpack_from("<H", source, 4)[0]
for index in range(count):
    width, height, _, _, _, _, size, offset = struct.unpack_from("<BBBBHHII", source, 6 + 16 * index)
    frame = source[offset:offset + size]
    if (width or 256, height or 256) == (256, 256) and frame.startswith(b"\x89PNG\r\n\x1a\n"):
        Path("packaging/bestfit-frequency/assets/bestfit-icon.png").write_bytes(frame)
        break
else:
    raise ValueError("The official application icon has no 256 x 256 PNG frame")
```
