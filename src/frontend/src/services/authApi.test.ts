import { afterEach, describe, expect, it, vi } from "vitest";
import { signup, verifyEmail } from "./authApi";

describe("authApi", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("calls the backend signup route with the expected payload", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({ "content-type": "application/json" }),
      json: async () => ({
        accessToken: "token",
        expiresAt: new Date().toISOString(),
        workspace: {
          userId: "u1",
          fullName: "Owner",
          email: "owner@test.local",
          emailVerified: false,
          organizationId: "o1",
          organizationName: "Org",
          membershipId: "m1",
          requiresTwoFactor: false,
          permissions: [],
          organizations: [],
          roles: [],
        },
      }),
    });

    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
    });
    vi.stubGlobal("window", {
      location: {
        origin: "http://localhost:5173",
        hostname: "localhost",
        protocol: "http:",
      },
      localStorage: {
        getItem: vi.fn(() => null),
        setItem: vi.fn(),
        removeItem: vi.fn(),
      },
    });

    await signup({
      fullName: "Owner User",
      email: "owner@test.local",
      password: "StrongPass!123",
      organizationName: "North Org",
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/api/auth/signup");
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      method: "POST",
      body: JSON.stringify({
        fullName: "Owner User",
        email: "owner@test.local",
        password: "StrongPass!123",
        organizationName: "North Org",
      }),
    });
  });

  it("retries the backend directly when the dev origin returns 404", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({
        ok: false,
        status: 404,
        statusText: "Not Found",
        headers: new Headers({ "content-type": "text/plain" }),
        text: async () => "Not Found",
      })
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        headers: new Headers({ "content-type": "application/json" }),
        json: async () => ({
          accessToken: "token",
          expiresAt: new Date().toISOString(),
          workspace: {
            userId: "u1",
            fullName: "Owner",
            email: "owner@test.local",
            emailVerified: false,
            organizationId: "o1",
            organizationName: "Org",
            membershipId: "m1",
            requiresTwoFactor: false,
            permissions: [],
            organizations: [],
            roles: [],
          },
        }),
      });

    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
    });
    vi.stubGlobal("window", {
      location: {
        origin: "http://localhost:5173",
        hostname: "localhost",
        port: "5173",
        protocol: "http:",
      },
      localStorage: {
        getItem: vi.fn(() => null),
        setItem: vi.fn(),
        removeItem: vi.fn(),
      },
    });

    await signup({
      fullName: "Owner User",
      email: "owner@test.local",
      password: "StrongPass!123",
      organizationName: "North Org",
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/api/auth/signup");
    expect(fetchMock.mock.calls[1]?.[0]).toBe("http://localhost:5080/api/auth/signup");
  });

  it("posts the verification token to the verify-email route", async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 204,
      headers: new Headers(),
      text: async () => "",
    });

    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("localStorage", {
      getItem: vi.fn(() => null),
      setItem: vi.fn(),
      removeItem: vi.fn(),
    });
    vi.stubGlobal("window", {
      location: {
        origin: "http://localhost:5173",
        hostname: "localhost",
        protocol: "http:",
      },
      localStorage: {
        getItem: vi.fn(() => null),
        setItem: vi.fn(),
        removeItem: vi.fn(),
      },
    });

    await verifyEmail("verification-token");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[0]).toBe("/api/auth/verify-email");
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      method: "POST",
      body: JSON.stringify({
        token: "verification-token",
      }),
    });
  });
});
