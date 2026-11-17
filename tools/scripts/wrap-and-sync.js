#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const VIEWS_DIR = path.join(__dirname, '..', 'Views');
const TRANSLATIONS_FILE = path.join(__dirname, '..', 'wwwroot', 'js', 'translations.js');
const IGNORE_FILES = ['_ViewStart.cshtml', '_ViewImports.cshtml', '_ValidationScriptsPartial.cshtml'];

const TRANSLATABLE_TAGS = ['h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'p', 'span', 'a', 'button', 'label', 'th', 'td', 'li', 'option'];

const VI_EN_MAP = {
    'Truy cập bị từ chối': 'Access denied',
    'Đổi mật khẩu': 'Change Password',
    'Nhập mật khẩu hiện tại để xác minh danh tính': 'Enter current password to verify identity',
    'Opps!': 'Opps!',
    'Cáp quang lại đứt rồi...': 'Fiber cable is broken again...',
    'Thêm keycap mới vào cơ sở dữ liệu': 'Add new keycap to database',
    'Tên Keycap *': 'Keycap Name *',
    'Hãng (Brand)': 'Brand',
    'Profile': 'Profile',
    'Chất liệu': 'Material',
    'URL Hình ảnh': 'Image URL',
    'Mô tả': 'Description',
    'Dán link hình ảnh trực tiếp (JPG, PNG, WebP).': 'Paste direct image link (JPG, PNG, WebP).',
    'Mô tả ngắn gọn về keycap...': 'Short description of the keycap...',
    'VD: GMK Olivia, ePBT Cool Kids...': 'e.g. GMK Olivia, ePBT Cool Kids...',
    'VD: GMK, ePBT, JTK, Akko...': 'e.g. GMK, ePBT, JTK, Akko...',
    'Hành động này không thể hoàn tác': 'This action cannot be undone',
    'Thêm switch mới vào cơ sở dữ liệu': 'Add new switch to database',
    'Tên Switch *': 'Switch Name *',
    'Loại (Type)': 'Type',
    'Actuation Force': 'Actuation Force',
    'Hình ảnh (URL)': 'Image URL',
    'Cập nhật thông tin cấu hình của bạn': 'Update your build configuration',
    'Thêm kit mới vào cơ sở dữ liệu': 'Add new kit to database',
    'Tên Kit *': 'Kit Name *',
    'Layout': 'Layout',
    'Mount Type': 'Mount Type',
    'PCB Type': 'PCB Type',
    '— Chọn profile —': '— Select profile —',
    '— Chọn chất liệu —': '— Select material —',
    '— Chọn loại —': '— Select type —',
    '— Chọn layout —': '— Select layout —',
    '— Chọn mount —': '— Select mount —',
    '— Chọn PCB —': '— Select PCB —',
    'Khác': 'Other',
    'Cherry': 'Cherry',
    'SA': 'SA',
    'OEM': 'OEM',
    'DSA': 'DSA',
    'KAT': 'KAT',
    'MT3': 'MT3',
    'XDA': 'XDA',
    'ASA': 'ASA',
    'PBT': 'PBT',
    'ABS': 'ABS',
    'POM': 'POM',
    'PC (Polycarbonate)': 'PC (Polycarbonate)',
    'Linear': 'Linear',
    'Tactile': 'Tactile',
    'Clicky': 'Clicky',
    'Hotswap': 'Hotswap',
    'Soldered': 'Soldered',
    'Gasket Mount': 'Gasket Mount',
    'Top Mount': 'Top Mount',
    'Tray Mount': 'Tray Mount',
    'O-Ring': 'O-Ring',
    'O-Ringless': 'O-Ringless',
    'Sandwich Mount': 'Sandwich Mount',
    'Plateless': 'Plateless',
    'Hotswap + Soldered': 'Hotswap + Soldered',
    '40%': '40%',
    '60%': '60%',
    '65%': '65%',
    'Alice': 'Alice',
    '70% FRL': '70% FRL',
    '75%': '75%',
    'TKL': 'TKL',
    '98 / Fullsize': '98 / Fullsize',
    'Numpad': 'Numpad',
    'Lực nhấn cần thiết để kích hoạt switch (tính bằng gram).': 'Force required to activate the switch (in grams).',
    'Dán link hình ảnh switch vào đây...': 'Paste switch image link here...',
    'Dán đường link hình ảnh minh họa cho switch (không bắt buộc).': 'Paste illustration image link for the switch (optional).',
    'VD: Boba U4T, Cherry MX Black...': 'e.g. Boba U4T, Cherry MX Black...',
    'VD: Gateron, Cherry, JWK...': 'e.g. Gateron, Cherry, JWK...',
    'VD: 45g, 62g, 67g...': 'e.g. 45g, 62g, 67g...',
    'VD: Neo65 Endgame Build...': 'e.g. Neo65 Endgame Build...',
    'VD: Neo65, QK75, Zoom65 V2...': 'e.g. Neo65, QK75, Zoom65 V2...',
    'VD: GMK Nord, EPBT BoW...': 'e.g. GMK Nord, EPBT BoW...',
    'VD: Aluminium, PC, FR4...': 'e.g. Aluminium, PC, FR4...',
    'VD: Case foam + PE foam': 'e.g. Case foam + PE foam',
    'VD: Tape mod, tempest mod, switch film...': 'e.g. Tape mod, tempest mod, switch film...',
    'Chọn hoặc nhập tên switch...': 'Select or enter switch name...',
    'Nhập mật khẩu hiện tại': 'Enter current password',
    'Ít nhất 6 ký tự': 'At least 6 characters',
    'Nhập lại mật khẩu mới': 'Re-enter new password',
    '40%': '40%',
    '60%': '60%',
    '65%': '65%',
    'Alice': 'Alice',
    '70% FRL': '70% FRL',
    '75%': '75%',
    'TKL': 'TKL',
    '98 / Fullsize': '98 / Fullsize',
    'Numpad': 'Numpad',
    'O-Ring': 'O-Ring',
    'O-Ringless': 'O-Ringless',
    'Sandwich Mount': 'Sandwich Mount',
    'Plateless': 'Plateless',
    'Hotswap + Soldered': 'Hotswap + Soldered',
    'Nhập tên build, kit, switch...': 'Enter build, kit, switch name...',
    'PC (Polycarbonate)': 'PC (Polycarbonate)',
    'Gasket Mount': 'Gasket Mount',
    'Top Mount': 'Top Mount',
    'Tray Mount': 'Tray Mount',
};

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

function getViewPrefix(filePath) {
    const relPath = path.relative(VIEWS_DIR, filePath).replace(/\\/g, '/').replace(/\.cshtml$/, '');
    const parts = relPath.split('/');
    const dir = parts[0].toLowerCase();
    const view = parts[parts.length - 1].toLowerCase();
    return `${dir}.${view}`;
}

function getKeyForText(text, prefix, tag, usedKeys) {
    const cleanText = text.replace(/&nbsp;/g, ' ').trim();

    if (VI_EN_MAP[cleanText]) {
        let key = `${prefix}.${VI_EN_MAP[cleanText].toLowerCase().replace(/[^a-z0-9]/g, '')}`;
        if (usedKeys.has(key)) {
            let suffix = 2;
            while (usedKeys.has(`${key}${suffix}`)) suffix++;
            key = `${key}${suffix}`;
        }
        usedKeys.add(key);
        return key;
    }

    const lowerText = cleanText.toLowerCase();
    for (const [vi, en] of Object.entries(VI_EN_MAP)) {
        if (vi.toLowerCase() === lowerText) {
            let key = `${prefix}.${en.toLowerCase().replace(/[^a-z0-9]/g, '')}`;
            if (usedKeys.has(key)) {
                let suffix = 2;
                while (usedKeys.has(`${key}${suffix}`)) suffix++;
                key = `${key}${suffix}`;
            }
            usedKeys.add(key);
            return key;
        }
    }

    let counter = 1;
    let key = `${prefix}.${tag}${counter}`;
    while (usedKeys.has(key)) {
        counter++;
        key = `${prefix}.${tag}${counter}`;
    }
    usedKeys.add(key);
    return key;
}

function wrapElement(line, tagName, filePath, usedKeys) {
    const regex = new RegExp(
        `(<${tagName}[^>]*?)>([^<@{][^<]*?)<\\/${tagName}>`,
        'g'
    );

    let result = line;
    let match;
    const replacements = [];

    while ((match = regex.exec(line)) !== null) {
        const fullMatch = match[0];
        const openTag = match[1];
        const textContent = match[2].trim();

        if (!textContent) continue;
        if (textContent.length < 2) continue;
        if (/^\d+$/.test(textContent)) continue;
        if (/^[@{}]/.test(textContent)) continue;
        if (openTag.includes('data-i18n=')) continue;
        if (openTag.includes('data-i18n-placeholder=')) continue;

        const key = getKeyForText(textContent, getViewPrefix(filePath), tagName, usedKeys);

        const hasExistingAttrs = openTag.includes(' ');
        const attrSpace = hasExistingAttrs ? '' : ' ';

        replacements.push({
            fullMatch,
            replacement: `${openTag}${attrSpace} data-i18n="${key}">${textContent}</${tagName}>`,
            text: textContent,
            key
        });
    }

    for (const r of replacements) {
        result = result.replace(r.fullMatch, r.replacement);
    }

    return { result, replacements };
}

function processFile(filePath) {
    const content = fs.readFileSync(filePath, 'utf8');
    const lines = content.split('\n');
    let modified = false;
    const newKeys = [];
    const usedKeys = new Set();

    const newLines = lines.map(line => {
        let currentLine = line;

        for (const tag of TRANSLATABLE_TAGS) {
            const { result, replacements } = wrapElement(currentLine, tag, filePath, usedKeys);
            if (replacements.length > 0) {
                currentLine = result;
                modified = true;
                for (const r of replacements) {
                    newKeys.push({ key: r.key, text: r.text });
                }
            }
        }

        return currentLine;
    });

    return { modified, newLines, newKeys };
}

function parseExistingKeys(fileContent) {
    const keys = new Set();
    const regex = /'([^']+)':/g;
    let match;
    while ((match = regex.exec(fileContent)) !== null) {
        keys.add(match[1]);
    }
    return keys;
}

function addKeysToTranslations(fileContent, newEntries) {
    const lines = fileContent.split('\n');
    const result = [];
    let inEn = false;
    let inVi = false;
    let lastKeyLine = -1;
    let section = '';

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        if (/^\s+en:\s*\{/.test(line)) { inEn = true; inVi = false; }
        if (/^\s+vi:\s*\{/.test(line)) { inEn = false; inVi = true; }
        if (/^\s+\},?\s*$/.test(line) && (inEn || inVi)) {
            const missingHere = newEntries.filter(([k]) => !fileContent.includes(`'${k}':`));
            if (missingHere.length > 0) {
                result.push('');
                result.push('        /* ── Auto-added ─── */');
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
        const content = fs.readFileSync(file, 'utf8');
        const hasDataI18n = /data-i18n="(?!placeholder)/.test(content);
        if (hasDataI18n) continue;

        const { modified, newLines, newKeys } = processFile(file);

        if (modified) {
            fs.writeFileSync(file, newLines.join('\n'), 'utf8');
            totalNewKeys += newKeys.length;
            allNewKeys.push(...newKeys);
            console.log(`Wrapped ${newKeys.length} element(s) in ${path.relative(process.cwd(), file)}`);
        }
    }

    if (totalNewKeys === 0) {
        console.log('No untranslated elements found.');
        process.exit(0);
    }

    console.log(`\nTotal: ${totalNewKeys} new key(s).`);

    const translationsContent = fs.readFileSync(TRANSLATIONS_FILE, 'utf8');
    const existingKeys = parseExistingKeys(translationsContent);
    const missingEntries = allNewKeys
        .filter(({ key }) => !existingKeys.has(key))
        .map(({ key, text }) => {
            const enText = VI_EN_MAP[text] || VI_EN_MAP[text.replace(/&nbsp;/g, ' ').trim()] || text;
            return [key, text, enText];
        });

    if (missingEntries.length > 0) {
        const updated = addKeysToTranslations(translationsContent, missingEntries);
        fs.writeFileSync(TRANSLATIONS_FILE, updated, 'utf8');
        console.log(`Added ${missingEntries.length} key(s) to translations.js`);
    }

    console.log('\nDone! Review translations.js for EN values that need updating.');
    process.exit(1);
}

main();
