import { build } from "esbuild";
import { copyFile } from "node:fs/promises";

await build({
    entryPoints: { "code-editor": "ClientScripts/code-editor.js",
        "editor.worker": "node_modules/monaco-editor/esm/vs/editor/editor.worker.js" },
    outdir: "wwwroot/js/editor",
    bundle: true,
    format: "esm",
    splitting: true,
    minify: true,
    target: ["es2022"],
    loader: { ".ttf": "file" },
    legalComments: "linked"
});
await copyFile("node_modules/monaco-editor/LICENSE", "wwwroot/js/editor/MONACO-LICENSE.txt");
