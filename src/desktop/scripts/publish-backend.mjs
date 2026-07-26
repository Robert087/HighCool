import { mkdir, rm, cp } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const supportedRuntimes = new Set(["linux-x64", "win-x64", "osx-arm64", "osx-x64"]);
const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const desktopRoot = resolve(scriptDirectory, "..");
const repoRoot = resolve(desktopRoot, "../..");
const frontendDist = resolve(repoRoot, "src/frontend/dist");
const apiProject = resolve(repoRoot, "src/backend/Api/ERP.Api.csproj");
const apiWwwroot = resolve(repoRoot, "src/backend/Api/wwwroot");
const tauriAssets = resolve(desktopRoot, "src-tauri/assets");
const runtime = parseRuntime();
const backendPublishRoot = resolve(desktopRoot, "backend-publish");
const publishRoot = resolve(backendPublishRoot, runtime);

if (!supportedRuntimes.has(runtime)) {
  throw new Error(`Unsupported desktop runtime '${runtime}'. Supported runtimes: ${[...supportedRuntimes].join(", ")}`);
}

if (!existsSync(frontendDist)) {
  throw new Error("Frontend dist was not found. Run npm run build:frontend first.");
}

await rm(apiWwwroot, { recursive: true, force: true });
await mkdir(apiWwwroot, { recursive: true });
await cp(frontendDist, apiWwwroot, { recursive: true });

await rm(resolve(tauriAssets, "index.html"), { force: true });
await rm(resolve(tauriAssets, "assets"), { recursive: true, force: true });
await cp(resolve(frontendDist, "index.html"), resolve(tauriAssets, "index.html"));
await cp(resolve(frontendDist, "assets"), resolve(tauriAssets, "assets"), { recursive: true });

await rm(backendPublishRoot, { recursive: true, force: true });
await mkdir(publishRoot, { recursive: true });

const publishArgs = [
  "publish",
  apiProject,
  "-c",
  "Release",
  "-r",
  runtime,
  "--self-contained",
  "true",
  "-p:PublishTrimmed=false",
  "-p:PublishSingleFile=false",
  "-p:DebugType=None",
  "-p:DebugSymbols=false",
  "-o",
  publishRoot,
];

const result = spawnSync("dotnet", publishArgs, {
  cwd: repoRoot,
  stdio: "inherit",
  env: {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Desktop",
  },
});

if (result.status !== 0) {
  throw new Error(`dotnet publish failed with exit code ${result.status}`);
}

console.log(`Published HighCool desktop backend to ${publishRoot}`);

function parseRuntime() {
  const runtimeArgIndex = process.argv.indexOf("--runtime");
  if (runtimeArgIndex >= 0) {
    const value = process.argv[runtimeArgIndex + 1];
    if (!value || value.startsWith("--")) {
      throw new Error("Missing runtime after --runtime.");
    }

    return value;
  }

  return process.env.HIGHCOOL_DESKTOP_RUNTIME ?? detectRuntime();
}

function detectRuntime() {
  if (process.platform === "linux" && process.arch === "x64") {
    return "linux-x64";
  }

  if (process.platform === "win32" && process.arch === "x64") {
    return "win-x64";
  }

  if (process.platform === "darwin" && process.arch === "arm64") {
    return "osx-arm64";
  }

  if (process.platform === "darwin" && process.arch === "x64") {
    return "osx-x64";
  }

  throw new Error(`Unsupported desktop runtime for ${process.platform}/${process.arch}. Set HIGHCOOL_DESKTOP_RUNTIME explicitly.`);
}
