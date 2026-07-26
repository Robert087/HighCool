import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const desktopRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("windows installer script fails early outside native Windows with PowerShell commands", async (t) => {
  if (process.platform === "win32") {
    t.skip("native Windows runs the real installer build");
    return;
  }

  const script = await readFile(resolve(desktopRoot, "scripts/build-windows-installer.mjs"), "utf8");

  assert.match(script, /process\.platform !== "win32"/);
  assert.match(script, /npm run desktop:build:windows/);
  assert.match(script, /Set-Location C:\\\\Path\\\\To\\\\HighCool\\\\src\\\\desktop/);
  assert.match(script, /x86_64-pc-windows-msvc/);
  assert.match(script, /--bundles", "nsis"/);
});

test("desktop package exposes repeatable Windows build scripts", async () => {
  const packageJson = JSON.parse(await readFile(resolve(desktopRoot, "package.json"), "utf8"));

  assert.equal(packageJson.scripts["publish:backend:windows"], "node scripts/publish-backend.mjs --runtime win-x64");
  assert.equal(packageJson.scripts["prepare:desktop:windows"], "npm run build:frontend && npm run publish:backend:windows");
  assert.equal(packageJson.scripts["desktop:build:windows"], "node scripts/build-windows-installer.mjs");
});
