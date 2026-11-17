#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const VIEWS_DIR = path.join(__dirname, '..', 'Views');
const IGNORE_FILES = ['_ViewStart.cshtml', '_ViewImports.cshtml', '_ValidationScriptsPartial.cshtml'];

const TRANSLATABLE_TAGS = ['h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'p', 'span', 'a', 'button', 'label', 'th', 'td', 'li', 'option'];

const VI_EN_MAP = {
    'Truy cập bị từ chối': 'accessDenied',
    'Đổi mật khẩu': 'changePassword',
    'Nhập mật khẩu hiện tại để xác minh danh tính': 'enterCurrentPassword',
    'Opps!': 'opps',
    'Cáp quang lại đứt rồi...': 'fiberBroken',
    'Thêm keycap mới vào cơ sở dữ liệu': 'addKeycapDesc',
    'Tên Keycap *': 'nameLabel',
    'Hãng (Brand)': 'brandLabel',
    'Profile': 'profileLabel',
    'Chất liệu': 'materialLabel',
    'URL Hình ảnh': 'imageUrlLabel',
    'Mô tả': 'descriptionLabel',
    'Dán link hình ảnh trực tiếp (JPG, PNG, WebP).': 'imageUrlHint',
    'Mô tả ngắn gọn về keycap...': 'descriptionPlaceholder',
    'VD: GMK Olivia, ePBT Cool Kids...': 'namePlaceholder',
    'VD: GMK, ePBT, JTK, Akko...': 'brandPlaceholder',
    'Hành động này không thể hoàn tác': 'deleteConfirm',
    '&nbsp;·&nbsp;': 'separator',
    'Thêm switch mới vào cơ sở dữ liệu': 'addSwitchDesc',
    'Tên Switch *': 'switchNameLabel',
    'Loại (Type)': 'typeLabel',
    'ActuationForce': 'actuationForceLabel',
    'Hình ảnh (URL)': 'imageUrlLabel',
    'Cập nhật thông tin cấu hình của bạn': 'updateSpecDesc',
    'Thêm kit mới vào cơ sở dữ liệu': 'addKitDesc',
    'Tên Kit *': 'kitNameLabel',
    'Layout': 'layoutLabel',
    'Mount Type': 'mountTypeLabel',
    'PCB Type': 'pcbTypeLabel',
    '— Chọn profile —': 'selectProfile',
    '— Chọn chất liệu —': 'selectMaterial',
    '— Chọn loại —': 'selectType',
    '— Chọn layout —': 'selectLayout',
    '— Chọn mount —': 'selectMount',
    '— Chọn PCB —': 'selectPcb',
    'Cherry': 'cherry',
    'SA': 'sa',
    'OEM': 'oem',
    'DSA': 'dsa',
    'KAT': 'kat',
    'MT3': 'mt3',
    'XDA': 'xda',
    'ASA': 'asa',
    'PBT': 'pbt',
    'ABS': 'abs',
    'POM': 'pom',
    'PC (Polycarbonate)': 'pcPolycarbonate',
    'Linear': 'linear',
    'Tactile': 'tactile',
    'Clicky': 'clicky',
    'Hotswap': 'hotswap',
    'Soldered': 'soldered',
    'Gasket Mount': 'gasketMount',
    'Top Mount': 'topMount',
    'Tray Mount': 'trayMount',
    'Khác': 'other',
    'Lực nhấn cần thiết để kích hoạt switch (tính bằng gram).': 'actuationForceHint',
    'Dán link hình ảnh switch vào đây...': 'switchImageUrlPlaceholder',
    'Dán đường link hình ảnh minh họa cho switch (không bắt buộc).': 'switchImageUrlHint',
    'VD: Boba U4T, Cherry MX Black...': 'switchNamePlaceholder',
    'VD: Gateron, Cherry, JWK...': 'switchBrandPlaceholder',
    'VD: 45g, 62g, 67g...': 'actuationForcePlaceholder',
    'VD: Neo65 Endgame Build...': 'specNamePlaceholder',
    'VD: Neo65, QK75, Zoom65 V2...': 'kitNamePlaceholder',
    'VD: GMK Nord, EPBT BoW...': 'keycapNamePlaceholder',
    'VD: Aluminium, PC, FR4...': 'plateMaterialPlaceholder',
    'VD: Case foam + PE foam': 'foamPlaceholder',
    'VD: Tape mod, tempest mod, switch film...': 'modPlaceholder',
    'Chọn hoặc nhập tên switch...': 'switchSelectPlaceholder',
    'Nhập mật khẩu hiện tại': 'currentPasswordPlaceholder',
    'Ít nhất 6 ký tự': 'minLengthPlaceholder',
    'Nhập lại mật khẩu mới': 'confirmPasswordPlaceholder',
    '40%': 'layout40',
    '60%': 'layout60',
    '65%': 'layout65',
    'Alice': 'layoutAlice',
    '70% FRL': 'layout70Frl',
    '75%': 'layout75',
    'TKL': 'layoutTkl',
    '98 / Fullsize': 'layout98Fullsize',
    'Numpad': 'layoutNumpad',
    'O-Ring': 'oring',
    'O-Ringless': 'oringLess',
    'Sandwich Mount': 'sandwichMount',
    'Plateless': 'plateless',
    'Hotswap + Soldered': 'hotswapSoldered',
    'Nhập tên build, kit, switch...': 'searchPlaceholder',
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
        let key = `${prefix}.${VI_EN_MAP[cleanText]}`;
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
            let key = `${prefix}.${en}`;
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
            allNewKeys.push({ file: path.relative(process.cwd(), file), keys: newKeys });
            console.log(`Wrapped ${newKeys.length} element(s) in ${path.relative(process.cwd(), file)}`);
        }
    }

    if (totalNewKeys === 0) {
        console.log('No untranslated elements found.');
        process.exit(0);
    }

    console.log(`\nTotal: ${totalNewKeys} new translation key(s) across ${allNewKeys.length} file(s).`);
    console.log('\nGenerated keys:');
    for (const { file, keys } of allNewKeys) {
        console.log(`\n${file}:`);
        for (const { key, text } of keys) {
            console.log(`  ${key} → "${text}"`);
        }
    }
    console.log('\nRun "npm run sync:translations" to add missing keys to translations.js.');

    process.exit(1);
}

main();
