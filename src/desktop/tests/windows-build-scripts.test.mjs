import assert from "node:assert/strict";
import { access, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import {
  defaultDesktopRoot,
  formatSpawnFailure,
  LaunchResolutionError,
  resolveLaunchSpec,
  resolveTauriCliPath,
  resolveTauriLaunchSpec,
} from "../scripts/command-utils.mjs";

const desktopRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repoRoot = resolve(desktopRoot, "../..");
const fakeNodeExecPath = process.execPath;
const fakeNpmExecPath = resolve(desktopRoot, "node_modules", "npm", "bin", "npm-cli.js");
const tauriCliEntry = resolve(desktopRoot, "node_modules", "@tauri-apps/cli", "tauri.js");

function win32LaunchEnv(overrides = {}) {
  return {
    platform: "win32",
    execPath: fakeNodeExecPath,
    cwd: repoRoot,
    desktopRoot,
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
  assert.match(script, /run\("tauri", \["build", "--config", bundleConfig/);
  assert.match(script, /resolveLaunchSpec\(command, args/);
  assert.match(script, /shell: false/);
  assert.doesNotMatch(script, /\bnpx\b/);
});

test("desktop package exposes repeatable Windows build scripts", async () => {
  const packageJson = JSON.parse(await readFile(resolve(desktopRoot, "package.json"), "utf8"));

  assert.equal(packageJson.scripts["publish:backend:windows"], "node scripts/publish-backend.mjs --runtime win-x64");
  assert.equal(packageJson.scripts["prepare:desktop:windows"], "npm run build:frontend && npm run publish:backend:windows");
  assert.equal(packageJson.scripts["desktop:build:windows"], "node scripts/build-windows-installer.mjs");
  assert.equal(packageJson.devDependencies["@tauri-apps/cli"], "2.11.4");
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

test("NSIS installer blocks while HighCool runtime processes are still running", async () => {
  const tauriConfig = JSON.parse(await readFile(resolve(desktopRoot, "src-tauri/tauri.conf.json"), "utf8"));
  const hooks = await readFile(resolve(desktopRoot, "src-tauri/windows/installer-hooks.nsh"), "utf8");

  assert.equal(tauriConfig.bundle.windows.nsis.installerHooks, "./windows/installer-hooks.nsh");
  assert.match(hooks, /NSIS_HOOK_PREINSTALL/);
  assert.match(hooks, /NSIS_HOOK_PREUNINSTALL/);
  assert.match(hooks, /\$\{MAINBINARYNAME\}\.exe/);
  assert.match(hooks, /ERP\.Api\.exe/);
  assert.match(hooks, /FindProcessCurrentUser/);
  assert.match(hooks, /MB_RETRYCANCEL/);
  assert.match(hooks, /Abort "HighCool must be closed before installation can continue\."/);
  assert.doesNotMatch(hooks, /KillProcess/);
  assert.doesNotMatch(hooks, /\btaskkill\b/i);
  assert.doesNotMatch(hooks, /\bdotnet\.exe\b/i);
});

test("tauri resolves through @tauri-apps/cli/tauri.js", () => {
  const launch = resolveTauriLaunchSpec(["--version"], {
    desktopRoot,
    execPath: fakeNodeExecPath,
  });

  assert.equal(launch.executable, fakeNodeExecPath);
  assert.equal(launch.args[0], tauriCliEntry);
  assert.deepEqual(launch.args, [tauriCliEntry, "--version"]);
  assert.equal(launch.resolution, "tauri-cli");
});

test("tauri resolution is independent of process.cwd()", () => {
  const originalCwd = process.cwd();

  try {
    process.chdir(repoRoot);
    const launch = resolveTauriLaunchSpec(["--version"], {
      desktopRoot,
      execPath: fakeNodeExecPath,
    });

    assert.equal(launch.args[0], tauriCliEntry);
    assert.notEqual(launch.args[0], resolve(repoRoot, "node_modules", "@tauri-apps", "cli", "tauri.js"));
  } finally {
    process.chdir(originalCwd);
  }
});

test("tauri resolution works when cwd is the repository root", () => {
  const launch = resolveLaunchSpec("tauri", ["--version"], win32LaunchEnv({ cwd: repoRoot }));

  assert.equal(launch.args[0], tauriCliEntry);
  assert.equal(launch.executable, fakeNodeExecPath);
});

test("tauri preserves arguments containing spaces as separate argv entries", () => {
  const spacedConfig = "C:\\Program Files\\HighCool\\tauri.bundle.conf.json";
  const launch = resolveTauriLaunchSpec(
    ["build", "--config", spacedConfig, "--target", "x86_64-pc-windows-msvc", "--bundles", "nsis"],
    { desktopRoot, execPath: fakeNodeExecPath },
  );

  assert.deepEqual(launch.args, [
    tauriCliEntry,
    "build",
    "--config",
    spacedConfig,
    "--target",
    "x86_64-pc-windows-msvc",
    "--bundles",
    "nsis",
  ]);
});

test("tauri never invokes npx.cmd or npx-cli.js", () => {
  const launch = resolveLaunchSpec(
    "tauri",
    ["build", "--bundles", "nsis"],
    win32LaunchEnv({ cwd: repoRoot }),
  );

  assert.notEqual(launch.executable, "npx.cmd");
  assert.notEqual(launch.args[0], "npx.cmd");
  assert.doesNotMatch(launch.args[0], /npx-cli\.js$/);
  assert.match(launch.args[0], /[\\/]@tauri-apps[\\/]cli[\\/]tauri\.js$/);
});

test("missing @tauri-apps/cli throws a deterministic error", async () => {
  const tempDesktopRoot = await mkdtemp(join(tmpdir(), "highcool-desktop-missing-tauri-"));
  await writeFile(
    join(tempDesktopRoot, "package.json"),
    JSON.stringify({ name: "highcool-desktop-test", private: true }),
  );

  try {
    assert.throws(
      () => resolveTauriCliPath(tempDesktopRoot),
      (error) => {
        assert.equal(error instanceof LaunchResolutionError, true);
        assert.match(error.message, /command: tauri/);
        assert.match(error.message, /desktop root:/);
        assert.match(error.message, /expected package: @tauri-apps\/cli/);
        assert.match(error.message, /package resolution error:/);
        assert.doesNotMatch(error.message, /npm_execpath/);
        return true;
      },
    );
  } finally {
    await rm(tempDesktopRoot, { recursive: true, force: true });
  }
});

test("resolveLaunchSpec rejects npx without falling back to npx-cli.js", () => {
  assert.throws(
    () => resolveLaunchSpec("npx", ["tauri", "--version"], win32LaunchEnv({ cwd: repoRoot })),
    (error) => {
      assert.equal(error instanceof LaunchResolutionError, true);
      assert.match(error.message, /npx is not supported/);
      assert.doesNotMatch(error.message, /npx-cli\.js/);
      return true;
    },
  );
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

test("defaultDesktopRoot matches the desktop package beside command-utils.mjs", () => {
  assert.equal(defaultDesktopRoot, desktopRoot);
});

function packageJsonScript(packageJsonContents, scriptName) {
  return JSON.parse(packageJsonContents).scripts[scriptName];
}
