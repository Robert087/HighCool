using ERP.Application.Security;

namespace ERP.Infrastructure.Security;

public sealed class NoOpAuthMessageDeliveryService : IAuthMessageDeliveryService
{
    public Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task SendEmailVerificationAsync(string email, string verificationToken, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
