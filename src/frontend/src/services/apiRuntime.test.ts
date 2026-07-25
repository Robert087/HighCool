import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, requestJson } from "./api";
import {
  isAllowedLoopbackApiOrigin,
  resetDesktopRuntimeInfo,
  resolveDesktopRuntimeInfo,
  verifyDesktopBackendHealth,
} from "./apiRuntime";

function stubStorage() {
  const storage = {
    getItem: vi.fn(() => null),
    setItem: vi.fn(),
    removeItem: vi.fn(),
  };

  vi.stubGlobal("localStorage", storage);
  return storage;
}

describe("apiRuntime", () => {
  afterEach(() => {
    resetDesktopRuntimeInfo();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("accepts only explicit loopback HTTP API origins", () => {
    expect(isAllowedLoopbackApiOrigin("http://127.0.0.1:17600")).toBe(true);
    expect(isAllowedLoopbackApiOrigin("http://localhost:17600")).toBe(true);
    expect(isAllowedLoopbackApiOrigin("http://127.0.0.1")).toBe(false);
    expect(isAllowedLoopbackApiOrigin("https://127.0.0.1:17600")).toBe(false);
    expect(isAllowedLoopbackApiOrigin("http://example.com:17600")).toBe(false);
  });

  it("resolves the desktop runtime origin before making API requests", async () => {
    const events: string[] = [];
    const invoke = vi.fn(async () => {
      events.push("invoke");
      return {
        apiOrigin: "http://127.0.0.1:17642",
        healthUrl: "http://127.0.0.1:17642/health",
        desktopMode: true,
      };
    });
    const fetchMock = vi.fn(async () => {
      events.push("fetch");
      return {
        ok: true,
        status: 200,
        headers: new Headers({ "content-type": "application/json" }),
        json: async () => ({ ok: true }),
      };
    });

    vi.stubGlobal("window", {
      __TAURI__: { core: { invoke } },
      localStorage: stubStorage(),
    });
    vi.stubGlobal("fetch", fetchMock);

    await requestJson<{ ok: boolean }>("/api/auth/me");

    expect(events).toEqual(["invoke", "fetch"]);
    expect((fetchMock.mock.calls[0] as unknown[] | undefined)?.[0]).toBe("http://127.0.0.1:17642/api/auth/me");
    expect(invoke).toHaveBeenCalledWith("get_backend_runtime_info");
  });

  it("does not fall back to hardcoded 5080 when a desktop runtime origin exists", async () => {
    const invoke = vi.fn(async () => ({
      apiOrigin: "http://127.0.0.1:17677",
      healthUrl: "http://127.0.0.1:17677/health",
      desktopMode: true,
    }));
    const fetchMock = vi.fn(async () => {
      throw new TypeError("network down");
    });

    vi.stubGlobal("window", {
      __TAURI__: { core: { invoke } },
      location: { port: "5173", protocol: "http:" },
      localStorage: stubStorage(),
    });
    vi.stubGlobal("fetch", fetchMock);

    await expect(requestJson("/api/auth/me")).rejects.toThrow(ApiError);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect((fetchMock.mock.calls[0] as unknown[] | undefined)?.[0]).toBe("http://127.0.0.1:17677/api/auth/me");
  });

  it("keeps the existing browser development API behavior", async () => {
    const fetchMock = vi.fn(async () => ({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => ({ ok: true }),
    }));

    vi.stubGlobal("window", {
      location: {
        port: "5173",
        protocol: "http:",
      },
      localStorage: stubStorage(),
    });
    vi.stubGlobal("fetch", fetchMock);

    await requestJson<{ ok: boolean }>("/api/auth/me");

    expect((fetchMock.mock.calls[0] as unknown[] | undefined)?.[0]).toBe("/api/auth/me");
  });

  it("rejects unsafe runtime information before fetch", async () => {
    const invoke = vi.fn(async () => ({
      apiOrigin: "https://example.com",
      healthUrl: "https://example.com/health",
      desktopMode: true,
    }));
    const fetchMock = vi.fn();

    vi.stubGlobal("window", {
      __TAURI__: { core: { invoke } },
      localStorage: stubStorage(),
    });
    vi.stubGlobal("fetch", fetchMock);

    await expect(resolveDesktopRuntimeInfo()).rejects.toThrow("safe loopback");

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("re-resolves runtime information after a failed attempt", async () => {
    const invoke = vi
      .fn()
      .mockRejectedValueOnce(new Error("not ready"))
      .mockResolvedValueOnce({
        apiOrigin: "http://127.0.0.1:17651",
        healthUrl: "http://127.0.0.1:17651/health",
        desktopMode: true,
      });

    vi.stubGlobal("window", {
      __TAURI__: { core: { invoke } },
      localStorage: stubStorage(),
    });

    await expect(resolveDesktopRuntimeInfo()).rejects.toThrow("not ready");
    await expect(resolveDesktopRuntimeInfo()).resolves.toMatchObject({
      apiOrigin: "http://127.0.0.1:17651",
    });
    expect(invoke).toHaveBeenCalledTimes(2);
  });

  it("surfaces backend health failure without exposing secrets", async () => {
    const fetchMock = vi.fn(async () => ({
      ok: false,
      status: 503,
      headers: new Headers({ "content-type": "text/plain" }),
      text: async () => "JwtSecret=secret",
    }));

    vi.stubGlobal("fetch", fetchMock);

    await expect(verifyDesktopBackendHealth({
      apiOrigin: "http://127.0.0.1:17600",
      healthUrl: "http://127.0.0.1:17600/health",
      desktopMode: true,
    })).rejects.toThrow("503");
  });
});
