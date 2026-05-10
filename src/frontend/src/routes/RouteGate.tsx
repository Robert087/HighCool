import type { PropsWithChildren } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../features/auth/AuthProvider";
import { useFeatureConfiguration } from "../features/auth/FeatureConfigurationProvider";
import { AccessDeniedPage } from "../pages/AccessDeniedPage";
import { FeatureDisabledPage } from "../pages/FeatureDisabledPage";
import { DISABLE_FEATURE_GATING, DISABLE_ORG_SETUP_WIZARD } from "../config/temporaryFlags";

type FeatureFlagKey =
  | "workspaceEnabled"
  | "procurementEnabled"
  | "inventoryEnabled"
  | "suppliersEnabled"
  | "supplierFinancialsEnabled"
  | "settingsEnabled";

interface RouteGateProps extends PropsWithChildren {
  allowDuringSetup?: boolean;
  feature?: FeatureFlagKey;
  permission?: string;
}

export function RouteGate({ allowDuringSetup = false, children, feature, permission }: RouteGateProps) {
  const { hasPermission, isAuthenticated, isLoading, workspace } = useAuth();
  const { features, isLoading: isFeaturesLoading } = useFeatureConfiguration();

  if (isLoading) {
    return null;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (!DISABLE_ORG_SETUP_WIZARD && !workspace?.setupCompleted && !allowDuringSetup) {
    return <Navigate to="/setup/organization" replace />;
  }

  if (!DISABLE_ORG_SETUP_WIZARD && workspace?.setupCompleted && allowDuringSetup) {
    return <Navigate to="/workspace" replace />;
  }

  if (permission && !hasPermission(permission)) {
    return <AccessDeniedPage />;
  }

  if (!DISABLE_FEATURE_GATING && feature) {
    if (isFeaturesLoading) {
      return null;
    }

    if (features && !features[feature]) {
      return <FeatureDisabledPage />;
    }
  }

  return <>{children}</>;
}
