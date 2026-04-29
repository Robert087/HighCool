using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Security;

public sealed class IdentityService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext,
    JwtTokenService jwtTokenService,
    IPasswordHasher<UserAccount> passwordHasher,
    IAuditLogService auditLogService) : IIdentityService
{
    private const int DefaultLoginAttemptLimit = 5;
    private static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromHours(8);
    private static readonly TimeSpan RememberMeTimeout = TimeSpan.FromDays(30);
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromDays(2);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(2);

    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRequestExecutionContext _executionContext = executionContext;
    private readonly JwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IPasswordHasher<UserAccount> _passwordHasher = passwordHasher;
    private readonly IAuditLogService _auditLogService = auditLogService;

    public async Task<AuthResponse> SignupAsync(SignupRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (await _dbContext.UserAccounts.IgnoreQueryFilters().AnyAsync(entity => entity.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        ValidatePassword(request.Password, null);

        var organization = new Organization
        {
            Name = request.OrganizationName.Trim(),
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            PurchaseOrderPrefix = "PO",
            PurchaseReceiptPrefix = "PR",
            PurchaseReturnPrefix = "RTN",
            PaymentPrefix = "PAY",
            SetupCompleted = false,
            SetupStep = "company-profile",
            SetupVersion = "v1",
            CreatedBy = email
        };

        var user = new UserAccount
        {
            FullName = request.FullName.Trim(),
            Email = email,
            EmailVerified = true,
            Status = UserAccountStatus.Active,
            CreatedBy = email
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _dbContext.Organizations.Add(organization);
        _dbContext.UserAccounts.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var securitySettings = new OrganizationSecuritySettings
        {
            OrganizationId = organization.Id,
            CreatedBy = email
        };

        var ownerProfile = new UserProfile
        {
            OrganizationId = organization.Id,
            LanguagePreference = "en",
            CreatedBy = email
        };

        _dbContext.OrganizationSecuritySettings.Add(securitySettings);
        _dbContext.UserProfiles.Add(ownerProfile);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = await CreateDefaultRolesAsync(organization.Id, email, cancellationToken);
        var ownerRole = roles.Single(entity => entity.TemplateKey == "owner");

        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            ProfileId = ownerProfile.Id,
            Status = MembershipStatus.Active,
            IsOwner = true,
            BranchAccessMode = AccessScopeMode.All,
            WarehouseAccessMode = AccessScopeMode.All,
            CreatedBy = email
        };

        _dbContext.OrganizationMemberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.MembershipRoles.Add(new MembershipRole
        {
            OrganizationId = organization.Id,
            MembershipId = membership.Id,
            RoleId = ownerRole.Id,
            CreatedBy = email
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await CreateAuthResponseAsync(user, organization, membership, rememberMe: true, deviceName: "Signup", cancellationToken);
        await _auditLogService.WriteAsync(
            "signup",
            "auth",
            nameof(UserAccount),
            user.Id.ToString(),
            null,
            new { UserId = user.Id, user.Email, OrganizationId = organization.Id, OrganizationName = organization.Name },
            organization.Id,
            user.Id,
            cancellationToken);

        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Email == email, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.Status is UserAccountStatus.Suspended or UserAccountStatus.Disabled || user.IsDeleted)
        {
            throw new UnauthorizedAccessException(user.Status == UserAccountStatus.Suspended
                ? "Your account is suspended."
                : "Your account is disabled.");
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Your account is locked due to repeated failed attempts.");
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts += 1;
            var limit = await ResolveLoginAttemptLimitAsync(user.Id, cancellationToken);
            if (user.FailedLoginAttempts >= limit)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "failed_login",
                "auth",
                nameof(UserAccount),
                user.Id.ToString(),
                null,
                new { user.Email, user.FailedLoginAttempts },
                null,
                user.Id,
                cancellationToken);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!user.EmailVerified)
        {
            throw new UnauthorizedAccessException("Your email is not verified yet.");
        }

        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .Where(entity => entity.UserId == user.Id && entity.Status == MembershipStatus.Active)
            .OrderByDescending(entity => entity.IsOwner)
            .ThenBy(entity => entity.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedAccessException("You do not have an active organization membership.");
        }

        var organization = await _dbContext.Organizations.IgnoreQueryFilters().SingleAsync(entity => entity.Id == membership.OrganizationId, cancellationToken);

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIpAddress = _executionContext.IpAddress;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = await CreateAuthResponseAsync(user, organization, membership, request.RememberMe, request.DeviceName, cancellationToken);
        await _auditLogService.WriteAsync(
            "login",
            "auth",
            nameof(UserAccount),
            user.Id.ToString(),
            null,
            new { user.Email, organization.Id },
            organization.Id,
            user.Id,
            cancellationToken);

        return response;
    }

    public async Task LogoutAsync(bool allDevices, CancellationToken cancellationToken)
    {
        if (!_executionContext.UserId.HasValue)
        {
            return;
        }

        if (allDevices)
        {
            var sessions = await _dbContext.UserSessions
                .IgnoreQueryFilters()
                .Where(entity => entity.UserId == _executionContext.UserId.Value && entity.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var session in sessions)
            {
                session.IsActive = false;
                session.RevokedAt = DateTime.UtcNow;
                session.RevokedBy = _executionContext.Actor;
            }
        }
        else if (_executionContext.SessionId.HasValue)
        {
            var session = await _dbContext.UserSessions
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(entity => entity.Id == _executionContext.SessionId.Value, cancellationToken);

            if (session is not null)
            {
                session.IsActive = false;
                session.RevokedAt = DateTime.UtcNow;
                session.RevokedBy = _executionContext.Actor;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            "logout",
            "auth",
            nameof(UserSession),
            _executionContext.SessionId?.ToString(),
            null,
            new { allDevices },
            null,
            null,
            cancellationToken);
    }

    public async Task<CurrentWorkspaceDto> GetCurrentWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (!_executionContext.UserId.HasValue || !_executionContext.OrganizationId.HasValue || !_executionContext.MembershipId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleAsync(entity => entity.Id == _executionContext.UserId.Value, cancellationToken);
        var membership = await LoadMembershipAsync(_executionContext.MembershipId.Value, cancellationToken);
        var organization = await _dbContext.Organizations.IgnoreQueryFilters().SingleAsync(entity => entity.Id == membership.OrganizationId, cancellationToken);
        return await BuildWorkspaceAsync(user, organization, membership, cancellationToken);
    }

    public async Task<AuthResponse> SwitchOrganizationAsync(SwitchOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (!_executionContext.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Where(entity =>
                entity.UserId == _executionContext.UserId.Value &&
                entity.OrganizationId == request.OrganizationId &&
                entity.Status == MembershipStatus.Active)
            .OrderByDescending(entity => entity.IsOwner)
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedAccessException("You do not have access to that organization.");
        }

        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleAsync(entity => entity.Id == _executionContext.UserId.Value, cancellationToken);
        var organization = await _dbContext.Organizations.IgnoreQueryFilters().SingleAsync(entity => entity.Id == request.OrganizationId, cancellationToken);

        return await CreateAuthResponseAsync(user, organization, membership, request.RememberMe, "Organization switch", cancellationToken);
    }

    public async Task<string?> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(entity => entity.Email == email, cancellationToken);

        if (user is null || user.IsDeleted)
        {
            return null;
        }

        var rawToken = SecurityTokenTools.CreateToken();
        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = SecurityTokenTools.ComputeHash(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(PasswordResetTokenLifetime),
            CreatedBy = user.Email
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("password_reset_requested", "auth", nameof(UserAccount), user.Id.ToString(), null, new { user.Email }, null, user.Id, cancellationToken);
        return rawToken;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = SecurityTokenTools.ComputeHash(request.Token);
        var resetToken = await _dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null || resetToken.UsedAt.HasValue || resetToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Password reset token is invalid or expired.");
        }

        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleAsync(entity => entity.Id == resetToken.UserId, cancellationToken);
        ValidatePassword(request.Password, null);
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        resetToken.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("password_reset", "auth", nameof(UserAccount), user.Id.ToString(), null, new { user.Email }, null, user.Id, cancellationToken);
    }

    public async Task<string?> RequestEmailVerificationAsync(RequestEmailVerificationRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(entity => entity.Email == email, cancellationToken);
        if (user is null || user.EmailVerified)
        {
            return null;
        }

        var token = await CreateEmailVerificationTokenAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task ConfirmEmailVerificationAsync(ConfirmEmailVerificationRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = SecurityTokenTools.ComputeHash(request.Token);
        var verification = await _dbContext.EmailVerificationTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);

        if (verification is null || verification.UsedAt.HasValue || verification.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Email verification token is invalid or expired.");
        }

        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleAsync(entity => entity.Id == verification.UserId, cancellationToken);
        user.EmailVerified = true;
        verification.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("email_verified", "auth", nameof(UserAccount), user.Id.ToString(), null, new { user.Email }, null, user.Id, cancellationToken);
    }

    public async Task AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = SecurityTokenTools.ComputeHash(request.Token);
        var invitation = await _dbContext.UserInvitations
            .IgnoreQueryFilters()
            .Include(entity => entity.Roles)
            .Include(entity => entity.WarehouseAccesses)
            .Include(entity => entity.BranchAccesses)
            .SingleOrDefaultAsync(entity => entity.TokenHash == tokenHash, cancellationToken);

        if (invitation is null)
        {
            throw new InvalidOperationException("Invitation is invalid.");
        }

        if (invitation.Status == InvitationStatus.Revoked)
        {
            throw new InvalidOperationException("Invitation was revoked.");
        }

        if (invitation.Status == InvitationStatus.Accepted)
        {
            throw new InvalidOperationException("Invitation was already accepted.");
        }

        if (invitation.ExpiresAt <= DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Invitation has expired.");
        }

        var email = NormalizeEmail(invitation.Email);
        var user = await _dbContext.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(entity => entity.Email == email, cancellationToken);
        if (user is null)
        {
            ValidatePassword(request.Password, null);
            user = new UserAccount
            {
                FullName = request.FullName.Trim(),
                Email = email,
                EmailVerified = true,
                Status = UserAccountStatus.Active,
                CreatedBy = email
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            _dbContext.UserAccounts.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var membership = new OrganizationMembership
        {
            OrganizationId = invitation.OrganizationId,
            UserId = user.Id,
            ProfileId = invitation.ProfileId,
            Status = MembershipStatus.Active,
            BranchAccessMode = invitation.BranchAccessMode,
            WarehouseAccessMode = invitation.WarehouseAccessMode,
            CreatedBy = email
        };
        _dbContext.OrganizationMemberships.Add(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var role in invitation.Roles)
        {
            _dbContext.MembershipRoles.Add(new MembershipRole
            {
                OrganizationId = invitation.OrganizationId,
                MembershipId = membership.Id,
                RoleId = role.RoleId,
                CreatedBy = email
            });
        }

        foreach (var warehouseAccess in invitation.WarehouseAccesses)
        {
            _dbContext.MembershipWarehouseAccesses.Add(new MembershipWarehouseAccess
            {
                OrganizationId = invitation.OrganizationId,
                MembershipId = membership.Id,
                WarehouseId = warehouseAccess.WarehouseId,
                CreatedBy = email
            });
        }

        foreach (var branchAccess in invitation.BranchAccesses)
        {
            _dbContext.MembershipBranchAccesses.Add(new MembershipBranchAccess
            {
                OrganizationId = invitation.OrganizationId,
                MembershipId = membership.Id,
                BranchCode = branchAccess.BranchCode,
                CreatedBy = email
            });
        }

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("invitation_accepted", "identity", nameof(UserInvitation), invitation.Id.ToString(), null, new { invitation.Email }, null, user.Id, cancellationToken);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(
        UserAccount user,
        Organization organization,
        OrganizationMembership membership,
        bool rememberMe,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        var securitySettings = await _dbContext.OrganizationSecuritySettings
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.OrganizationId == organization.Id, cancellationToken);

        var sessionSecret = SecurityTokenTools.CreateToken();
        var session = new UserSession
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            MembershipId = membership.Id,
            SessionTokenHash = SecurityTokenTools.ComputeHash(sessionSecret),
            DeviceName = deviceName,
            Browser = _executionContext.UserAgent,
            IpAddress = _executionContext.IpAddress,
            RememberMe = rememberMe,
            ExpiresAt = DateTime.UtcNow.Add(rememberMe ? RememberMeTimeout : TimeSpan.FromMinutes(securitySettings.SessionTimeoutMinutes <= 0 ? DefaultSessionTimeout.TotalMinutes : securitySettings.SessionTimeoutMinutes)),
            CreatedBy = user.Email
        };

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var workspace = await BuildWorkspaceAsync(user, organization, membership, cancellationToken);
        var token = _jwtTokenService.CreateAccessToken(user, organization, membership, session);

        return new AuthResponse(token.AccessToken, token.ExpiresAt, workspace, null, null);
    }

    private async Task<CurrentWorkspaceDto> BuildWorkspaceAsync(
        UserAccount user,
        Organization organization,
        OrganizationMembership membership,
        CancellationToken cancellationToken)
    {
        membership = await LoadMembershipAsync(membership.Id, cancellationToken);
        var roles = membership.Roles
            .Where(entity => entity.Role is not null)
            .Select(entity => ToRoleDto(entity.Role!))
            .ToArray();

        var permissions = membership.IsOwner
            ? Permissions.All
            : Permissions.Expand(roles.SelectMany(entity => entity.Permissions)).OrderBy(entity => entity).ToArray();

        var organizations = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.UserId == user.Id && entity.Status == MembershipStatus.Active)
            .Join(
                _dbContext.Organizations.IgnoreQueryFilters().AsNoTracking(),
                membershipEntity => membershipEntity.OrganizationId,
                org => org.Id,
                (membershipEntity, org) => new OrganizationOptionDto(org.Id, org.Name, membershipEntity.IsOwner))
            .ToListAsync(cancellationToken);

        var security = await _dbContext.OrganizationSecuritySettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(entity => entity.OrganizationId == organization.Id, cancellationToken);

        return new CurrentWorkspaceDto(
            user.Id,
            user.FullName,
            user.Email,
            user.EmailVerified,
            organization.Id,
            organization.Name,
            membership.Id,
            security.ForceTwoFactor,
            organization.SetupCompleted,
            organization.SetupStep,
            organization.SetupVersion,
            permissions.OrderBy(entity => entity).ToArray(),
            organizations,
            roles);
    }

    private async Task<OrganizationMembership> LoadMembershipAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        return await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .SingleAsync(entity => entity.Id == membershipId, cancellationToken);
    }

    private async Task<IReadOnlyList<Role>> CreateDefaultRolesAsync(Guid organizationId, string actor, CancellationToken cancellationToken)
    {
        var roles = new List<Role>();
        foreach (var template in RoleTemplateCatalog.DefaultTemplates)
        {
            var role = new Role
            {
                OrganizationId = organizationId,
                Name = template.Name,
                TemplateKey = template.Key,
                IsSystemRole = true,
                IsProtected = template.IsProtected,
                IsActive = true,
                CreatedBy = actor
            };
            _dbContext.Roles.Add(role);
            roles.Add(role);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var role in roles)
        {
            var template = RoleTemplateCatalog.DefaultTemplates.Single(entity => entity.Key == role.TemplateKey);
            foreach (var permission in Permissions.Expand(template.Permissions))
            {
                _dbContext.RolePermissions.Add(new RolePermission
                {
                    OrganizationId = organizationId,
                    RoleId = role.Id,
                    PermissionKey = permission,
                    CreatedBy = actor
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task<int> ResolveLoginAttemptLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orgId = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Where(entity => entity.UserId == userId)
            .Select(entity => (Guid?)entity.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!orgId.HasValue)
        {
            return DefaultLoginAttemptLimit;
        }

        return await _dbContext.OrganizationSecuritySettings
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == orgId.Value)
            .Select(entity => entity.LoginAttemptLimit)
            .FirstOrDefaultAsync(cancellationToken) switch
        {
            <= 0 => DefaultLoginAttemptLimit,
            var value => value
        };
    }

    private async Task<string> CreateEmailVerificationTokenAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var rawToken = SecurityTokenTools.CreateToken();
        _dbContext.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = SecurityTokenTools.ComputeHash(rawToken),
            ExpiresAt = DateTime.UtcNow.Add(VerificationTokenLifetime),
            CreatedBy = user.Email
        });

        await _auditLogService.WriteAsync("email_verification_requested", "auth", nameof(UserAccount), user.Id.ToString(), null, new { user.Email }, null, user.Id, cancellationToken);
        return rawToken;
    }

    private static void ValidatePassword(string password, OrganizationSecuritySettings? policy)
    {
        var minimumPasswordLength = policy?.MinimumPasswordLength ?? 10;
        if (string.IsNullOrWhiteSpace(password) || password.Length < minimumPasswordLength)
        {
            throw new InvalidOperationException($"Password must be at least {minimumPasswordLength} characters.");
        }

        if ((policy?.RequireUppercase ?? true) && !password.Any(char.IsUpper))
        {
            throw new InvalidOperationException("Password must include an uppercase letter.");
        }

        if ((policy?.RequireLowercase ?? true) && !password.Any(char.IsLower))
        {
            throw new InvalidOperationException("Password must include a lowercase letter.");
        }

        if ((policy?.RequireNumber ?? true) && !password.Any(char.IsDigit))
        {
            throw new InvalidOperationException("Password must include a number.");
        }

        if ((policy?.RequireSymbol ?? true) && password.All(char.IsLetterOrDigit))
        {
            throw new InvalidOperationException("Password must include a symbol.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    internal static RoleDto ToRoleDto(Role role)
    {
        return new RoleDto(
            role.Id,
            role.Name,
            role.IsSystemRole,
            role.IsProtected,
            role.IsActive,
            role.TemplateKey,
            Permissions.Expand(role.Permissions.Select(entity => entity.PermissionKey)).OrderBy(entity => entity).ToArray());
    }
}
