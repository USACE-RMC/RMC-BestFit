import os
import re

pattern = re.compile(r'public\s+(async\s+)?(void|Task)\s+(Test_\w+)\(\)')

test_files = []
for root, dirs, files in os.walk('.'):
    if 'obj' in dirs:
        dirs.remove('obj')
    for f in sorted(files):
        if f.endswith('Tests.cs') and f != 'MSTestSettings.cs':
            test_files.append(os.path.join(root, f))

for fpath in sorted(test_files):
    with open(fpath, 'r') as f:
        content = f.read()
        folder = os.path.dirname(fpath).replace('\', '/').replace('./', '')
        fname = os.path.basename(fpath)
        
        # Count test methods
        matches = pattern.findall(content)
        if matches:
            print(f"{folder}/{fname}:{len(matches)}")
