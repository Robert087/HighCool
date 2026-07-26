import { existsSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const commandUtilsDirectory = dirname(fileURLToPath(import.meta.url));
export const defaultDesktopRoot = resolve(commandUtilsDirectory, "..");

export class LaunchResolutionError extends Error {
  constructor(message) {
    super(message);
    this.name = "LaunchResolutionError";
  }
}

export function resolveLaunchSpec(command, args, options = {}) {
  const platform = options.platform ?? process.platform;
  const env = options.env ?? process.env;
  const execPath = options.execPath ?? process.execPath;
  const desktopRoot = options.desktopRoot ?? defaultDesktopRoot;

  if (command === "npm") {
    return resolveNpmLaunchSpec(command, args, execPath, env);
  }

  if (command === "tauri") {
    return resolveTauriLaunchSpec(args, { execPath, desktopRoot });
  }

  if (command === "npx") {
    throw new LaunchResolutionError(
      "npx is not supported. Resolve local CLI packages directly, for example @tauri-apps/cli/tauri.js.",
    );
  }

  if (platform === "win32") {
    return {
      command,
      executable: command,
      args: [...args],
    };
  }

  return {
    command,
    executable: command,
    args: [...args],
  };
}

export function resolveTauriCliPath(desktopRoot = defaultDesktopRoot) {
  const packageName = "@tauri-apps/cli";
  const requireFromDesktop = createRequire(resolve(desktopRoot, "package.json"));

  let packageJsonPath;
  try {
    packageJsonPath = requireFromDesktop.resolve(`${packageName}/package.json`);
  } catch (error) {
    throw new LaunchResolutionError(
      [
        "tauri launch failed: could not resolve @tauri-apps/cli from the desktop package dependency tree.",
        "command: tauri",
        `desktop root: ${desktopRoot}`,
        `expected package: ${packageName}`,
        `package resolution error: ${error.message}`,
      ].join("\n"),
    );
  }

  const packageDirectory = dirname(packageJsonPath);
  const tauriCliPath = join(packageDirectory, "tauri.js");
  if (!existsSync(tauriCliPath)) {
    throw new LaunchResolutionError(
      [
        "tauri launch failed: @tauri-apps/cli resolved but tauri.js was not found.",
        "command: tauri",
        `desktop root: ${desktopRoot}`,
        `expected package: ${packageName}`,
        `resolved package directory: ${packageDirectory}`,
        `expected cli entry: ${tauriCliPath}`,
      ].join("\n"),
    );
  }

  return tauriCliPath;
}

export function resolveTauriLaunchSpec(args, options = {}) {
  const execPath = options.execPath ?? process.execPath;
  const desktopRoot = options.desktopRoot ?? defaultDesktopRoot;
  const tauriCliPath = resolveTauriCliPath(desktopRoot);

  return {
    command: "tauri",
    executable: execPath,
    args: [tauriCliPath, ...args],
    resolution: "tauri-cli",
  };
}

function resolveNpmLaunchSpec(command, args, execPath, env) {
  const npmExecPath = env.npm_execpath;
  if (!npmExecPath || npmExecPath.trim() === "") {
    throw new LaunchResolutionError(
      "npm launch failed: process.env.npm_execpath is missing or empty. Run npm commands through `npm run` so npm sets npm_execpath.",
    );
  }

  return {
    command,
    executable: execPath,
    args: [npmExecPath, ...args],
    npmExecPathPresent: true,
    resolution: "npm-cli",
  };
}

export function formatSpawnFailure({ label, command, executable, args, result, cwd, npmExecPathPresent }) {
  const details = [
    `${label} failed.`,
    `command: ${command}`,
    `executable: ${executable}`,
    `arguments: ${JSON.stringify(args)}`,
  ];

  if (cwd !== undefined) {
    details.push(`cwd: ${cwd}`);
  }

  if (npmExecPathPresent !== undefined) {
    details.push(`npm_execpath present: ${npmExecPathPresent}`);
  }

  details.push(`status: ${result.status}`);
  details.push(`signal: ${result.signal}`);

  if (result.error) {
    details.push(`error.message: ${result.error.message}`);
    details.push(`error.code: ${result.error.code ?? ""}`);
  }

  return details.join("\n");
}
