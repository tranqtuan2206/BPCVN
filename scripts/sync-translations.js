#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const VIEWS_DIR = path.join(__dirname, '..', 'Views');
const TRANSLATIONS_FILE = path.join(__dirname, '..', 'wwwroot', 'js', 'translations.js');

function findCshtmlFiles(dir) {
    const files = [];
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            files.push(...findCshtmlFiles(fullPath));
        } else if (entry.name.endsWith('.cshtml')) {
            files.push(fullPath);
        }
    }
    return files;
}

function scanDataI18nKeys(viewsDir) {
    const cshtmlFiles = findCshtmlFiles(viewsDir);
    const keys = new Set();

    for (const file of cshtmlFiles) {
        const content = fs.readFileSync(file, 'utf8');
        const regex = /data-i18n="([^"]+)"/g;
        let match;
        while ((match = regex.exec(content)) !== null) {
            keys.add(match[1]);
        }
    }

    return keys;
}

function parseTranslationKeys(fileContent) {
    const enKeys = [];
    const viKeys = [];

    const enSection = fileContent.match(/en:\s*\{([\s\S]*?)\n\s*\},/);
    const viSection = fileContent.match(/vi:\s*\{([\s\S]*?)\n\s*\}\s*\}/);

    if (enSection) {
        const keyRegex = /'([^']+)':/g;
        let m;
        while ((m = keyRegex.exec(enSection[1])) !== null) {
            enKeys.push(m[1]);
        }
    }

    if (viSection) {
        const keyRegex = /'([^']+)':/g;
        let m;
        while ((m = keyRegex.exec(viSection[1])) !== null) {
            viKeys.push(m[1]);
        }
    }

    return { enKeys, viKeys };
}

function findMissingKeys(viewKeys, dictKeys) {
    const missing = [];
    for (const key of viewKeys) {
        if (!dictKeys.includes(key)) {
            missing.push(key);
        }
    }
    return missing;
}

function addMissingKeys(fileContent, missingKeys) {
    let result = fileContent;

    for (const key of missingKeys) {
        const enLine = `        '${key}': '${key}',\n`;
        const viLine = `        '${key}': '${key}',\n`;

        result = result.replace(
            /('common\.cancel':\s*'[^']*',[^\n]*\n)(\s*\},)/,
            `$1${enLine}$2`
        );

        result = result.replace(
            /('common\.cancel':\s*'[^']*',[^\n]*\n)(\s*\}\s*\})/,
            `$1${viLine}$2`
        );
    }

    return result;
}

function main() {
    const viewKeys = scanDataI18nKeys(VIEWS_DIR);
    const fileContent = fs.readFileSync(TRANSLATIONS_FILE, 'utf8');
    const { enKeys, viKeys } = parseTranslationKeys(fileContent);

    const missingInEn = findMissingKeys(viewKeys, enKeys);
    const missingInVi = findMissingKeys(viewKeys, viKeys);

    const allMissing = [...new Set([...missingInEn, ...missingInVi])];

    if (allMissing.length === 0) {
        console.log('All translation keys are in sync.');
        process.exit(0);
    }

    console.log(`Found ${allMissing.length} missing translation key(s):`);
    for (const key of allMissing) {
        const inEn = missingInEn.includes(key) ? 'EN: MISSING' : 'EN: ok';
        const inVi = missingInVi.includes(key) ? 'VI: MISSING' : 'VI: ok';
        console.log(`  - ${key} (${inEn}, ${inVi})`);
    }

    const updatedContent = addMissingKeys(fileContent, allMissing);
    fs.writeFileSync(TRANSLATIONS_FILE, updatedContent, 'utf8');

    console.log(`\nUpdated ${TRANSLATIONS_FILE}`);
    console.log('Please review and update the default values for the new keys.');

    process.exit(1);
}

main();
