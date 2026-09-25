using Axpense.Data.Entities;
using Axpense.Data.UserApplication;
using Axpense.Infrastructure.Context;
using Axpense.Infrastructure.Seeding;
using Axpense.Service.Auth.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Axpense.Service.Auth
{
    public class AuthService : IAuthService
    {
        #region Fields
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly AxpenseDbContext _db;
        private readonly IConfiguration _configuration;
        #endregion

        #region Constructor
        public AuthService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            AxpenseDbContext db,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _configuration = configuration;
        }
        #endregion

        #region Methods
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await _userManager.FindByEmailAsync(email) != null)
                return Fail("EMAIL_TAKEN", "An account with this email already exists.", "email");

            string userName;
            if (!string.IsNullOrWhiteSpace(request.UserName))
            {
                userName = request.UserName.Trim();
                if (userName.Length < 3)
                    return Fail("USERNAME_INVALID", "Username must be at least 3 characters.", "userName");
                if (await _userManager.FindByNameAsync(userName) != null)
                    return Fail("USERNAME_TAKEN", "This username is already taken.", "userName");
            }
            else
            {
                userName = await UniqueUserNameAsync(email);
            }

            // كل تسجيل جديد بيعمل Organization (Tenant) جديدة، والمستخدم ده بيبقى أول عضو فيها
            var organization = new Organization
            {
                Name = request.OrganizationName.Trim()
            };
            _db.Organizations.Add(organization);
            await _db.SaveChangesAsync();
            await OrganizationDefaultsSeeder.SeedAsync(_db, organization.Id);

            var user = new ApplicationUser
            {
                UserName = userName,
                Email = email,
                OrganizationId = organization.Id,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim()
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                // فشل إنشاء المستخدم - لازم نرجّع الـ Organization اللي أنشأناها عشان منسيبش بيانات يتيمة
                _db.Organizations.Remove(organization);
                await _db.SaveChangesAsync();
                return PasswordOrIdentityFail(createResult, "password");
            }

            // أول مستخدم في الـ Organization بيبقى Owner افتراضيًا (إلا لو اتحدد Role تاني صراحة)
            var role = string.IsNullOrWhiteSpace(request.Role) ? "Owner" : request.Role.Trim();

            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role));

            await _userManager.AddToRoleAsync(user, role);

            // New workspaces start the onboarding wizard (company profile); OnboardingCompletedAt stays null until it is finished or skipped.
            _db.OrganizationSettings.Add(new Data.Entities.SaasEntities.OrganizationSettings
            {
                OrganizationId = organization.Id,
                CompanyName = organization.Name,
                CurrentState = (int)Data.Enums.CurrentStatusType.Active,
                CreatedBy = user.Id,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return await BuildSuccessResponseAsync(user, organization, "Account created.");
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var login = (request.UserNameOrEmail ?? "").Trim();
            var user = await _userManager.FindByNameAsync(login)
                       ?? await _userManager.FindByEmailAsync(login);

            if (user == null || user.IsDeleted)
                return Fail("INVALID_CREDENTIALS", "The email/username or password is incorrect.");

            if (await _userManager.IsLockedOutAsync(user))
                return Fail("LOCKED_OUT", "Too many failed attempts. The account is locked for a few minutes.");

            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                    return Fail("LOCKED_OUT", "Too many failed attempts. The account is locked for a few minutes.");
                return Fail("INVALID_CREDENTIALS", "The email/username or password is incorrect.");
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var organization = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == user.OrganizationId);
            return await BuildSuccessResponseAsync(user, organization, "تم تسجيل الدخول بنجاح");
        }

        public async Task<AuthResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
                return new AuthResponse { Success = false, Message = "المستخدم غير موجود" };

            var email = request.Email.Trim();
            var duplicateEmail = await _userManager.FindByEmailAsync(email);
            if (duplicateEmail != null && duplicateEmail.Id != user.Id)
                return new AuthResponse { Success = false, Message = "البريد الإلكتروني مستخدم بالفعل" };

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.Email = email;
            user.PhoneNumber = request.PhoneNumber?.Trim();

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "فشل تحديث البيانات",
                    Errors = updateResult.Errors.Select(e => e.Description)
                };
            }

            var organization = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == user.OrganizationId);
            return await BuildSuccessResponseAsync(user, organization, "تم تحديث البيانات بنجاح");
        }

        public async Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
                return new AuthResponse { Success = false, Message = "المستخدم غير موجود" };

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "فشل تغيير كلمة المرور - تأكد من كلمة المرور الحالية",
                    Errors = result.Errors.Select(e => e.Description)
                };
            }

            return new AuthResponse { Success = true, Message = "تم تغيير كلمة المرور بنجاح" };
        }

        public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());

            // ما بنكشفش إن الإيميل مش مسجل، عشان محدش يستخدمها يجرب إيميلات موجودة ولا لأ
            if (user == null || user.IsDeleted)
                return new AuthResponse { Success = true, Message = "If this email is registered, a reset link has been sent." };

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // TODO: لسه معملناش خدمة إيميل - التوكن بيترجع هنا مؤقتًا عشان تقدر تختبر الفلو،
            // لما نضيف IEmailService هنبعت التوكن بإيميل بدل ما يترجع في الـ Response
            return new AuthResponse
            {
                Success = true,
                Message = "If this email is registered, a reset link has been sent.",
                ResetToken = token
            };
        }

        public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null || user.IsDeleted)
                return Fail("INVALID_RESET_TOKEN", "This reset link is invalid or has expired.");

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

            if (!result.Succeeded)
            {
                if (result.Errors.Any(e => e.Code == "InvalidToken"))
                    return Fail("INVALID_RESET_TOKEN", "This reset link is invalid or has expired.");
                return PasswordOrIdentityFail(result, "newPassword");
            }

            // إعادة تعيين الباسورد بنجاح = فرصة كويسة إننا نفك أي قفل سابق ونصفر عداد المحاولات الفاشلة
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            return new AuthResponse { Success = true, Message = "Password reset. You can sign in now." };
        }
        public async Task<List<UserSummaryResponse>> GetAllUsersAsync()
        {
            var users = await _userManager.Users
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            var result = new List<UserSummaryResponse>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserSummaryResponse
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    Role = roles.FirstOrDefault() ?? "Staff",
                    IsRootSuperAdmin = user.IsRootSuperAdmin,
                    CreatedAt = user.CreatedAt
                });
            }

            return result;
        }

        public async Task<List<UserSummaryResponse>> GetOrganizationUsersAsync(Guid organizationId)
        {
            var users = await _userManager.Users
                .Where(u => !u.IsDeleted && u.OrganizationId == organizationId)
                .OrderBy(u => u.FirstName)
                .ToListAsync();

            var result = new List<UserSummaryResponse>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new UserSummaryResponse
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    Role = roles.FirstOrDefault() ?? "Staff",
                    IsRootSuperAdmin = user.IsRootSuperAdmin,
                    CreatedAt = user.CreatedAt
                });
            }

            return result;
        }

        #endregion

        #region Helpers
        private static AuthResponse Fail(string code, string message, string? field = null) => new()
        {
            Success = false, Code = code, Message = message,
            FieldErrors = field is null ? null : new Dictionary<string, string> { [field] = message }
        };

        /// <summary>Maps Identity errors: password rule failures go to the password field, the rest are returned as messages.</summary>
        private static AuthResponse PasswordOrIdentityFail(IdentityResult result, string passwordField)
        {
            var errors = result.Errors.ToList();
            var pwd = errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).ToList();
            if (pwd.Count > 0)
                return new AuthResponse
                {
                    Success = false, Code = "PASSWORD_INVALID", Message = pwd[0].Description, Errors = errors.Select(e => e.Description),
                    FieldErrors = new Dictionary<string, string> { [passwordField] = string.Join(" ", pwd.Select(e => e.Description)) }
                };
            var dupEmail = errors.FirstOrDefault(e => e.Code == "DuplicateEmail");
            if (dupEmail is not null) return Fail("EMAIL_TAKEN", "An account with this email already exists.", "email");
            var dupName = errors.FirstOrDefault(e => e.Code == "DuplicateUserName");
            if (dupName is not null) return Fail("USERNAME_TAKEN", "This username is already taken.", "userName");
            return new AuthResponse { Success = false, Code = "ACCOUNT_ERROR", Message = errors.FirstOrDefault()?.Description ?? "Could not create the account.", Errors = errors.Select(e => e.Description) };
        }

        /// <summary>Username from the email's local part (letters, digits, . _ -), suffixed with a number when taken.</summary>
        private async Task<string> UniqueUserNameAsync(string email)
        {
            var local = new string(email.Split('@')[0].Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-').ToArray());
            if (local.Length < 3) local = (local + "user").Substring(0, Math.Max(3, local.Length));
            if (local.Length > 40) local = local[..40];
            var candidate = local;
            for (var i = 2; await _userManager.FindByNameAsync(candidate) != null; i++) candidate = $"{local}{i}";
            return candidate;
        }

        private async Task<AuthResponse> BuildSuccessResponseAsync(ApplicationUser user, Organization? organization, string message)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Staff";

            var jwtSection = _configuration.GetSection("Jwt");
            var secretKey = jwtSection["Key"]
                ?? throw new InvalidOperationException("Jwt:Key غير موجود في appsettings.json");

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, role),
                new Claim("organization_id", user.OrganizationId.ToString())
            };

            var durationMinutes = double.TryParse(jwtSection["DurationInMinutes"], out var minutes)
                ? minutes
                : 60;
            var expiresOn = DateTime.UtcNow.AddMinutes(durationMinutes);

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: expiresOn,
                signingCredentials: credentials);

            return new AuthResponse
            {
                Success = true,
                Message = message,
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Role = role,
                OrganizationId = user.OrganizationId,
                OrganizationName = organization?.Name,
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresOn = expiresOn
            };
        }
        #endregion
    }
}
