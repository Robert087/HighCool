import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import { formatSpawnFailure, resolveExecutable } from "../scripts/command-utils.mjs";

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
  assert.match(script, /tauri\.bundle\.conf\.json/);
  assert.match(script, /"--config", bundleConfig/);
  assert.match(script, /--bundles", "nsis"/);
  assert.match(script, /resolveExecutable\(command\)/);
});

test("desktop package exposes repeatable Windows build scripts", async () => {
  const packageJson = JSON.parse(await readFile(resolve(desktopRoot, "package.json"), "utf8"));

  assert.equal(packageJson.scripts["publish:backend:windows"], "node scripts/publish-backend.mjs --runtime win-x64");
  assert.equal(packageJson.scripts["prepare:desktop:windows"], "npm run build:frontend && npm run publish:backend:windows");
  assert.equal(packageJson.scripts["desktop:build:windows"], "node scripts/build-windows-installer.mjs");
});

test("backend publish resource is only configured for bundle packaging", async () => {
  const tauriConfig = JSON.parse(await readFile(resolve(desktopRoot, "src-tauri/tauri.conf.json"), "utf8"));
  const bundleConfig = JSON.parse(await readFile(resolve(desktopRoot, "src-tauri/tauri.bundle.conf.json"), "utf8"));

  assert.equal(tauriConfig.bundle.resources, undefined);
  assert.deepEqual(bundleConfig.bundle.resources, {
    "../backend-publish": "backend-publish",
  });
  assert.match(packageJsonScript(await readFile(resolve(desktopRoot, "package.json"), "utf8"), "desktop:build"), /tauri\.bundle\.conf\.json/);
});

test("windows resource generation has a real ico and package metadata", async () => {
  const tauriConfig = JSON.parse(await readFile(resolve(desktopRoot, "src-tauri/tauri.conf.json"), "utf8"));
  const cargoToml = await readFile(resolve(desktopRoot, "src-tauri/Cargo.toml"), "utf8");

  assert.deepEqual(tauriConfig.bundle.icon, ["icons/icon.ico", "icons/icon.png"]);
  await access(resolve(desktopRoot, "src-tauri/icons/icon.ico"));
  assert.match(cargoToml, /\[package\.metadata\.tauri-winres\]/);
  assert.match(cargoToml, /OriginalFilename = "HighCool\.exe"/);
});

test("installer runner resolves npm and npx through Windows command shims", () => {
  assert.equal(resolveExecutable("npm", "win32"), "npm.cmd");
  assert.equal(resolveExecutable("npx", "win32"), "npx.cmd");
  assert.equal(resolveExecutable("npm", "linux"), "npm");
  assert.equal(resolveExecutable("dotnet", "win32"), "dotnet");
  assert.equal(resolveExecutable("cargo", "win32"), "cargo");
});

test("installer runner reports spawn errors with command diagnostics", () => {
  const message = formatSpawnFailure({
    label: "npm",
    command: "npm",
    executable: "npm.cmd",
    args: ["--version"],
    result: {
      status: null,
      signal: null,
      error: Object.assign(new Error("spawn npm ENOENT"), { code: "ENOENT" }),
    },
  });

  assert.match(message, /command: npm/);
  assert.match(message, /executable: npm\.cmd/);
  assert.match(message, /arguments: \["--version"\]/);
  assert.match(message, /status: null/);
  assert.match(message, /signal: null/);
  assert.match(message, /error\.message: spawn npm ENOENT/);
  assert.match(message, /error\.code: ENOENT/);
});

function packageJsonScript(packageJsonContents, scriptName) {
  return JSON.parse(packageJsonContents).scripts[scriptName];
}
