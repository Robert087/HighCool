namespace ERP.Api.Configuration;

public static class TemporaryFeatureFlags
{
    // TEMPORARILY_DISABLED: Organization setup wizard bypassed until UX/feature mapping is stabilized.
    public static bool DisableOrgSetupWizard => true;

    // TEMPORARILY_DISABLED: Feature gating bypassed until all module keys/routes are aligned.
    public static bool DisableFeatureGating => true;
}
