const fs = require('fs');
const zlib = require('zlib');
const path = require('path');

const buildDir = path.join(__dirname, 'docs', 'Build');

const files = [
    { input: 'folder.framework.js.br', output: 'folder.framework.js' },
    { input: 'folder.data.br',          output: 'folder.data' },
    { input: 'folder.wasm.br',          output: 'folder.wasm' },
];

async function decompressFile(inputName, outputName) {
    const inputPath  = path.join(buildDir, inputName);
    const outputPath = path.join(buildDir, outputName);

    const inputBuffer  = fs.readFileSync(inputPath);
    const outputBuffer = zlib.brotliDecompressSync(inputBuffer);
    fs.writeFileSync(outputPath, outputBuffer);

    const mb = (outputBuffer.length / 1024 / 1024).toFixed(2);
    console.log(`✅ Decompressed ${inputName} → ${outputName} (${mb} MB)`);
}

(async () => {
    console.log('Decompressing Brotli files for GitHub Pages...');
    for (const f of files) {
        await decompressFile(f.input, f.output);
        const inputPath = path.join(buildDir, f.input);
        if (fs.existsSync(inputPath)) {
            fs.unlinkSync(inputPath);
            console.log(`🗑️ Removed ${f.input}`);
        }
    }

    // Update index.html to remove compression references
    const indexPath = path.join(__dirname, 'docs', 'index.html');
    let html = fs.readFileSync(indexPath, 'utf8');
    
    // Unity loader automatically detects compression from file extension
    // Remove .br references so it loads plain files
    html = html.replace(/folder\.framework\.js\.br/g, 'folder.framework.js');
    html = html.replace(/folder\.data\.br/g, 'folder.data');
    html = html.replace(/folder\.wasm\.br/g, 'folder.wasm');
    html = html.replace(/folder\.loader\.js/g, 'folder.loader.js?v=3');
    html = html.replace('<title>Unity Web Player | fifa26</title>', 
        '<meta http-equiv="Cache-Control" content="no-cache, no-store, must-revalidate">\n    <meta http-equiv="Pragma" content="no-cache">\n    <meta http-equiv="Expires" content="0">\n    <title>FIFA 26 - Unity Web Player</title>');
    html = html.replace(/"companyName":\s*"[^"]*"/, '"companyName": "ashrafnsali"');

    fs.writeFileSync(indexPath, html);
    console.log('✅ Updated index.html — removed .br references');
    console.log('🎮 Done! Ready to push to GitHub.');
})();
