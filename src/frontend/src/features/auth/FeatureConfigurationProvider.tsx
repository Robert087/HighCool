import { createContext, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";
import { useAuth } from "./AuthProvider";
import { getFeatureConfiguration, type FeatureConfiguration } from "../../services/settingsApi";

interface FeatureConfigurationContextValue {
  features: FeatureConfiguration | null;
  isLoading: boolean;
  reload: () => Promise<void>;
}

const FeatureConfigurationContext = createContext<FeatureConfigurationContextValue | null>(null);

export function FeatureConfigurationProvider({ children }: PropsWithChildren) {
  const { isAuthenticated, workspace } = useAuth();
  const [features, setFeatures] = useState<FeatureConfiguration | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function load() {
    if (!isAuthenticated || !workspace?.setupCompleted) {
      setFeatures(null);
      setIsLoading(false);
      return;
    }

    setIsLoading(true);

    try {
      const response = await getFeatureConfiguration();
      setFeatures(response);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, [isAuthenticated, workspace?.setupCompleted, workspace?.organizationId]);

  const value = useMemo<FeatureConfigurationContextValue>(() => ({
    features,
    isLoading,
    reload: load,
  }), [features, isLoading]);

  return (
    <FeatureConfigurationContext.Provider value={value}>
      {children}
    </FeatureConfigurationContext.Provider>
  );
}

export function useFeatureConfiguration() {
  const context = useContext(FeatureConfigurationContext);
  if (!context) {
    throw new Error("useFeatureConfiguration must be used within a FeatureConfigurationProvider.");
  }

  return context;
}
