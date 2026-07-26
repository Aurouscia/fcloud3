#!/usr/bin/env node
/**
 * 开发脚本：下载 KaTeX、Mermaid、PrismJS 的渲染资源到 public/renderers，
 * 用于生产环境自托管，避免运行时依赖外部 CDN。
 *
 * KaTeX / PrismJS 通过 npm registry tarball 批量提取（内容与 unpkg 同源），
 * Mermaid 的 npm tarball 过大，改为直链下载其压缩后的 dist 产物。
 *
 * 用法：
 *   node collect-renderers.mjs
 */
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT_DIR = __dirname;

const VERSIONS = Object.freeze({
    katex: '0.17.0',
    mermaid: '11.15.0',
    prism: '1.30.0',
});

const packages = [
    {
        name: 'katex',
        version: VERSIONS.katex,
        tarball: {
            url: `https://registry.npmjs.org/katex/-/katex-${VERSIONS.katex}.tgz`,
            copy: [
                { from: 'dist/katex.min.js', to: `katex@${VERSIONS.katex}/katex.min.js` },
                { from: 'dist/katex.min.css', to: `katex@${VERSIONS.katex}/katex.min.css` },
                // CSS 中通过相对路径引用字体，必须一并自托管
                { from: 'dist/fonts', to: `katex@${VERSIONS.katex}/fonts` },
            ],
        },
    },
    {
        name: 'mermaid',
        version: VERSIONS.mermaid,
        files: [
            {
                url: `https://unpkg.com/mermaid@${VERSIONS.mermaid}/dist/mermaid.min.js`,
                to: `mermaid@${VERSIONS.mermaid}/mermaid.min.js`,
            },
        ],
    },
    {
        name: 'prismjs',
        version: VERSIONS.prism,
        tarball: {
            url: `https://registry.npmjs.org/prismjs/-/prismjs-${VERSIONS.prism}.tgz`,
            copy: [
                // 核心与所有语言高亮组件
                { from: 'components', to: `prismjs@${VERSIONS.prism}/components` },
                // 主题 CSS（当前 index.html 使用 prism-tomorrow）
                { from: 'themes', to: `prismjs@${VERSIONS.prism}/themes` },
                // 当前使用的插件及其样式
                { from: 'plugins/line-numbers', to: `prismjs@${VERSIONS.prism}/plugins/line-numbers` },
                { from: 'plugins/toolbar', to: `prismjs@${VERSIONS.prism}/plugins/toolbar` },
                { from: 'plugins/copy-to-clipboard', to: `prismjs@${VERSIONS.prism}/plugins/copy-to-clipboard` },
            ],
        },
    },
];

async function fetchBuffer(url) {
    const timeoutMs = 5 * 60 * 1000; // 单个下载最多 5 分钟
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);

    try {
        const res = await fetch(url, { signal: controller.signal });
        if (!res.ok) {
            throw new Error(`下载失败 ${url}: ${res.status} ${res.statusText}`);
        }
        return Buffer.from(await res.arrayBuffer());
    } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') {
            throw new Error(`下载超时 ${url}（>${timeoutMs / 1000}s）`);
        }
        throw err;
    } finally {
        clearTimeout(timer);
    }
}

async function ensureTarAvailable() {
    try {
        await execFileAsync('tar', ['--version']);
    } catch {
        throw new Error('本脚本需要系统 tar 命令，当前环境未找到');
    }
}

async function copyRecursive(src, dest) {
    const stat = await fs.stat(src);
    if (stat.isDirectory()) {
        await fs.mkdir(dest, { recursive: true });
        const entries = await fs.readdir(src);
        const counts = await Promise.all(
            entries.map((entry) => copyRecursive(path.join(src, entry), path.join(dest, entry)))
        );
        return counts.reduce((a, b) => a + b, 0);
    }
    await fs.mkdir(path.dirname(dest), { recursive: true });
    await fs.copyFile(src, dest);
    return 1;
}

async function cleanOutputTargets() {
    const targets = [`katex@${VERSIONS.katex}`, `mermaid@${VERSIONS.mermaid}`, `prismjs@${VERSIONS.prism}`];
    await Promise.all(
        targets.map(async (target) => {
            const targetPath = path.join(OUT_DIR, target);
            try {
                await fs.rm(targetPath, { recursive: true, force: true });
            } catch {
                // 目录不存在时忽略
            }
        })
    );
}

async function processTarballPackage(pkg, tempRoot) {
    if (!pkg.tarball) {
        throw new Error(`package ${pkg.name} 缺少 tarball 配置`);
    }

    const pkgTempDir = path.join(tempRoot, pkg.name);
    await fs.mkdir(pkgTempDir, { recursive: true });

    const tarballPath = path.join(pkgTempDir, 'package.tgz');
    console.log(`  下载 tarball: ${pkg.tarball.url}`);
    const buffer = await fetchBuffer(pkg.tarball.url);
    await fs.writeFile(tarballPath, buffer);
    console.log(`  大小: ${(buffer.length / 1024 / 1024).toFixed(2)} MB`);

    console.log(`  解压到临时目录`);
    await execFileAsync('tar', ['-xzf', tarballPath, '-C', pkgTempDir]);

    let totalFiles = 0;
    for (const item of pkg.tarball.copy) {
        const src = path.join(pkgTempDir, 'package', item.from);
        const dest = path.join(OUT_DIR, item.to);
        const count = await copyRecursive(src, dest);
        totalFiles += count;
        console.log(`  复制: ${item.from} -> ${item.to} (${count} 个文件)`);
    }

    return { name: pkg.name, files: totalFiles };
}

async function processFilePackage(pkg) {
    if (!pkg.files) {
        throw new Error(`package ${pkg.name} 缺少 files 配置`);
    }

    for (const item of pkg.files) {
        console.log(`  下载: ${item.url}`);
        const buffer = await fetchBuffer(item.url);
        const dest = path.join(OUT_DIR, item.to);
        await fs.mkdir(path.dirname(dest), { recursive: true });
        await fs.writeFile(dest, buffer);
        console.log(`  保存: ${item.to} (${(buffer.length / 1024).toFixed(1)} KB)`);
    }

    return { name: pkg.name, files: pkg.files.length };
}

async function processPackage(pkg, tempRoot) {
    console.log(`\n[${pkg.name}@${pkg.version}]`);

    if (pkg.tarball) {
        return processTarballPackage(pkg, tempRoot);
    }
    if (pkg.files) {
        return processFilePackage(pkg);
    }
    throw new Error(`package ${pkg.name} 缺少 source 配置`);
}

async function main() {
    await ensureTarAvailable();
    await fs.mkdir(OUT_DIR, { recursive: true });

    // 清理旧资源，避免已删除的文件残留
    console.log('清理旧的渲染器资源...');
    await cleanOutputTargets();

    const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'collect-renderers-'));
    console.log(`临时目录: ${tempRoot}`);

    try {
        const results = [];
        for (const pkg of packages) {
            const result = await processPackage(pkg, tempRoot);
            results.push(result);
        }

        console.log('\n完成。输出目录:', OUT_DIR);
        for (const r of results) {
            console.log(`  - ${r.name}: ${r.files} 个文件`);
        }
    } finally {
        await fs.rm(tempRoot, { recursive: true, force: true });
        console.log('已清理临时目录');
    }
}

main().catch((err) => {
    console.error(err);
    process.exit(1);
});
