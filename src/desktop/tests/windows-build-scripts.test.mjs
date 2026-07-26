import assert from "node:assert/strict";
import { access, readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import {
  formatSpawnFailure,
  LaunchResolutionError,
  resolveLaunchSpec,
} from "../scripts/command-utils.mjs";

const desktopRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const fakeNodeExecPath = process.execPath;
const fakeNpmExecPath = resolve(desktopRoot, "node_modules", "npm", "bin", "npm-cli.js");
const fakeNpxExecPath = resolve(desktopRoot, "node_modules", "npm", "bin", "npx-cli.js");
const tauriCliEntry = resolve(desktopRoot, "node_modules", "@tauri-apps/cli", "tauri.js");

function win32LaunchEnv(overrides = {}) {
  return {
    platform: "win32",
    execPath: fakeNodeExecPath,
    cwd: desktopRoot,
    env: {
      npm_execpath: fakeNpmExecPath,
      ...overrides.env,
    },
    ...overrides,
  };
}

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
  assert.match(script, /resolveLaunchSpec\(command, args/);
  assert.match(script, /shell: false/);
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

test("npm resolves to node executable plus npm_execpath on Windows", () => {
  const launch = resolveLaunchSpec("npm", ["--version"], win32LaunchEnv());

  assert.equal(launch.executable, fakeNodeExecPath);
  assert.deepEqual(launch.args, [fakeNpmExecPath, "--version"]);
  assert.equal(launch.npmExecPathPresent, true);
  assert.equal(launch.resolution, "npm-cli");
});

test("npm preserves original arguments in order on Windows", () => {
  const launch = resolveLaunchSpec("npm", ["run", "build:frontend"], win32LaunchEnv());

  assert.deepEqual(launch.args, [fakeNpmExecPath, "run", "build:frontend"]);
});

test("npm preserves arguments containing spaces as separate argv entries", () => {
  const spacedConfig = "C:\\Program Files\\HighCool\\tauri.bundle.conf.json";
  const launch = resolveLaunchSpec(
    "npm",
    ["run", "build:frontend", "--", "--config", spacedConfig],
    win32LaunchEnv(),
  );

  assert.deepEqual(launch.args, [fakeNpmExecPath, "run", "build:frontend", "--", "--config", spacedConfig]);
  assert.notEqual(launch.args.at(-1), launch.args.slice(-2).join(" "));
});

test("npx resolves through local package CLI JS entry instead of npx.cmd", () => {
  const launch = resolveLaunchSpec("npx", ["tauri", "--version"], win32LaunchEnv());

  assert.equal(launch.executable, fakeNodeExecPath);
  assert.deepEqual(launch.args, [tauriCliEntry, "--version"]);
  assert.notEqual(launch.executable, "npx.cmd");
  assert.notEqual(launch.args[0], "npx.cmd");
  assert.equal(launch.resolution, "local-package-cli");
});

test("native commands remain unchanged on Windows", () => {
  for (const command of ["node", "dotnet", "cargo", "rustc", "rustup"]) {
    const launch = resolveLaunchSpec(command, ["--version"], win32LaunchEnv());
    assert.equal(launch.executable, command);
    assert.deepEqual(launch.args, ["--version"]);
    assert.equal(launch.npmExecPathPresent, undefined);
  }
});

test("missing npm_execpath produces a clear deterministic npm launch error", () => {
  assert.throws(
    () => resolveLaunchSpec("npm", ["--version"], win32LaunchEnv({ env: { npm_execpath: "" } })),
    (error) => {
      assert.equal(error instanceof LaunchResolutionError, true);
      assert.match(error.message, /npm launch failed/);
      assert.match(error.message, /npm_execpath is missing or empty/);
      return true;
    },
  );
});

test("spawn EINVAL diagnostics remain visible in failure output", () => {
  const message = formatSpawnFailure({
    label: "npm",
    command: "npm",
    executable: fakeNodeExecPath,
    args: [fakeNpmExecPath, "--version"],
    cwd: "C:\\actions-runner\\work\\HighCool\\HighCool\\src\\desktop",
    npmExecPathPresent: true,
    result: {
      status: null,
      signal: null,
      error: Object.assign(new Error("spawnSync npm.cmd EINVAL"), { code: "EINVAL" }),
    },
  });

  assert.match(message, /command: npm/);
  assert.match(message, /executable: /);
  assert.match(message, /arguments: \[/);
  assert.match(message, /cwd: C:\\actions-runner\\work\\HighCool\\HighCool\\src\\desktop/);
  assert.match(message, /npm_execpath present: true/);
  assert.match(message, /status: null/);
  assert.match(message, /signal: null/);
  assert.match(message, /error\.message: spawnSync npm\.cmd EINVAL/);
  assert.match(message, /error\.code: EINVAL/);
});

test("installer runner launch resolution does not require shell execution", async () => {
  const script = await readFile(resolve(desktopRoot, "scripts/build-windows-installer.mjs"), "utf8");

  assert.doesNotMatch(script, /shell:\s*true/);
  assert.match(script, /shell:\s*false/);
});

function packageJsonScript(packageJsonContents, scriptName) {
  return JSON.parse(packageJsonContents).scripts[scriptName];
}
