export interface DesktopRuntimeInfo {
  apiOrigin: string;
  healthUrl: string;
  desktopMode: boolean;
}

type TauriInvoke = <T>(command: string, args?: Record<string, unknown>) => Promise<T>;

interface TauriGlobal {
  core?: {
    invoke?: TauriInvoke;
  };
}

declare global {
  interface Window {
    __TAURI__?: TauriGlobal;
    __TAURI_INTERNALS__?: {
      invoke?: TauriInvoke;
    };
  }
}

let runtimeInfoPromise: Promise<DesktopRuntimeInfo | null> | null = null;

export function isAllowedLoopbackApiOrigin(value: string) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" &&
      url.port.length > 0 &&
      (url.hostname === "127.0.0.1" || url.hostname === "localhost" || url.hostname === "::1" || url.hostname === "[::1]");
  } catch {
    return false;
  }
}

export function getTauriInvoke(): TauriInvoke | null {
  if (typeof window === "undefined") {
    return null;
  }

  return window.__TAURI__?.core?.invoke ?? window.__TAURI_INTERNALS__?.invoke ?? null;
}

export function isDesktopRuntime() {
  return getTauriInvoke() !== null;
}

export function resetDesktopRuntimeInfo() {
  runtimeInfoPromise = null;
}

export async function resolveDesktopRuntimeInfo() {
  const invoke = getTauriInvoke();
  if (!invoke) {
    return null;
  }

  if (!runtimeInfoPromise) {
    runtimeInfoPromise = invoke<DesktopRuntimeInfo>("get_backend_runtime_info")
      .then((info) => {
        if (!info?.desktopMode || !isAllowedLoopbackApiOrigin(info.apiOrigin)) {
          throw new Error("Desktop backend origin is not a safe loopback HTTP origin.");
        }

        const expectedHealthUrl = `${info.apiOrigin.replace(/\/$/, "")}/health`;
        if (info.healthUrl !== expectedHealthUrl) {
          throw new Error("Desktop backend health URL does not match the API origin.");
        }

        return {
          apiOrigin: info.apiOrigin.replace(/\/$/, ""),
          healthUrl: info.healthUrl,
          desktopMode: true,
        };
      })
      .catch((error) => {
        runtimeInfoPromise = null;
        throw error;
      });
  }

  return runtimeInfoPromise;
}

export async function verifyDesktopBackendHealth(info: DesktopRuntimeInfo) {
  const response = await fetch(info.healthUrl, {
    headers: { Accept: "text/plain" },
  });

  if (!response.ok) {
    throw new Error(`Local backend health check failed with status ${response.status}.`);
  }
}
