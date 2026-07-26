import { rm } from "node:fs/promises";
import { existsSync, readdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";
import { formatSpawnFailure, LaunchResolutionError, resolveLaunchSpec } from "./command-utils.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const desktopRoot = resolve(scriptDirectory, "..");
const repoRoot = resolve(desktopRoot, "../..");
const frontendRoot = resolve(desktopRoot, "../frontend");
const backendRoot = resolve(desktopRoot, "../backend");
const srcTauriRoot = resolve(desktopRoot, "src-tauri");
const artifactsRoot = resolve(repoRoot, "artifacts");
const windowsTarget = "x86_64-pc-windows-msvc";
const bundleConfig = resolve(srcTauriRoot, "tauri.bundle.conf.json");

if (process.platform !== "win32") {
  console.error("HighCool Windows installer builds must run on native Windows with the MSVC Rust target.");
  console.error("");
  console.error("Run these commands in PowerShell on Windows 10/11 x64:");
  console.error("  Set-Location C:\\Path\\To\\HighCool\\src\\desktop");
  console.error("  npm install");
  console.error("  npm run desktop:build:windows");
  process.exit(1);
}

run("node", ["--version"], { cwd: desktopRoot, label: "Node.js" });
run("npm", ["--version"], { cwd: desktopRoot, label: "npm" });
run("dotnet", ["--info"], { cwd: backendRoot, label: ".NET SDK" });
run("rustc", ["--version"], { cwd: desktopRoot, label: "rustc" });
run("cargo", ["--version"], { cwd: desktopRoot, label: "cargo" });
run("rustup", ["target", "list", "--installed"], {
  cwd: desktopRoot,
  label: "Rust installed targets",
  validateOutput: (output) => output.includes(windowsTarget),
  failureMessage: `Rust target ${windowsTarget} is not installed. Run: rustup target add ${windowsTarget}`,
});
run("tauri", ["--version"], { cwd: desktopRoot, label: "Tauri CLI" });
run("npm", ["run", "check:versions"], { cwd: desktopRoot, label: "desktop version check" });

await cleanGeneratedWindowsBundles();

run("npm", ["run", "build:frontend"], { cwd: desktopRoot, label: "frontend production build" });
run("npm", ["run", "publish:backend:windows"], { cwd: desktopRoot, label: "win-x64 backend publish" });
run("tauri", ["build", "--config", bundleConfig, "--target", windowsTarget, "--bundles", "nsis"], {
  cwd: desktopRoot,
  label: "Tauri NSIS bundle",
});

const artifacts = findNsisInstallers();
if (artifacts.length === 0) {
  throw new Error("Tauri finished without producing an NSIS installer executable.");
}

console.log("HighCool Windows installer artifact(s):");
for (const artifact of artifacts) {
  console.log(`  ${artifact}`);
}

async function cleanGeneratedWindowsBundles() {
  const candidates = [
    resolve(desktopRoot, "backend-publish"),
    resolve(desktopRoot, "target", windowsTarget, "release", "bundle", "nsis"),
    resolve(srcTauriRoot, "target", windowsTarget, "release", "bundle", "nsis"),
  ];

  for (const candidate of candidates) {
    await rm(candidate, { recursive: true, force: true });
  }
}

function findNsisInstallers() {
  const roots = [
    resolve(desktopRoot, "target", windowsTarget, "release", "bundle", "nsis"),
    resolve(srcTauriRoot, "target", windowsTarget, "release", "bundle", "nsis"),
  ];

  return roots.flatMap((root) => collectExeFiles(root));
}

function collectExeFiles(root) {
  if (!existsSync(root)) {
    return [];
  }

  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = resolve(root, entry.name);
    if (entry.isDirectory()) {
      return collectExeFiles(path);
    }

    return entry.isFile() && entry.name.toLowerCase().endsWith(".exe") ? [path] : [];
  });
}

function run(command, args, options = {}) {
  const label = options.label ?? command;
  const cwd = options.cwd ?? desktopRoot;
  let launch;

  try {
    launch = resolveLaunchSpec(command, args, {
      cwd,
      env: process.env,
      desktopRoot,
    });
  } catch (error) {
    if (error instanceof LaunchResolutionError) {
      throw error;
    }

    throw new Error(`${label} launch resolution failed: ${error.message}`);
  }

  const result = spawnSync(launch.executable, launch.args, {
    cwd,
    stdio: options.validateOutput ? "pipe" : "inherit",
    encoding: options.validateOutput ? "utf8" : undefined,
    env: process.env,
    shell: false,
  });

  const output = `${result.stdout ?? ""}${result.stderr ?? ""}`;
  if (result.status !== 0 || result.error) {
    throw new Error(
      formatSpawnFailure({
        label,
        command,
        executable: launch.executable,
        args: launch.args,
        result,
        cwd,
        npmExecPathPresent: launch.npmExecPathPresent,
      }),
    );
  }

  if (options.validateOutput && !options.validateOutput(output)) {
    throw new Error(options.failureMessage ?? `${label} prerequisite validation failed.`);
  }

  if (options.validateOutput && output.trim().length > 0) {
    console.log(output.trim());
  }
}
