"""Check publication metadata, destinations, portable links, and text bounds."""
import argparse
from collections import Counter
import hashlib
import json
import logging
from pathlib import Path
import re
import subprocess

import pdfplumber
from pypdf import PdfReader

from pdf_report_common import _find_chapter_pages, _load_chapter_destinations, _load_chapter_titles


def main():
    """Audit an immutable rendered PDF; this does not replace visual inspection."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('pdf', type=Path)
    parser.add_argument('--report', choices=['technical_reference', 'verification_report'], required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    logging.getLogger('pdfminer').setLevel(logging.ERROR)
    root = Path(__file__).resolve().parents[1]
    metadata = json.loads((root/'docs/report-metadata.json').read_text(encoding='utf-8'))
    metadata = {**metadata, **metadata[args.report].get('checkpoint', {})}
    source = 'technical-reference' if args.report == 'technical_reference' else 'verification'
    reader = PdfReader(args.pdf)
    chapters = _find_chapter_pages(reader, _load_chapter_titles(root, source), _load_chapter_destinations(root, source))
    failures = []
    for field, expected in [('/BestFitCommit', metadata['bestfit_commit']), ('/NumericsCommit', metadata['numerics_source_commit']), ('/ReleaseStatus', metadata['release_status'])]:
        if reader.metadata.get(field) != expected:
            failures.append(f'Metadata mismatch: {field}')
    if len(reader.outline) != len(chapters)+2:
        failures.append('Incorrect bookmark count')
    if '/StructTreeRoot' not in reader.trailer['/Root']:
        failures.append('Tagged structure is missing')
    destinations = {str(k).lstrip('/'): v for k,v in reader.named_destinations.items()}
    links = Counter()
    source_targets = set()
    for index, page in enumerate(reader.pages):
        for ref in page.get('/Annots', []):
            annotation = ref.get_object()
            if annotation.get('/Dest') is not None:
                name = str(annotation['/Dest']).lstrip('/')
                if name not in destinations: failures.append(f'Unresolved destination on page {index+1}: {name}')
                links['internal'] += 1
            elif uri := annotation.get('/A', {}).get('/URI'):
                uri = str(uri)
                if not re.match(r'https?://', uri): failures.append(f'Nonportable link: {uri}')
                prefix = 'https://github.com/USACE-RMC/RMC-BestFit/'
                if uri.startswith((prefix+'blob/', prefix+'tree/')):
                    parts = uri[len(prefix):].split('/', 2)
                    if len(parts)!=3 or parts[0] not in ('blob','tree') or parts[1]!=metadata['bestfit_commit']:
                        failures.append(f'Incorrect source checkpoint link: {uri}')
                    else: source_targets.add((parts[0], parts[2].split('#')[0]))
                links['external'] += 1
    # Check pinned links against the Git object database, not only the current filesystem.
    from urllib.parse import unquote
    for kind, relative in sorted(source_targets):
        result = subprocess.run(['git','cat-file','-t',metadata['bestfit_commit']+':'+unquote(relative)],cwd=root,capture_output=True,text=True)
        if result.returncode or result.stdout.strip()!=kind:
            failures.append(f'Pinned {kind} target absent at checkpoint: {relative}')
    overflow = []
    low_content = []
    fonts = Counter()
    with pdfplumber.open(args.pdf) as pdf:
        for number, page in enumerate(pdf.pages,1):
            if number == 1: continue
            body = [c for c in page.chars if 45<c['top']<742]
            if len(body)<80: low_content.append(number)
            for char in body:
                fonts[round(char['size'],2)]+=1
                if char['x0']<49 or char['x1']>563:
                    overflow.append({'page':number,'text':char['text'],'x0':round(char['x0'],2),'x1':round(char['x1'],2)})
            text=page.extract_text() or ''
            if f'{number} / {len(pdf.pages)}' not in text:
                failures.append(f'Missing printed page number: {number}')
    result = {'sha256':hashlib.sha256(args.pdf.read_bytes()).hexdigest(), 'pages':len(reader.pages), 'chapters':len(chapters),'bookmarks':len(reader.outline),'contentsPages':list(range(2,chapters[0][1]+1)), 'chapterPages':chapters,'metadata':dict(reader.metadata),'links':dict(links),'pinnedTargetsChecked':len(source_targets),'bodyFontSizes':fonts.most_common(6),'lowContentPages':low_content,'horizontalTextOverflow':overflow,'failures':failures}
    args.output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['chapterPages','metadata','horizontalTextOverflow']},indent=2))
    print(f'Horizontal text overflow: {len(overflow)} characters; inspect {args.output}')
    if failures or overflow: raise SystemExit(1)


if __name__ == '__main__':
    main()
