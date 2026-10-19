import { build } from "esbuild";
import { copyFile } from "node:fs/promises";
await build({ entryPoints: ["ClientScripts/social.js"], outfile: "wwwroot/js/social/social.js",
    bundle: true, format: "esm", minify: true, target: ["es2022"], legalComments: "linked" });
await copyFile("ClientScripts/licenses/SIGNALR-LICENSE.txt", "wwwroot/js/social/SIGNALR-LICENSE.txt");
