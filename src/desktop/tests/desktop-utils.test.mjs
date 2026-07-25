import assert from "node:assert/strict";
import test from "node:test";
import {
  buildBackendEnvironment,
  isAllowedDesktopBackendUrl,
  sanitizeSupportText,
} from "../scripts/desktop-utils.mjs";

test("desktop backend URLs are restricted to explicit loopback HTTP ports", () => {
  assert.equal(isAllowedDesktopBackendUrl("http://127.0.0.1:17600"), true);
  assert.equal(isAllowedDesktopBackendUrl("http://localhost:17600"), true);
  assert.equal(isAllowedDesktopBackendUrl("https://127.0.0.1:17600"), false);
  assert.equal(isAllowedDesktopBackendUrl("http://192.168.1.10:17600"), false);
  assert.equal(isAllowedDesktopBackendUrl("http://127.0.0.1"), false);
  assert.equal(isAllowedDesktopBackendUrl("not-a-url"), false);
});

test("backend environment selects Desktop profile and loopback binding without command-line secrets", () => {
  const env = buildBackendEnvironment({
    port: 17642,
    appDataDirectory: "/tmp/highcool-desktop-test",
    startupToken: "startup-token",
    jwtSecret: "jwt-secret",
  });

  assert.equal(env.ASPNETCORE_ENVIRONMENT, "Desktop");
  assert.equal(env.ASPNETCORE_URLS, "http://127.0.0.1:17642");
  assert.equal(env.Database__Provider, "Sqlite");
  assert.equal(env.LocalDatabase__AllowDevelopmentReset, "false");
  assert.equal(env.Desktop__StartupToken, "startup-token");
  assert.equal(env.Authentication__JwtSecret, "jwt-secret");
});

test("support text redacts secrets and sensitive local paths", () => {
  const text = sanitizeSupportText(
    "Authorization: Bearer abc.def.ghi JwtSecret=secret /root/HighCool/customer.db ConnectionStrings__DefaultConnection=Server=localhost",
  );

  assert.equal(text.includes("abc.def.ghi"), false);
  assert.equal(text.includes("secret"), false);
  assert.equal(text.includes("/root/HighCool"), false);
  assert.match(text, /\[redacted\]/);
});
