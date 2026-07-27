import type { PropsWithChildren } from "react";
import { Navigate } from "react-router-dom";
import { useAuth } from "../features/auth/AuthProvider";
import { useFeatureConfiguration, type FeatureFlagKey } from "../features/auth/FeatureConfigurationProvider";
import { AccessDeniedPage } from "../pages/AccessDeniedPage";
import { FeatureDisabledPage } from "../pages/FeatureDisabledPage";
import { DISABLE_ORG_SETUP_WIZARD } from "../config/temporaryFlags";

interface RouteGateProps extends PropsWithChildren {
  allowDuringSetup?: boolean;
  feature?: FeatureFlagKey;
  permission?: string;
}

export function RouteGate({ allowDuringSetup = false, children, feature, permission }: RouteGateProps) {
  const { hasPermission, isAuthenticated, isLoading, workspace } = useAuth();
  const { hasFeature, isLoading: isFeaturesLoading } = useFeatureConfiguration();

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

  if (feature) {
    if (isFeaturesLoading) {
      return null;
    }

    if (!hasFeature(feature)) {
      return <FeatureDisabledPage />;
    }
  }

  return <>{children}</>;
}
