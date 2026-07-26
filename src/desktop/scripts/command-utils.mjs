import { existsSync, readFileSync, readdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";

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
  const cwd = options.cwd ?? process.cwd();

  if (command === "npm") {
    return resolveNpmLaunchSpec(command, args, execPath, env);
  }

  if (command === "npx") {
    return resolveNpxLaunchSpec(command, args, execPath, env, cwd);
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

function resolveNpxLaunchSpec(command, args, execPath, env, cwd) {
  const [toolName, ...toolArgs] = args;
  if (!toolName) {
    throw new LaunchResolutionError("npx launch failed: a tool name is required.");
  }

  const localCliEntry = findLocalPackageBinJs(toolName, cwd);
  if (localCliEntry) {
    return {
      command,
      executable: execPath,
      args: [localCliEntry, ...toolArgs],
      npmExecPathPresent: Boolean(env.npm_execpath?.trim()),
      resolution: "local-package-cli",
    };
  }

  const npmExecPath = env.npm_execpath;
  if (!npmExecPath || npmExecPath.trim() === "") {
    throw new LaunchResolutionError(
      "npx launch failed: process.env.npm_execpath is missing or empty and no local package CLI entry was found.",
    );
  }

  const npxCliEntry = join(dirname(npmExecPath), "npx-cli.js");
  if (!existsSync(npxCliEntry)) {
    throw new LaunchResolutionError(
      `npx launch failed: npx-cli.js was not found next to npm_execpath at ${npxCliEntry}.`,
    );
  }

  return {
    command,
    executable: execPath,
    args: [npxCliEntry, ...args],
    npmExecPathPresent: true,
    resolution: "npx-cli",
  };
}

function findLocalPackageBinJs(toolName, cwd) {
  const nodeModules = resolve(cwd, "node_modules");
  if (!existsSync(nodeModules)) {
    return null;
  }

  const directPackageBin = readPackageBinEntry(resolve(nodeModules, toolName), toolName);
  if (directPackageBin) {
    return directPackageBin;
  }

  for (const entry of readdirSync(nodeModules, { withFileTypes: true })) {
    if (entry.name.startsWith(".")) {
      continue;
    }

    const packageDirectory = resolve(nodeModules, entry.name);
    if (entry.isDirectory() && entry.name.startsWith("@")) {
      for (const scopedEntry of readdirSync(packageDirectory, { withFileTypes: true })) {
        if (!scopedEntry.isDirectory()) {
          continue;
        }

        const scopedBin = readPackageBinEntry(resolve(packageDirectory, scopedEntry.name), toolName);
        if (scopedBin) {
          return scopedBin;
        }
      }
      continue;
    }

    if (entry.isDirectory()) {
      const packageBin = readPackageBinEntry(packageDirectory, toolName);
      if (packageBin) {
        return packageBin;
      }
    }
  }

  return null;
}

function readPackageBinEntry(packageDirectory, toolName) {
  const packageJsonPath = join(packageDirectory, "package.json");
  if (!existsSync(packageJsonPath)) {
    return null;
  }

  const packageJson = JSON.parse(readFileSync(packageJsonPath, "utf8"));
  const binField = packageJson.bin;
  if (!binField) {
    return null;
  }

  let relativeBinPath;
  if (typeof binField === "string") {
    relativeBinPath = binField;
  } else if (typeof binField === "object" && binField[toolName]) {
    relativeBinPath = binField[toolName];
  } else {
    return null;
  }

  const absoluteBinPath = resolve(packageDirectory, relativeBinPath);
  return existsSync(absoluteBinPath) ? absoluteBinPath : null;
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
