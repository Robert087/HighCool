import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";
import { ApiError } from "../../services/api";
import {
  login as loginRequest,
  logout as logoutRequest,
  me as meRequest,
  signup as signupRequest,
  switchOrganization as switchOrganizationRequest,
  verifyEmail as verifyEmailRequest,
  type AuthRole,
  type OrganizationOption,
  type Workspace,
} from "../../services/authApi";
import { getStoredAccessToken, storeAccessToken } from "./authStorage";

interface AuthContextValue {
  error: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (input: { email: string; password: string; rememberMe: boolean }) => Promise<void>;
  logout: (allDevices?: boolean) => Promise<void>;
  signup: (input: { fullName: string; email: string; password: string; organizationName: string }) => Promise<void>;
  switchOrganization: (organizationId: string) => Promise<void>;
  refreshWorkspace: () => Promise<void>;
  workspace: Workspace | null;
  hasPermission: (permission: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const token = getStoredAccessToken();
    if (!token) {
      setIsLoading(false);
      return;
    }

    meRequest()
      .then((response) => {
        setWorkspace(response);
        setError(null);
      })
      .catch(() => {
        storeAccessToken(null);
        setWorkspace(null);
      })
      .finally(() => setIsLoading(false));
  }, []);

  async function login(input: { email: string; password: string; rememberMe: boolean }) {
    setError(null);
    const response = await loginRequest(input);
    storeAccessToken(response.accessToken);
    const refreshedWorkspace = await meRequest();
    setWorkspace(refreshedWorkspace);
  }

  async function signup(input: { fullName: string; email: string; password: string; organizationName: string }) {
    setError(null);
    const response = await signupRequest(input);

    storeAccessToken(response.accessToken);

    if (response.emailVerificationToken) {
      await verifyEmailRequest(response.emailVerificationToken);
    }

    const refreshedWorkspace = await meRequest();
    setWorkspace(refreshedWorkspace);
  }

  async function logout(allDevices = false) {
    try {
      await logoutRequest(allDevices);
    } finally {
      storeAccessToken(null);
      setWorkspace(null);
    }
  }

  async function switchOrganization(organizationId: string) {
    const response = await switchOrganizationRequest(organizationId);
    storeAccessToken(response.accessToken);
    setWorkspace(response.workspace);
  }

  async function refreshWorkspace() {
    const response = await meRequest();
    setWorkspace(response);
  }

  const value = useMemo<AuthContextValue>(() => ({
    error,
    isAuthenticated: workspace != null,
    isLoading,
    login: async (input) => {
      try {
        await login(input);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "Request failed");
        throw requestError;
      }
    },
    logout,
    signup: async (input) => {
      try {
        await signup(input);
      } catch (requestError) {
        setError(requestError instanceof Error ? requestError.message : "Request failed");
        throw requestError;
      }
    },
    switchOrganization,
    refreshWorkspace,
    workspace,
    hasPermission: (permission) => {
      if (!workspace) {
        return false;
      }

      const normalizedPermission = permission.toLowerCase();
      const normalizedPermissions = workspace.permissions.map((item) => item.toLowerCase());
      const isOwner = workspace.roles.some((role) => role.name.toLowerCase() === "owner");

      return isOwner || normalizedPermissions.includes(normalizedPermission);
    }
  }), [error, isLoading, workspace]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }

  return context;
}
