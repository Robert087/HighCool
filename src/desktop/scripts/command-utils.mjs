export function resolveExecutable(command, platform = process.platform) {
  if (platform === "win32") {
    if (command === "npm") {
      return "npm.cmd";
    }

    if (command === "npx") {
      return "npx.cmd";
    }
  }

  return command;
}

export function formatSpawnFailure({ label, command, executable, args, result }) {
  const details = [
    `${label} failed.`,
    `command: ${command}`,
    `executable: ${executable}`,
    `arguments: ${JSON.stringify(args)}`,
    `status: ${result.status}`,
    `signal: ${result.signal}`,
  ];

  if (result.error) {
    details.push(`error.message: ${result.error.message}`);
    details.push(`error.code: ${result.error.code ?? ""}`);
  }

  return details.join("\n");
}
