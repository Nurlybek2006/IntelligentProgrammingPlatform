import { readFile } from "node:fs/promises";

// Monaco 0.57.0 has two stylesheet factories but no nonce option. Adapt those
// factories at build time, before insertion, without modifying node_modules or
// globally blessing arbitrary style elements. Upgrades must pass these guards.
export function addStyleNonce(source) {
    const marker = "const style = document.createElement('style');";
    if (source.split(marker).length !== 2) {
        throw new Error("Monaco stylesheet factory changed; review the CSP adapter before upgrading.");
    }
    return source.replace(marker, marker + '\nstyle.nonce = document.querySelector(\'meta[name="csp-nonce"]\')?.content ?? "";');
}

export const monacoCspPlugin = {
    name: "monaco-style-nonce",
    async setup(build) {
        const { version } = JSON.parse(await readFile("node_modules/monaco-editor/package.json", "utf8"));
        if (version !== "0.57.0") throw new Error("Review Monaco CSP integration before changing its version.");
        build.onLoad({ filter: /[\\/]monaco-editor[\\/]esm[\\/]vs[\\/]base[\\/]browser[\\/](domStylesheets|ui[\\/]contextview[\\/]contextview)\.js$/ }, async ({ path }) => ({
            contents: addStyleNonce(await readFile(path, "utf8")), loader: "js"
        }));
    }
};
