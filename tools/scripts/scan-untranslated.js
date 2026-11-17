#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const VIEWS_DIR = path.join(__dirname, '..', 'Views');
const IGNORE_FILES = ['_ViewStart.cshtml', '_ViewImports.cshtml', '_ValidationScriptsPartial.cshtml'];

function findCshtmlFiles(dir) {
    const files = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            files.push(...findCshtmlFiles(fullPath));
        } else if (entry.name.endsWith('.cshtml') && !IGNORE_FILES.includes(entry.name)) {
            files.push(fullPath);
        }
    }
    return files;
}

function isFullyWrapped(content) {
    return content.includes('data-i18n=');
}

function findHardcodedText(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split('\n');
    const results = [];

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        const lineNum = i + 1;

        if (line.includes('data-i18n=')) continue;

        const tags = ['h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'p', 'span', 'a', 'button', 'label', 'th', 'td', 'li', 'option'];
        for (const tag of tags) {
            const regex = new RegExp(`<${tag}[^>]*>([^<@{][^<]{2,})<\\/${tag}>`, 'g');
            let match;
            while ((match = regex.exec(line)) !== null) {
                const text = match[1].trim();
                if (!text) continue;
                if (/^\d+$/.test(text)) continue;
                if (/^[@{}]/.test(text)) continue;
                if (text.includes('@@') || text.includes('{{')) continue;

                results.push({ line: lineNum, tag, text });
            }
        }

        const placeholderRegex = /placeholder="([^"]{3,})"/g;
        let phMatch;
        while ((phMatch = placeholderRegex.exec(line)) !== null) {
            if (line.includes('data-i18n-placeholder=')) continue;
            results.push({ line: lineNum, tag: 'placeholder', text: phMatch[1] });
        }
    }

    return results;
}

function main() {
    const files = findCshtmlFiles(VIEWS_DIR);
    const filesWithIssues = [];

    for (const file of files) {
        const content = fs.readFileSync(file, 'utf8');
        if (isFullyWrapped(content)) continue;

        const issues = findHardcodedText(file);
        if (issues.length > 0) {
            filesWithIssues.push({ file: path.relative(process.cwd(), file), issues });
        }
    }

    if (filesWithIssues.length === 0) {
        console.log('All views are fully translated!');
        process.exit(0);
    }

    let total = 0;
    console.log('=== Views with untranslated text ===\n');

    for (const { file, issues } of filesWithIssues) {
        console.log(`📄 ${file} (${issues.length} elements)`);
        for (const { line, tag, text } of issues) {
            console.log(`   L${line} <${tag}> "${text}"`);
        }
        console.log('');
        total += issues.length;
    }

    console.log(`\nTotal: ${total} untranslated element(s) across ${filesWithIssues.length} file(s).`);
    console.log('\nTo add translations:');
    console.log('1. Add data-i18n="your.key" to each element in the view');
    console.log('2. Run "npm run sync:translations" to auto-add keys to translations.js');
    console.log('3. Update the EN translation values in translations.js');

    process.exit(1);
}

main();
