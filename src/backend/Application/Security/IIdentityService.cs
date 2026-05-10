namespace ERP.Application.Security;

public interface IIdentityService
{
    Task<AuthResponse> SignupAsync(SignupRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(bool allDevices, CancellationToken cancellationToken);

    Task<CurrentWorkspaceDto> GetCurrentWorkspaceAsync(CancellationToken cancellationToken);

    Task<AuthResponse> SwitchOrganizationAsync(SwitchOrganizationRequest request, CancellationToken cancellationToken);

    Task<string?> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);

    Task<string?> RequestEmailVerificationAsync(RequestEmailVerificationRequest request, CancellationToken cancellationToken);

    Task ConfirmEmailVerificationAsync(ConfirmEmailVerificationRequest request, CancellationToken cancellationToken);

    Task AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken);
}
