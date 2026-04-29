import { requestJson } from "./api";

export interface AuthRole {
  id: string;
  name: string;
  permissions: string[];
}

export interface OrganizationOption {
  organizationId: string;
  name: string;
  isOwner: boolean;
}

export interface Workspace {
  userId: string;
  fullName: string;
  email: string;
  emailVerified: boolean;
  organizationId: string;
  organizationName: string;
  membershipId: string;
  requiresTwoFactor: boolean;
  setupCompleted: boolean;
  setupStep?: string | null;
  setupVersion?: string | null;
  permissions: string[];
  organizations: OrganizationOption[];
  roles: AuthRole[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  workspace: Workspace;
  emailVerificationToken?: string | null;
}

export interface SignupInput {
  fullName: string;
  email: string;
  password: string;
  organizationName: string;
}

export interface LoginInput {
  email: string;
  password: string;
  rememberMe: boolean;
}

export function signup(input: SignupInput) {
  return requestJson<AuthResponse>("/api/auth/signup", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export function login(input: LoginInput) {
  return requestJson<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({
      email: input.email,
      password: input.password,
      rememberMe: input.rememberMe,
      deviceName: "Web",
    }),
  });
}

export function verifyEmail(token: string) {
  return requestJson<void>("/api/auth/verify-email", {
    method: "POST",
    body: JSON.stringify({ token }),
  });
}

export function me() {
  return requestJson<Workspace>("/api/auth/me");
}

export function logout(allDevices: boolean) {
  return requestJson<void>("/api/auth/logout", {
    method: "POST",
    body: JSON.stringify({ allDevices }),
  });
}

export function switchOrganization(organizationId: string) {
  return requestJson<AuthResponse>("/api/auth/switch-organization", {
    method: "POST",
    body: JSON.stringify({
      organizationId,
      rememberMe: true,
    }),
  });
}
