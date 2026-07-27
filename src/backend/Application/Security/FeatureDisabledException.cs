namespace ERP.Application.Security;

public sealed class FeatureDisabledException(OrganizationFeature feature)
    : InvalidOperationException($"Feature '{feature.ToKey()}' is disabled for the active organization.")
{
    public const string ErrorCode = "FEATURE_DISABLED";

    public OrganizationFeature Feature { get; } = feature;
}

