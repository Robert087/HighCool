import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const desktopRoot = resolve(scriptDirectory, "..");

const packageJsonPath = resolve(desktopRoot, "package.json");
const tauriConfigPath = resolve(desktopRoot, "src-tauri/tauri.conf.json");
const cargoTomlPath = resolve(desktopRoot, "src-tauri/Cargo.toml");

const packageJson = JSON.parse(await readFile(packageJsonPath, "utf8"));
const tauriConfig = JSON.parse(await readFile(tauriConfigPath, "utf8"));
const cargoToml = await readFile(cargoTomlPath, "utf8");
const cargoVersion = cargoToml.match(/^version\s*=\s*"([^"]+)"/m)?.[1];

const versions = {
  "package.json": packageJson.version,
  "src-tauri/tauri.conf.json": tauriConfig.version,
  "src-tauri/Cargo.toml": cargoVersion,
};

const uniqueVersions = new Set(Object.values(versions));
if (uniqueVersions.size !== 1 || uniqueVersions.has(undefined)) {
  const details = Object.entries(versions)
    .map(([file, version]) => `${file}: ${version ?? "<missing>"}`)
    .join("\n");
  throw new Error(`HighCool desktop version mismatch:\n${details}`);
}

console.log(`HighCool desktop versions are synchronized at ${packageJson.version}`);
