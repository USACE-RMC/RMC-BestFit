// Independent Markdown/MathJax validation; offline LaTeX/SVG validation is separate.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const reference = path.join(root, 'docs/technical-reference');
const [htmlPath, mathjaxPath, outputPath] = process.argv.slice(2);
if (!htmlPath || !mathjaxPath || !outputPath || !process.env.BESTFIT_NODE_MODULES) {
    throw new Error('Usage: node validate-technical-reference-math.mjs HTML LOCAL_MATHJAX_BUNDLE REPORT_JSON; set BESTFIT_NODE_MODULES.');
}
const require = createRequire(pathToFileURL(path.join(process.env.BESTFIT_NODE_MODULES, 'package.json')));
const { chromium } = require('playwright');
const entries = fs.readFileSync(path.join(reference, 'book-order.txt'), 'utf8').split(/\r?\n/)
    .map(x => x.trim()).filter(x => x && !x.startsWith('#'));
const failures = [];
const equations = [];
for (const entry of entries) {
    const source = fs.readFileSync(path.join(reference, entry), 'utf8');
    if (/[\x00-\x08\x0b\x0c\x0e-\x1f]/.test(source)) failures.push({entry, error:'Control character in Markdown'});
    const prose = source.replace(/```[\s\S]*?```|`[^`\n]*`/g, match => match.replace(/[^\n]/g, ' '));
    if (/(?<!\\)\\[()[\]]/.test(prose)) failures.push({entry, error:'Use GitHub dollar delimiters, not slash delimiters'});
    let display = false;
    let displayTex = [];
    const lines = prose.split(/\r?\n/);
    for (let index=0; index<lines.length; index++) {
        const line = lines[index];
        if (line.trim()==='$$') {
            if (display) {
                equations.push({entry, line:index+1, tex:displayTex.join('\n').trim(), display:true});
                displayTex=[];
                if (lines[index+1]?.trim()) failures.push({entry,line:index+1,error:'Display closing delimiter needs a following blank line'});
            } else if (index && lines[index-1].trim()) failures.push({entry,line:index+1,error:'Display opening delimiter needs a preceding blank line'});
            display=!display;
        } else if (display) displayTex.push(line);
        else {
            if (line.includes('$$')) failures.push({entry,line:index+1,error:'Display delimiter must occupy its own line'});
            const inline=[...line.matchAll(/(?<!\\)\$([^$]*?)(?<!\\)\$/g)];
            const rest=line.replace(/(?<!\\)\$([^$]*?)(?<!\\)\$/g,'');
            if (/(?<!\\)\$/.test(rest)) failures.push({entry,line:index+1,error:'Unmatched inline dollar'});
            if (/\\[A-Za-z]+/.test(rest)) failures.push({entry,line:index+1,error:'Raw TeX command outside code or mathematics'});
            for (const match of inline) {
                if (/(?<!\\)\b(?:widehat|boldsymbol|mathbf|mathrm|frac)(?=[\\{])/.test(match[1])) failures.push({entry,line:index+1,error:'TeX command appears to be missing its backslash'});
                if (line.trimStart().startsWith('|') && /(?<!\\)\|/.test(match[1])) failures.push({entry,line:index+1,error:'Raw table separator inside math'});
                equations.push({entry,line:index+1,tex:match[1],display:false});
            }
        }
    }
    if (display) failures.push({entry,error:'Unclosed display equation'});
}
const unique=[...new Map(equations.map(e=>[(e.display?'display:':'inline:')+e.tex,e])).values()];
const browser=await chromium.launch({executablePath:process.env.BESTFIT_BROWSER_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
try {
    const page=await browser.newPage({viewport:{width:816,height:1056}});
    await page.goto(pathToFileURL(path.resolve(htmlPath)).href);
    await page.evaluate(()=>{window.MathJax={startup:{typeset:false},tex:{tags:'ams'}};});
    await page.addScriptTag({path:path.resolve(mathjaxPath)});
    await page.evaluate(()=>MathJax.startup.promise);
    const math=await page.evaluate(async equations=>{
        const errors=[];
        for (const equation of equations) {
            try {
                const node=await MathJax.tex2svgPromise(equation.tex,{display:equation.display});
                const error=node.querySelector('[data-mjx-error],merror');
                if (error || !node.querySelector('svg')) errors.push({...equation,error:error?.getAttribute('data-mjx-error') || 'No SVG'});
            } catch(error) {errors.push({...equation,error:String(error)});}
        }
        return {engine:MathJax.version,errors};
    },unique);
    const structure=await page.evaluate(()=>{
        const ids=[...document.querySelectorAll('[id]')].map(e=>e.id);
        return {
            brokenImages:[...document.images].filter(e=>!e.complete || !e.naturalWidth).map(e=>e.src),
            brokenFragments:[...document.querySelectorAll('a[href^="#"]')].map(e=>e.getAttribute('href')).filter(h=>!document.getElementById(decodeURIComponent(h.slice(1)))),
            localExternalLinks:[...document.querySelectorAll('a[href]')].map(e=>e.getAttribute('href')).filter(h=>!h.startsWith('#') && !/^https?:/.test(h)),
            duplicateIds:ids.filter((id,i)=>ids.indexOf(id)!==i),
            unrenderedHeadingMarkup:[...document.querySelectorAll('h1,h2,h3,h4')].map(e=>e.textContent).filter(text=>text.includes('`')),
            malformedTableRows:[...document.querySelectorAll('table')].flatMap((table,i)=>[...table.rows].filter(row=>row.cells.length!==table.rows[0].cells.length).map(row=>({table:i+1,text:row.textContent.slice(0,100)}))),
            renderedEquations:[...new Map([...document.images].filter(e=>e.src.includes('/equations/')).map(e=>[e.src,{tex:e.closest('.display-equation')?.getAttribute('aria-label') || e.alt,display:!!e.closest('.display-equation')}])).values()],
        };
    });
    const rendered=new Set(structure.renderedEquations.map(e=>(e.display?'display:':'inline:')+e.tex.trim()));
    const missingFromPdf=unique.filter(e=>!rendered.has((e.display?'display:':'inline:')+e.tex.replace(/\s+/g,' ').trim()));
    const report={chapters:entries.length,mathjaxVersion:math.engine,sourceEquationOccurrences:equations.length,uniqueSourceEquations:unique.length,offlineEquationAssets:structure.renderedEquations.length,markdownFailures:failures,mathjaxFailures:math.errors,missingFromPdf,...structure};
    delete report.renderedEquations;
    fs.writeFileSync(outputPath,JSON.stringify(report,null,2)+'\n');
    console.log(JSON.stringify(report,null,2));
    if (Object.values(report).some(value=>Array.isArray(value) && value.length)) process.exitCode=1;
} finally {await browser.close();}
