namespace ERP.Application.Security;

public interface IAuthMessageDeliveryService
{
    Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken);

    Task SendEmailVerificationAsync(string email, string verificationToken, CancellationToken cancellationToken);
}
