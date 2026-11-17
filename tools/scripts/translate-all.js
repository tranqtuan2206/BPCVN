#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const VIEWS_DIR = path.join(__dirname, '..', 'Views');
const TRANSLATIONS_FILE = path.join(__dirname, '..', 'wwwroot', 'js', 'translations.js');

const TRANSLATIONS = {
    // Kit Details
    'Thông số Kit': { key: 'kit.details.specs', en: 'Kit Specifications' },
    'Tên Kit': { key: 'kit.details.name', en: 'Kit Name' },
    'Hãng (Brand)': { key: 'kit.details.brand', en: 'Brand' },
    'Mount Type': { key: 'kit.details.mountType', en: 'Mount Type' },
    'PCB Type': { key: 'kit.details.pcbType', en: 'PCB Type' },
    'Chỉnh sửa': { key: 'common.edit', en: 'Edit' },
    'Xóa': { key: 'common.delete', en: 'Delete' },
    'Danh sách': { key: 'common.backToList', en: 'Back to list' },
    'Quay lại danh sách': { key: 'common.backToList', en: 'Back to list' },

    // Switch Details
    'Thông số Switch': { key: 'switch.details.specs', en: 'Switch Specifications' },
    'Tên Switch': { key: 'switch.details.name', en: 'Switch Name' },
    'Loại (Type)': { key: 'switch.details.type', en: 'Type' },
    'Lực nhấn': { key: 'switch.details.actuationForce', en: 'Actuation Force' },

    // Keycap Details
    'Thông số Keycap': { key: 'keycap.details.specs', en: 'Keycap Specifications' },
    'Tên Keycap': { key: 'keycap.details.name', en: 'Keycap Name' },
    'Chất liệu': { key: 'keycap.details.material', en: 'Material' },
    'Hình ảnh': { key: 'keycap.details.image', en: 'Image' },
    'Mô tả': { key: 'keycap.details.description', en: 'Description' },
    'Có': { key: 'common.yes', en: 'Yes' },

    // Delete pages
    'Xác nhận xóa Kit': { key: 'kit.delete.title', en: 'Confirm Delete Kit' },
    'Xác nhận xóa Switch': { key: 'switch.delete.title', en: 'Confirm Delete Switch' },
    'Xác nhận xóa Keycap': { key: 'keycap.delete.title', en: 'Confirm Delete Keycap' },
    'Hãng': { key: 'common.brand', en: 'Brand' },
    'Layout': { key: 'common.layout', en: 'Layout' },
    'Mount': { key: 'common.mount', en: 'Mount' },
    'Loại': { key: 'common.type', en: 'Type' },
    'Profile': { key: 'common.profile', en: 'Profile' },
    'Xóa vĩnh viễn': { key: 'common.deletePermanently', en: 'Delete permanently' },
    'Huỷ': { key: 'common.cancel', en: 'Cancel' },

    // Spec Details
    'Sửa': { key: 'common.edit', en: 'Edit' },
    'Sound Tests': { key: 'spec.details.soundTests', en: 'Sound Tests' },
    'Không rõ mic': { key: 'spec.details.unknownMic', en: 'Unknown mic' },
    'Custom switch': { key: 'spec.details.customSwitch', en: 'Custom switch' },
    'Không rõ': { key: 'spec.details.unknown', en: 'Unknown' },
    'Plate': { key: 'spec.details.plate', en: 'Plate' },
    'Foam Setup': { key: 'spec.details.foamSetup', en: 'Foam Setup' },
    'Mods': { key: 'spec.details.mods', en: 'Mods' },

    // Index pages
    'Kit Catalog': { key: 'kit.index.title', en: 'Kit Catalog' },
    'Switch Catalog': { key: 'switch.index.title', en: 'Switch Catalog' },
    'Keycap Catalog': { key: 'keycap.index.title', en: 'Keycap Catalog' },
    'Brand': { key: 'common.brand', en: 'Brand' },

    // Kit Create/Edit
    'Thêm Kit mới': { key: 'kit.create.title', en: 'Add New Kit' },
    'Thêm Switch mới': { key: 'switch.create.title', en: 'Add New Switch' },
    'Thêm Keycap mới': { key: 'keycap.create.title', en: 'Add New Keycap' },
    'Thêm Kit': { key: 'kit.create.submit', en: 'Add Kit' },
    'Thêm Switch': { key: 'switch.create.submit', en: 'Add Switch' },
    'Thêm Keycap': { key: 'keycap.create.submit', en: 'Add Keycap' },

    // Shared
    'Unknown Brand': { key: 'common.unknownBrand', en: 'Unknown Brand' },
};

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

function getViewPrefix(filePath) {
    const relPath = path.relative(VIEWS_DIR, filePath).replace(/\\/g, '/').replace(/\.cshtml$/, '');
    const parts = relPath.split('/');
    const dir = parts[0].toLowerCase();
    const view = parts[parts.length - 1].toLowerCase();
    return `${dir}.${view}`;
}

function wrapText(line, prefix, usedKeys) {
    let result = line;

    // Pattern 1: <tag>text</tag>
    const tagRegex = /(<(?:h[1-6]|p|span|a|button|label|div|th|td|li|option|small)[^>]*?)>([^<@{][^<]{1,})<\//g;
    let match;

    while ((match = tagRegex.exec(line)) !== null) {
        const fullMatch = match[0];
        const openTag = match[1];
        const text = match[2].trim();

        if (!text || text.length < 2) continue;
        if (/^\d+$/.test(text)) continue;
        if (openTag.includes('data-i18n=')) continue;

        const translation = TRANSLATIONS[text];
        if (!translation) continue;

        let key = translation.key;
        if (usedKeys.has(key)) {
            let suffix = 2;
            while (usedKeys.has(`${key}${suffix}`)) suffix++;
            key = `${key}${suffix}`;
        }
        usedKeys.add(key);

        const hasAttrs = openTag.includes(' ');
        const space = hasAttrs ? '' : ' ';
        result = result.replace(fullMatch, `${openTag}${space} data-i18n="${key}">${text}</`);
    }

    // Pattern 2: </i>text</tag> (text after icon)
    const iconTextRegex = /(<\/i>)([^<@{][^<]{1,?})(<\/(?:a|button|span|label|div|h[1-6]|p)>)/g;
    let match2;

    while ((match2 = iconTextRegex.exec(line)) !== null) {
        const fullMatch = match2[0];
        const text = match2[2].trim();

        if (!text || text.length < 2) continue;
        if (/^\d+$/.test(text)) continue;
        if (result.includes(`>${text}<`) && result.includes('data-i18n=')) continue;

        const translation = TRANSLATIONS[text];
        if (!translation) continue;

        let key = translation.key;
        if (usedKeys.has(key)) {
            let suffix = 2;
            while (usedKeys.has(`${key}${suffix}`)) suffix++;
            key = `${key}${suffix}`;
        }
        usedKeys.add(key);

        // Wrap with span
        result = result.replace(fullMatch, `</i><span data-i18n="${key}">${text}</span>${match2[3]}`);
    }

    return result;
}

function processFile(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split('\n');
    const prefix = getViewPrefix(filePath);
    const usedKeys = new Set();
    let modified = false;
    const newKeys = [];

    const newLines = lines.map(line => {
        const result = wrapText(line, prefix, usedKeys);
        if (result !== line) {
            modified = true;
            // Extract keys added
            const keyMatches = result.match(/data-i18n="([^"]+)"/g);
            if (keyMatches) {
                for (const km of keyMatches) {
                    const key = km.match(/data-i18n="([^"]+)"/)[1];
                    if (!line.includes(`data-i18n="${key}"`)) {
                        // Find the text for this key
                        const textMatch = result.match(new RegExp(`data-i18n="${key}"[^>]*>([^<]+)`));
                        if (textMatch) {
                            const viText = textMatch[1].trim();
                            const translation = TRANSLATIONS[viText];
                            if (translation) {
                                newKeys.push({ key, vi: viText, en: translation.en });
                            }
                        }
                    }
                }
            }
        }
        return result;
    });

    return { modified, newLines, newKeys };
}

function addKeysToTranslations(fileContent, newEntries) {
    const lines = fileContent.split('\n');
    const result = [];
    let inEn = false;
    let inVi = false;

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        if (/^\s+en:\s*\{/.test(line)) { inEn = true; inVi = false; }
        if (/^\s+vi:\s*\{/.test(line)) { inEn = false; inVi = true; }
        if (/^\s+\},?\s*$/.test(line) && (inEn || inVi)) {
            const missingHere = newEntries.filter(([k]) => !fileContent.includes(`'${k}':`));
            if (missingHere.length > 0) {
                result.push('');
                result.push('        /* ── Auto-translated ─── */');
                for (const [key, viText, enText] of missingHere) {
                    const text = inEn ? enText : viText;
                    result.push(`        '${key}': '${text.replace(/'/g, "\\'")}',`);
                }
            }
            inEn = false;
            inVi = false;
        }
        result.push(line);
    }

    return result.join('\n');
}

function main() {
    const files = findCshtmlFiles(VIEWS_DIR);
    let totalNewKeys = 0;
    const allNewKeys = [];

    for (const file of files) {
        const { modified, newLines, newKeys } = processFile(file);

        if (modified) {
            fs.writeFileSync(file, newLines.join('\n'), 'utf8');
            totalNewKeys += newKeys.length;
            allNewKeys.push(...newKeys);
            console.log(`Wrapped ${newKeys.length} element(s) in ${path.relative(process.cwd(), file)}`);
        }
    }

    if (totalNewKeys === 0) {
        console.log('No new translations needed.');
        process.exit(0);
    }

    console.log(`\nTotal: ${totalNewKeys} new translation key(s).`);

    const translationsContent = fs.readFileSync(TRANSLATIONS_FILE, 'utf8');
    const existingKeys = new Set();
    const regex = /'([^']+)':/g;
    let match;
    while ((match = regex.exec(translationsContent)) !== null) {
        existingKeys.add(match[1]);
    }

    const missingEntries = allNewKeys
        .filter(({ key }) => !existingKeys.has(key))
        .map(({ key, vi, en }) => [key, vi, en]);

    if (missingEntries.length > 0) {
        const updated = addKeysToTranslations(translationsContent, missingEntries);
        fs.writeFileSync(TRANSLATIONS_FILE, updated, 'utf8');
        console.log(`Added ${missingEntries.length} key(s) to translations.js`);
    }

    console.log('\nDone!');
    process.exit(1);
}

main();
