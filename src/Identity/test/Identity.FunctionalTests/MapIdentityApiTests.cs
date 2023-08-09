// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Identity.DefaultUI.WebSite;
using Identity.DefaultUI.WebSite.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Identity.FunctionalTests;

public class MapIdentityApiTests : LoggedTest
{
    private string Email { get; } = $"{Guid.NewGuid()}@example.com";
    private string Password { get; } = "[PLACEHOLDER]-1a";

    [Theory]
    [MemberData(nameof(AddIdentityModes))]
    public async Task CanRegisterUser(string addIdentityMode)
    {
        await using var app = await CreateAppAsync(AddIdentityActions[addIdentityMode]);
        using var client = app.GetTestClient();

        AssertOkAndEmpty(await client.PostAsJsonAsync("/identity/register", new { Email, Password }));
    }

    [Fact]
    public async Task RegisterFailsGivenNoEmail()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        AssertBadRequestAndEmpty(await client.PostAsJsonAsync("/identity/register", new { Password }));
    }

    [Fact]
    public async Task LoginFailsGivenUnregisteredUser()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "Failed");
    }

    [Fact]
    public async Task LoginFailsGivenWrongPassword()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password = "wrong" }),
            "Failed");
    }

    [Theory]
    [MemberData(nameof(AddIdentityModes))]
    public async Task CanLoginWithBearerToken(string addIdentityMode)
    {
        await using var app = await CreateAppAsync(AddIdentityActions[addIdentityMode]);
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        loginResponse.EnsureSuccessStatusCode();
        Assert.False(loginResponse.Headers.Contains(HeaderNames.SetCookie));

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tokenType = loginContent.GetProperty("token_type").GetString();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        var expiresIn = loginContent.GetProperty("expires_in").GetDouble();

        Assert.Equal("Bearer", tokenType);
        Assert.Equal(3600, expiresIn);

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));
    }

    [Fact]
    public async Task CanCustomizeBearerTokenExpiration()
    {
        var clock = new MockTimeProvider();
        var expireTimeSpan = TimeSpan.FromSeconds(42);

        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
            services.AddIdentityCore<ApplicationUser>().AddApiEndpoints().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme, options =>
            {
                options.BearerTokenExpiration = expireTimeSpan;
            });
        });

        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        var expiresIn = loginContent.GetProperty("expires_in").GetDouble();

        Assert.Equal(expireTimeSpan.TotalSeconds, expiresIn);

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        // Works without time passing.
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));

        clock.Advance(TimeSpan.FromSeconds(expireTimeSpan.TotalSeconds - 1));

        // Still works one second before expiration.
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));

        clock.Advance(TimeSpan.FromSeconds(1));

        // Fails the second the BearerTokenExpiration elapses.
        AssertUnauthorizedAndEmpty(await client.GetAsync("/auth/hello"));
    }

    [Fact]
    public async Task CanLoginWithCookies()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login?cookieMode=true", new { Email, Password });

        AssertOkAndEmpty(loginResponse);
        Assert.True(loginResponse.Headers.TryGetValues(HeaderNames.SetCookie, out var setCookieHeaders));
        var setCookieHeader = Assert.Single(setCookieHeaders);

        // The compiler does not see Assert.True's DoesNotReturnIfAttribute :(
        if (setCookieHeader.Split(';', 2) is not [var cookie, _])
        {
            throw new XunitException("Invalid Set-Cookie header!");
        }

        client.DefaultRequestHeaders.Add(HeaderNames.Cookie, cookie);
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));
    }

    [Fact]
    public async Task CannotLoginWithCookiesWithOnlyCoreServices()
    {
        await using var app = await CreateAppAsync(services => AddIdentityApiEndpointsBearerOnly(services));
        using var client = app.GetTestClient();

        await RegisterAsync(client);

        await Assert.ThrowsAsync<InvalidOperationException>(()
            => client.PostAsJsonAsync("/identity/login?cookieMode=true", new { Email, Password }));
    }

    [Fact]
    public async Task CanReadBearerTokenFromQueryString()
    {
        await using var app = await CreateAppAsync(services =>
        {
            services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
            services.AddIdentityCore<ApplicationUser>().AddApiEndpoints().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme, options =>
            {
                options.Events.OnMessageReceived = context =>
                {
                    context.Token = (string?)context.Request.Query["access_token"];
                    return Task.CompletedTask;
                };
            });
        });

        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();

        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync($"/auth/hello?access_token={accessToken}"));

        // The normal header still works
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));
    }

    [Theory]
    [MemberData(nameof(AddIdentityModes))]
    public async Task Returns401UnauthorizedStatusGivenNoBearerTokenOrCookie(string addIdentityMode)
    {
        await using var app = await CreateAppAsync(AddIdentityActions[addIdentityMode]);
        using var client = app.GetTestClient();

        AssertUnauthorizedAndEmpty(await client.GetAsync("/auth/hello"));

        client.DefaultRequestHeaders.Authorization = new("Bearer");
        AssertUnauthorizedAndEmpty(await client.GetAsync("/auth/hello"));

        client.DefaultRequestHeaders.Authorization = new("Bearer", "");
        AssertUnauthorizedAndEmpty(await client.GetAsync("/auth/hello"));
    }

    [Theory]
    [MemberData(nameof(AddIdentityModes))]
    public async Task CanUseRefreshToken(string addIdentityMode)
    {
        await using var app = await CreateAppAsync(AddIdentityActions[addIdentityMode]);
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginContent.GetProperty("refresh_token").GetString();

        var refreshResponse = await client.PostAsJsonAsync("/identity/refresh", new { refreshToken });
        var refreshContent = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = refreshContent.GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));
    }

    [Fact]
    public async Task Returns401UnauthorizedStatusGivenNullOrEmptyRefreshToken()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        string? refreshToken = null;
        AssertUnauthorizedAndEmpty(await client.PostAsJsonAsync("/identity/refresh", new { refreshToken }));

        refreshToken = "";
        AssertUnauthorizedAndEmpty(await client.PostAsJsonAsync("/identity/refresh", new { refreshToken }));
    }

    [Fact]
    public async Task CanCustomizeRefreshTokenExpiration()
    {
        var clock = new MockTimeProvider();
        var expireTimeSpan = TimeSpan.FromHours(42);

        await using var app = await CreateAppAsync(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            services.AddDbContext<ApplicationDbContext>((sp, options) => options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
            services.AddIdentityCore<ApplicationUser>().AddApiEndpoints().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddAuthentication().AddBearerToken(IdentityConstants.BearerScheme, options =>
            {
                options.RefreshTokenExpiration = expireTimeSpan;
            });
        });

        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginContent.GetProperty("refresh_token").GetString();
        var accessToken = loginContent.GetProperty("refresh_token").GetString();

        // Works without time passing.
        var refreshResponse = await client.PostAsJsonAsync("/identity/refresh", new { refreshToken });
        Assert.True(refreshResponse.IsSuccessStatusCode);

        clock.Advance(TimeSpan.FromSeconds(expireTimeSpan.TotalSeconds - 1));

        // Still works one second before expiration.
        refreshResponse = await client.PostAsJsonAsync("/identity/refresh", new { refreshToken });
        Assert.True(refreshResponse.IsSuccessStatusCode);

        // The bearer token stopped working 41 hours ago with the default 1 hour expiration.
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        AssertUnauthorizedAndEmpty(await client.GetAsync("/auth/hello"));

        clock.Advance(TimeSpan.FromSeconds(1));

        // Fails the second the RefreshTokenExpiration elapses.
        AssertUnauthorizedAndEmpty(await client.PostAsJsonAsync("/identity/refresh", new { refreshToken }));

        // But the last refresh_token from the successful /refresh only a second ago has not expired.
        var refreshContent = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        refreshToken = refreshContent.GetProperty("refresh_token").GetString();

        refreshResponse = await client.PostAsJsonAsync("/identity/refresh", new { refreshToken });
        refreshContent = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        accessToken = refreshContent.GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));
    }

    [Fact]
    public async Task RefreshReturns401UnauthorizedIfSecurityStampChanges()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var refreshToken = await LoginAsync(client);

        var userManager = app.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(Email);

        Assert.NotNull(user);

        await userManager.UpdateSecurityStampAsync(user);

        AssertUnauthorizedAndEmpty(await client.PostAsJsonAsync("/identity/refresh", new { refreshToken }));
    }

    [Fact]
    public async Task RefreshUpdatesUserFromStore()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var refreshToken = await LoginAsync(client);

        var userManager = app.Services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(Email);

        Assert.NotNull(user);

        var newUsername = $"{Guid.NewGuid()}@example.com";
        user.UserName = newUsername;
        await userManager.UpdateAsync(user);

        var refreshResponse = await client.PostAsJsonAsync("/identity/refresh", new { refreshToken });
        var refreshContent = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = refreshContent.GetProperty("access_token").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        Assert.Equal($"Hello, {newUsername}!", await client.GetStringAsync("/auth/hello"));
    }

    [Fact]
    public async Task LoginCanBeLockedOut()
    {
        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.Configure<IdentityOptions>(options =>
            {
                options.Lockout.MaxFailedAccessAttempts = 2;
            });
        });
        using var client = app.GetTestClient();

        await RegisterAsync(client);

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password = "wrong" }),
            "Failed");

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password = "wrong" }),
            "LockedOut");

        Assert.Single(TestSink.Writes, w =>
            w.LoggerName == "Microsoft.AspNetCore.Identity.SignInManager" &&
            w.EventId == new EventId(3, "UserLockedOut"));

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "LockedOut");
    }

    [Fact]
    public async Task LockoutCanBeDisabled()
    {
        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.Configure<IdentityOptions>(options =>
            {
                options.Lockout.AllowedForNewUsers = false;
                options.Lockout.MaxFailedAccessAttempts = 1;
            });
        });
        using var client = app.GetTestClient();

        await RegisterAsync(client);

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password = "wrong" }),
            "Failed");

        Assert.DoesNotContain(TestSink.Writes, w =>
            w.LoggerName == "Microsoft.AspNetCore.Identity.SignInManager" &&
            w.EventId == new EventId(3, "UserLockedOut"));

        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password }));
    }

    [Fact]
    public async Task AccountConfirmationCanBeEnabled()
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            });
        });
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        await LoginWithEmailConfirmationAsync(client, emailSender);

        Assert.Single(emailSender.Emails);
        Assert.Single(TestSink.Writes, w =>
            w.LoggerName == "Microsoft.AspNetCore.Identity.SignInManager" &&
            w.EventId == new EventId(4, "UserCannotSignInWithoutConfirmedAccount"));
    }

    [Fact]
    public async Task EmailConfirmationCanBeEnabled()
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
            });
        });
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        await LoginWithEmailConfirmationAsync(client, emailSender);

        Assert.Single(emailSender.Emails);
        Assert.Single(TestSink.Writes, w =>
            w.LoggerName == "Microsoft.AspNetCore.Identity.SignInManager" &&
            w.EventId == new EventId(0, "UserCannotSignInWithoutConfirmedEmail"));
    }

    [Fact]
    public async Task EmailConfirmationCanBeResent()
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
            });
        });
        using var client = app.GetTestClient();

        await RegisterAsync(client);

        var firstEmail = Assert.Single(emailSender.Emails);
        Assert.Equal("Confirm your email", firstEmail.Subject);
        Assert.Equal(Email, firstEmail.Address);

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "NotAllowed");

        AssertOk(await client.PostAsJsonAsync("/identity/resendConfirmationEmail", new { Email = "wrong" }));
        AssertOk(await client.PostAsJsonAsync("/identity/resendConfirmationEmail", new { Email = Email }));

        // Even though both resendConfirmationEmail requests returned a 200, only one for a valid registration was sent
        Assert.Equal(2, emailSender.Emails.Count);
        var resentEmail = emailSender.Emails[1];
        Assert.Equal("Confirm your email", resentEmail.Subject);
        Assert.Equal(Email, resentEmail.Address);

        AssertOk(await client.GetAsync(GetEmailConfirmationLink(resentEmail)));
        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password }));
    }

    [Fact]
    public async Task CanAddEndpointsToMultipleRouteGroupsForSameUserType()
    {
        // Test with confirmation email since that tests link generation capabilities
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync<ApplicationUser, ApplicationDbContext>(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            });
        }, autoStart: false);

        app.MapGroup("/identity2").MapIdentityApi<ApplicationUser>();

        await app.StartAsync();
        using var client = app.GetTestClient();

        // We have to use different user names to register twice since they use the same store.
        await RegisterAsync(client, "/identity", "a@example.com");
        await LoginWithEmailConfirmationAsync(client, emailSender, "/identity", "a@example.com");

        await RegisterAsync(client, "/identity2", "b@example.com");
        await LoginWithEmailConfirmationAsync(client, emailSender, "/identity2", "b@example.com");
    }

    [Fact]
    public async Task CanAddEndpointsToMultipleRouteGroupsForMultipleUsersTypes()
    {
        // Test with confirmation email since that tests link generation capabilities
        var emailSender = new TestEmailSender();

        // Even with OnModelCreating tricks to prefix table names, using the same database
        // for multiple user tables is difficult because index conflics, so we just use a different db.
        var dbConnection2 = new SqliteConnection("DataSource=:memory:");

        await using var app = await CreateAppAsync<ApplicationUser, ApplicationDbContext>(services =>
        {
            AddIdentityApiEndpoints<ApplicationUser, ApplicationDbContext>(services);

            // We just added cookie and/or bearer auth scheme(s) above. We cannot re-add these without an error.
            services
                .AddDbContext<IdentityDbContext>((sp, options) => options.UseSqlite(dbConnection2))
                .AddIdentityCore<IdentityUser>()
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddApiEndpoints();

            services.AddSingleton<IDisposable>(_ => dbConnection2);

            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            });
        }, autoStart: false);

        // The following two lines are already taken care of by CreateAppAsync for ApplicationUser and ApplicationDbContext
        await dbConnection2.OpenAsync();
        await app.Services.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();

        app.MapGroup("/identity2").MapIdentityApi<IdentityUser>();

        await app.StartAsync();
        using var client = app.GetTestClient();

        // We can use the same email twice since we're using two distinct DbContexts.
        await RegisterAsync(client, "/identity");
        await LoginWithEmailConfirmationAsync(client, emailSender, "/identity");

        await RegisterAsync(client, "/identity2");
        await LoginWithEmailConfirmationAsync(client, emailSender, "/identity2");
    }

    [Theory]
    [MemberData(nameof(AddIdentityModes))]
    public async Task CanEnableAndLoginWithTwoFactor(string addIdentityMode)
    {
        await using var app = await CreateAppAsync(AddIdentityActions[addIdentityMode]);
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        var refreshToken = loginContent.GetProperty("refresh_token").GetString();

        AssertUnauthorizedAndEmpty(await client.PostAsync("/identity/account/2fa", null));

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        // We cannot enable 2fa without verifying we can produce a valid token.
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/2fa", new { Enable = true }),
            "RequiresTwoFactor");
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/2fa", new { Enable = true, TwoFactorCode = "wrong" }),
            "InvalidTwoFactorCode");

        // Even though we're now authenticated, we must send at least "{}" in the request body. An empty request fails.
        AssertBadRequestAndEmpty(await client.PostAsync("/identity/account/2fa", null));
        AssertBadRequestAndEmpty(await client.PostAsJsonAsync<object?>("/identity/account/2fa", null));

        var twoFactorKeyResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var twoFactorKeyContent = await twoFactorKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(twoFactorKeyContent.GetProperty("isTwoFactorEnabled").GetBoolean());
        Assert.False(twoFactorKeyContent.GetProperty("isMachineRemembered").GetBoolean());

        var sharedKey = twoFactorKeyContent.GetProperty("sharedKey").GetString();

        var keyBytes = Base32.FromBase32(sharedKey);
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestep = Convert.ToInt64(unixTimestamp / 30);
        var twoFactorCode = Rfc6238AuthenticationService.ComputeTotp(keyBytes, (ulong)timestep, modifierBytes: null).ToString(CultureInfo.InvariantCulture);

        var enable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true });
        var enable2faContent = await enable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enable2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());
        Assert.False(enable2faContent.GetProperty("isMachineRemembered").GetBoolean());

        // We can still access auth'd endpoints with old access token.
        Assert.Equal($"Hello, {Email}!", await client.GetStringAsync("/auth/hello"));

        // But the refresh token is invalidated by the security stamp.
        AssertUnauthorizedAndEmpty(await client.PostAsJsonAsync("/identity/refresh", new { refreshToken }));

        client.DefaultRequestHeaders.Clear();

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "RequiresTwoFactor");

        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password, twoFactorCode }));
    }

    [Fact]
    public async Task CanLoginWithRecoveryCodeAndDisableTwoFactor()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        var twoFactorKeyResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var twoFactorKeyContent = await twoFactorKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sharedKey = twoFactorKeyContent.GetProperty("sharedKey").GetString();

        var keyBytes = Base32.FromBase32(sharedKey);
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestep = Convert.ToInt64(unixTimestamp / 30);
        var twoFactorCode = Rfc6238AuthenticationService.ComputeTotp(keyBytes, (ulong)timestep, modifierBytes: null).ToString(CultureInfo.InvariantCulture);

        var enable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true });
        var enable2faContent = await enable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enable2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());

        var recoveryCodes = enable2faContent.GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(10, recoveryCodes.Length);

        client.DefaultRequestHeaders.Clear();

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "RequiresTwoFactor");

        var recoveryLoginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password, TwoFactorRecoveryCode = recoveryCodes[0] });

        var recoveryLoginContent = await recoveryLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var recoveryAccessToken = recoveryLoginContent.GetProperty("access_token").GetString();
        Assert.NotEqual(accessToken, recoveryAccessToken);

        client.DefaultRequestHeaders.Authorization = new("Bearer", recoveryAccessToken);

        var disable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { Enable = false });
        var disable2faContent = await disable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(disable2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());

        client.DefaultRequestHeaders.Clear();

        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password }));
    }

    [Fact]
    public async Task CanResetSharedKey()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        var twoFactorKeyResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var twoFactorKeyContent = await twoFactorKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sharedKey = twoFactorKeyContent.GetProperty("sharedKey").GetString();

        var keyBytes = Base32.FromBase32(sharedKey);
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestep = Convert.ToInt64(unixTimestamp / 30);
        var twoFactorCode = Rfc6238AuthenticationService.ComputeTotp(keyBytes, (ulong)timestep, modifierBytes: null).ToString(CultureInfo.InvariantCulture);

        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true, ResetSharedKey = true }),
            "CannotResetSharedKeyAndEnable");

        var enable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true });
        var enable2faContent = await enable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enable2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());

        var resetKeyResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { ResetSharedKey = true });
        var resetKeyContent = await resetKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(resetKeyContent.GetProperty("isTwoFactorEnabled").GetBoolean());

        var resetSharedKey = resetKeyContent.GetProperty("sharedKey").GetString();

        var resetKeyBytes = Base32.FromBase32(sharedKey);
        var resetTwoFactorCode = Rfc6238AuthenticationService.ComputeTotp(keyBytes, (ulong)timestep, modifierBytes: null).ToString(CultureInfo.InvariantCulture);

        // The old 2fa code no longer works
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true }),
            "InvalidTwoFactorCode");

        var reenable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { TwoFactorCode = resetTwoFactorCode, Enable = true });
        var reenable2faContent = await reenable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enable2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());
    }

    [Fact]
    public async Task CanResetRecoveryCodes()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password });

        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        var twoFactorKeyResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var twoFactorKeyContent = await twoFactorKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sharedKey = twoFactorKeyContent.GetProperty("sharedKey").GetString();

        var keyBytes = Base32.FromBase32(sharedKey);
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestep = Convert.ToInt64(unixTimestamp / 30);
        var twoFactorCode = Rfc6238AuthenticationService.ComputeTotp(keyBytes, (ulong)timestep, modifierBytes: null).ToString(CultureInfo.InvariantCulture);

        var enable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true });
        var enable2faContent = await enable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        var recoveryCodes = enable2faContent.GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(10, enable2faContent.GetProperty("recoveryCodesLeft").GetInt32());
        Assert.Equal(10, recoveryCodes.Length);

        client.DefaultRequestHeaders.Clear();

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "RequiresTwoFactor");

        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password, TwoFactorRecoveryCode = recoveryCodes[0] }));
        // Cannot reuse codes
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password, TwoFactorRecoveryCode = recoveryCodes[0] }),
            "Failed");

        var recoveryLoginResponse = await client.PostAsJsonAsync("/identity/login", new { Email, Password, TwoFactorRecoveryCode = recoveryCodes[1] });
        var recoveryLoginContent = await recoveryLoginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var recoveryAccessToken = recoveryLoginContent.GetProperty("access_token").GetString();
        Assert.NotEqual(accessToken, recoveryAccessToken);

        client.DefaultRequestHeaders.Authorization = new("Bearer", recoveryAccessToken);

        var updated2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var updated2faContent = await updated2faResponse.Content.ReadFromJsonAsync<JsonElement>();;
        Assert.Equal(8, updated2faContent.GetProperty("recoveryCodesLeft").GetInt32());
        Assert.Null(updated2faContent.GetProperty("recoveryCodes").GetString());

        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true, ResetSharedKey = true }),
            "CannotResetSharedKeyAndEnable");

        var resetRecoveryResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { ResetRecoveryCodes = true });
        var resetRecoveryContent = await resetRecoveryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var resetRecoveryCodes = resetRecoveryContent.GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Equal(10, resetRecoveryContent.GetProperty("recoveryCodesLeft").GetInt32());
        Assert.Equal(10, resetRecoveryCodes.Length);
        Assert.Empty(recoveryCodes.Intersect(resetRecoveryCodes));

        client.DefaultRequestHeaders.Clear();

        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password, TwoFactorRecoveryCode = resetRecoveryCodes[0] }));

        // Even unused codes from before the reset now fail.
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password, TwoFactorRecoveryCode = recoveryCodes[2] }),
            "Failed");
    }

    [Fact]
    public async Task CanUsePersistentTwoFactorCookies()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        var loginResponse = await client.PostAsJsonAsync("/identity/login?cookieMode=true", new { Email, Password });
        ApplyCookies(client, loginResponse);

        var twoFactorKeyResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var twoFactorKeyContent = await twoFactorKeyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(twoFactorKeyContent.GetProperty("isTwoFactorEnabled").GetBoolean());
        Assert.False(twoFactorKeyContent.GetProperty("isMachineRemembered").GetBoolean());

        var sharedKey = twoFactorKeyContent.GetProperty("sharedKey").GetString();

        var keyBytes = Base32.FromBase32(sharedKey);
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var timestep = Convert.ToInt64(unixTimestamp / 30);
        var twoFactorCode = Rfc6238AuthenticationService.ComputeTotp(keyBytes, (ulong)timestep, modifierBytes: null).ToString(CultureInfo.InvariantCulture);

        var enable2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new { twoFactorCode, Enable = true });
        var enable2faContent = await enable2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(enable2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());
        Assert.False(enable2faContent.GetProperty("isMachineRemembered").GetBoolean());

        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "RequiresTwoFactor");

        var twoFactorLoginResponse = await client.PostAsJsonAsync("/identity/login?cookieMode=true&persistCookies=false", new { Email, Password, twoFactorCode });
        ApplyCookies(client, twoFactorLoginResponse);

        var cookie2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var cookie2faContent = await cookie2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cookie2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());
        Assert.False(cookie2faContent.GetProperty("isMachineRemembered").GetBoolean());

        var persistentLoginResponse = await client.PostAsJsonAsync("/identity/login?cookieMode=true", new { Email, Password, twoFactorCode });
        ApplyCookies(client, persistentLoginResponse);

        var persistent2faResponse = await client.PostAsJsonAsync("/identity/account/2fa", new object());
        var persistent2faContent = await persistent2faResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(persistent2faContent.GetProperty("isTwoFactorEnabled").GetBoolean());
        Assert.True(persistent2faContent.GetProperty("isMachineRemembered").GetBoolean());
    }

    [Fact]
    public async Task CanResetPassword()
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            });
        });
        using var client = app.GetTestClient();

        var confirmedEmail = "confirmed@example.com";
        var unconfirmedEmail = "unconfirmed@example.com";

        await RegisterAsync(client, email: confirmedEmail);
        await LoginWithEmailConfirmationAsync(client, emailSender, email: confirmedEmail);

        await RegisterAsync(client, email: unconfirmedEmail);

        // Two emails were sent, but only one was confirmed
        Assert.Equal(2, emailSender.Emails.Count);

        // Returns 200 status for invalid email addresses
        AssertOkAndEmpty(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = confirmedEmail }));
        AssertOkAndEmpty(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = unconfirmedEmail }));
        AssertOkAndEmpty(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = "wrong" }));

        // But only one email was sent for the confirmed address
        Assert.Equal(3, emailSender.Emails.Count);
        var resetEmail = emailSender.Emails[2];

        Assert.Equal("Reset your password", resetEmail.Subject);
        Assert.Equal(confirmedEmail, resetEmail.Address);

        var resetCode = GetPasswordResetCode(resetEmail);
        var newPassword = $"{Password}!";

        // The same validation errors are returned even for invalid emails
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = confirmedEmail, resetCode }),
            "MissingNewPassword");
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = unconfirmedEmail, resetCode }),
            "MissingNewPassword");
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = "wrong", resetCode }),
            "MissingNewPassword");

        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = confirmedEmail, ResetCode = "wrong", newPassword }),
            "InvalidToken");
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = unconfirmedEmail, ResetCode = "wrong", newPassword }),
            "InvalidToken");
        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = "wrong", ResetCode = "wrong", newPassword }),
            "InvalidToken");

        AssertOkAndEmpty(await client.PostAsJsonAsync("/identity/resetPassword", new { Email = confirmedEmail, resetCode, newPassword }));

        // The old password is no longer valid
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email = confirmedEmail, Password }),
            "Failed");

        // But the new password is
        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email = confirmedEmail, Password = newPassword }));
    }

    [Fact]
    public async Task CanGetClaims()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        await LoginAsync(client);

        var infoResponse = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        Assert.Equal(Email, infoResponse.GetProperty("email").GetString());

        var claims = infoResponse.GetProperty("claims");
        Assert.Equal(Email, claims.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(Email, claims.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal("pwd", claims.GetProperty("amr").GetString());
        Assert.NotNull(claims.GetProperty(ClaimTypes.NameIdentifier).GetString());
    }

    [Theory]
    [MemberData(nameof(AddIdentityModes))]
    public async Task CanChangeEmail(string addIdentityModes)
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityActions[addIdentityModes](services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            });
        });
        using var client = app.GetTestClient();

        AssertUnauthorizedAndEmpty(await client.GetAsync("/identity/account/info"));

        await RegisterAsync(client);
        var originalRefreshToken = await LoginWithEmailConfirmationAsync(client, emailSender);

        var infoResponse = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        Assert.Equal(Email, infoResponse.GetProperty("email").GetString());
        Assert.True(infoResponse.GetProperty("isEmailConfirmed").GetBoolean());

        var infoClaims = infoResponse.GetProperty("claims");
        Assert.Equal("pwd", infoClaims.GetProperty("amr").GetString());
        Assert.Equal(Email, infoClaims.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(Email, infoClaims.GetProperty(ClaimTypes.Email).GetString());

        var originalNameIdentifier = infoResponse.GetProperty("claims").GetProperty(ClaimTypes.NameIdentifier).GetString();
        var newEmail = $"New-{Email}";

        var infoPostResponse = await client.PostAsJsonAsync("/identity/account/info", new { newEmail });
        var infoPostContent = await infoPostResponse.Content.ReadFromJsonAsync<JsonElement>();
        // The email isn't updated until the new email is confirmed.
        Assert.Equal(Email, infoPostContent.GetProperty("email").GetString());
        Assert.True(infoPostContent.GetProperty("isEmailConfirmed").GetBoolean());

        // And none of the claims have yet been updated.
        var infoPostClaims = infoPostContent.GetProperty("claims");
        Assert.Equal(Email, infoPostClaims.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(Email, infoPostClaims.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal(originalNameIdentifier, infoClaims.GetProperty(ClaimTypes.NameIdentifier).GetString());

        // We cannot log in with the new email until we confirm the email change.
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email = newEmail, Password }),
            "Failed");

        // And we can still use the original refresh token since the email change has not yet been confirmed.
        AssertOk(await client.PostAsJsonAsync("/identity/refresh", new { RefreshToken = originalRefreshToken }));

        // Two emails have now been sent. The first was sent during registration. And the second for the email change.
        Assert.Equal(2, emailSender.Emails.Count);
        var email = emailSender.Emails[1];

        Assert.Equal("Confirm your email", email.Subject);
        Assert.Equal(newEmail, email.Address);

        AssertOk(await client.GetAsync(GetEmailConfirmationLink(email)));

        var infoAfterEmailChange = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        // The email is immediately updated after the email is confirmed.
        Assert.Equal(newEmail, infoAfterEmailChange.GetProperty("email").GetString());

        // The email still won't be available as a claim until we get a new token.
        var claimsAfterEmailChange = infoAfterEmailChange.GetProperty("claims");
        Assert.Equal(Email, claimsAfterEmailChange.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(Email, claimsAfterEmailChange.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal(originalNameIdentifier, infoClaims.GetProperty(ClaimTypes.NameIdentifier).GetString());

        // And now the email has changed, the refresh token is invalidated by the security stamp.
        AssertUnauthorizedAndEmpty(await client.PostAsJsonAsync("/identity/refresh", new { RefreshToken = originalRefreshToken }));

        // We will finally see all the claims updated after logging in again.
        await LoginAsync(client, email: newEmail);

        var infoAfterFinalLogin = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        Assert.Equal(newEmail, infoAfterFinalLogin.GetProperty("email").GetString());
        Assert.True(infoAfterFinalLogin.GetProperty("isEmailConfirmed").GetBoolean());

        var claimsAfterFinalLogin = infoAfterFinalLogin.GetProperty("claims");
        Assert.Equal(newEmail, claimsAfterFinalLogin.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(newEmail, claimsAfterFinalLogin.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal(originalNameIdentifier, infoClaims.GetProperty(ClaimTypes.NameIdentifier).GetString());
    }

    [Fact]
    public async Task CannotUpdateClaimsDuringInfoPostWithCookies()
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
            services.Configure<IdentityOptions>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
            });
        });
        using var client = app.GetTestClient();

        AssertUnauthorizedAndEmpty(await client.GetAsync("/identity/account/info"));

        await RegisterAsync(client);
        await LoginWithEmailConfirmationAsync(client, emailSender);

        // Clear bearer token. We just used the common login email for convenient email verification.
        client.DefaultRequestHeaders.Clear();
        var loginResponse = await client.PostAsJsonAsync("/identity/login?cookieMode=true", new { Email, Password });
        ApplyCookies(client, loginResponse);

        var infoResponse = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        Assert.Equal(Email, infoResponse.GetProperty("email").GetString());
        var infoClaims = infoResponse.GetProperty("claims");
        Assert.Equal("pwd", infoClaims.GetProperty("amr").GetString());
        Assert.Equal(Email, infoClaims.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(Email, infoClaims.GetProperty(ClaimTypes.Email).GetString());

        var originalNameIdentifier = infoResponse.GetProperty("claims").GetProperty(ClaimTypes.NameIdentifier).GetString();
        var newEmail = $"NewEmailPrefix-{Email}";

        var infoPostResponse = await client.PostAsJsonAsync("/identity/account/info", new { newEmail });
        // There are no cookie updates because nothing has changed yet.
        Assert.False(infoPostResponse.Headers.Contains(HeaderNames.SetCookie));

        var infoPostContent = await infoPostResponse.Content.ReadFromJsonAsync<JsonElement>();
        // The email isn't updated until the email is confirmed.
        Assert.Equal(Email, infoPostContent.GetProperty("email").GetString());

        // The claims have not been updated to match.
        var infoPostClaims = infoPostContent.GetProperty("claims");
        Assert.Equal(Email, infoPostClaims.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal(originalNameIdentifier, infoClaims.GetProperty(ClaimTypes.NameIdentifier).GetString());

        // Two emails have now been sent. The first was sent during registration. And the second for the email change.
        Assert.Equal(2, emailSender.Emails.Count);
        var email = emailSender.Emails[1];

        Assert.Equal("Confirm your email", email.Subject);
        Assert.Equal(newEmail, email.Address);

        var emailConfirmationResponse = await client.GetAsync(GetEmailConfirmationLink(email));
        // Even though the user does change during this request, we still don't refresh the cookie, because this
        // request doesn't rely on authentication. It's entirely possible the client is logged in as a different user.
        Assert.False(emailConfirmationResponse.Headers.Contains(HeaderNames.SetCookie));
        AssertOk(emailConfirmationResponse);

        var infoAfterEmailChange = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        // The email is immediately updated after the email is confirmed.
        Assert.Equal(newEmail, infoAfterEmailChange.GetProperty("email").GetString());

        // The email still won't be available as a claim until we get a new cookie.
        var claimsAfterEmailChange = infoAfterEmailChange.GetProperty("claims");
        Assert.Equal(Email, claimsAfterEmailChange.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal(originalNameIdentifier, infoClaims.GetProperty(ClaimTypes.NameIdentifier).GetString());

        // We will finally see all the claims updated after logging in again.
        var secondLoginResponse = await client.PostAsJsonAsync("/identity/login?cookieMode=true", new { Email = newEmail, Password });
        ApplyCookies(client, secondLoginResponse);

        var infoAfterFinalLogin = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        Assert.Equal(newEmail, infoAfterFinalLogin.GetProperty("email").GetString());

        var claimsAfterFinalLogin = infoAfterFinalLogin.GetProperty("claims");
        Assert.Equal(newEmail, claimsAfterFinalLogin.GetProperty(ClaimTypes.Name).GetString());
        Assert.Equal(newEmail, claimsAfterFinalLogin.GetProperty(ClaimTypes.Email).GetString());
        Assert.Equal(originalNameIdentifier, infoClaims.GetProperty(ClaimTypes.NameIdentifier).GetString());
    }

    [Fact]
    public async Task CanChangePasswordWithoutResetEmail()
    {
        await using var app = await CreateAppAsync();
        using var client = app.GetTestClient();

        await RegisterAsync(client);
        await LoginAsync(client);

        var newPassword = $"{Password}!";

        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/info", new { newPassword }),
            "OldPasswordRequired");
        AssertOk(await client.PostAsJsonAsync("/identity/account/info", new { OldPassword = Password, newPassword }));

        client.DefaultRequestHeaders.Clear();

        // We can immediately log in with the new password
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "Failed");
        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password = newPassword }));
    }

    [Fact]
    public async Task MustSendValidRequestToSendEmailChangeConfirmation()
    {
        var emailSender = new TestEmailSender();

        await using var app = await CreateAppAsync(services =>
        {
            AddIdentityApiEndpoints(services);
            services.AddSingleton<IEmailSender>(emailSender);
        });
        using var client = app.GetTestClient();

        await RegisterAsync(client);

        // We're not going to bother to confirm the original email, but it should be there.
        Assert.Single(emailSender.Emails);
        emailSender.Emails.Clear();

        await LoginAsync(client);

        var newEmail = $"New-{Email}";
        var newPassword = $"{Password}!";

        await AssertValidationProblemAsync(await client.PostAsJsonAsync("/identity/account/info", new { newPassword, newEmail }),
            "OldPasswordRequired");

        // Since the request is invalid, no change email confirmation was sent.
        Assert.Empty(emailSender.Emails);

        // We can in fact update multiple things at once if we do it correctly, though the response wont show a email update until we confirm the email.
        var infoPostResponse = await client.PostAsJsonAsync("/identity/account/info", new { OldPassword = Password, newPassword, newEmail });

        var infoPostContent = await infoPostResponse.Content.ReadFromJsonAsync<JsonElement>();
        // The email isn't updated until the email is confirmed.
        Assert.Equal(Email, infoPostContent.GetProperty("email").GetString());
        Assert.False(infoPostContent.GetProperty("isEmailConfirmed").GetBoolean());

        // We cannot login with the new email yet.
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email = newEmail, Password = newPassword }),
            "Failed");
        // And we cannot login with the old email and password either.
        await AssertProblemAsync(await client.PostAsJsonAsync("/identity/login", new { Email, Password }),
            "Failed");
        // We'll have to use the old email with the new password until we confirm the new email.
        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email, Password = newPassword }));

        // Confirm the email change.
        var changeEmail = Assert.Single(emailSender.Emails);
        Assert.Equal(newEmail, changeEmail.Address);
        AssertOk(await client.GetAsync(GetEmailConfirmationLink(changeEmail)));

        var infoGetContent = await client.GetFromJsonAsync<JsonElement>("/identity/account/info");
        // The email isn't updated until the email is confirmed.
        Assert.Equal(newEmail, infoGetContent.GetProperty("email").GetString());
        Assert.True(infoGetContent.GetProperty("isEmailConfirmed").GetBoolean());

        // We can now login with the new email too.
        AssertOk(await client.PostAsJsonAsync("/identity/login", new { Email = newEmail, Password = newPassword }));
    }

    private async Task<WebApplication> CreateAppAsync<TUser, TContext>(Action<IServiceCollection>? configureServices, bool autoStart = true)
        where TUser : class, new()
        where TContext : DbContext
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(LoggerFactory);
        builder.Services.AddAuthorization();

        var dbConnection = new SqliteConnection("DataSource=:memory:");
        // Dispose SqliteConnection with host by registering as a singleton factory.
        builder.Services.AddSingleton(_ => dbConnection);

        configureServices ??= services => AddIdentityApiEndpoints<TUser, TContext>(services);
        configureServices(builder.Services);

        var app = builder.Build();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGroup("/identity").MapIdentityApi<TUser>();

        var authGroup = app.MapGroup("/auth").RequireAuthorization();
        authGroup.MapGet("/hello",
            (ClaimsPrincipal user) => $"Hello, {user.Identity?.Name}!");

        await dbConnection.OpenAsync();
        await app.Services.GetRequiredService<TContext>().Database.EnsureCreatedAsync();

        if (autoStart)
        {
            await app.StartAsync();
        }

        return app;
    }

    private static IdentityBuilder AddIdentityApiEndpoints<TUser, TContext>(IServiceCollection services)
        where TUser : class, new()
        where TContext : DbContext
    {
        return services.AddDbContext<TContext>((sp, options) => options.UseSqlite(sp.GetRequiredService<SqliteConnection>()))
            .AddIdentityApiEndpoints<TUser>().AddEntityFrameworkStores<TContext>();
    }

    private static IdentityBuilder AddIdentityApiEndpoints(IServiceCollection services)
        => AddIdentityApiEndpoints<ApplicationUser, ApplicationDbContext>(services);

    private static IdentityBuilder AddIdentityApiEndpointsBearerOnly(IServiceCollection services)
    {
        services
            .AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        return services
            .AddDbContext<ApplicationDbContext>((sp, options) => options.UseSqlite(sp.GetRequiredService<SqliteConnection>()))
            .AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();
    }

    private Task<WebApplication> CreateAppAsync(Action<IServiceCollection>? configureServices = null)
        => CreateAppAsync<ApplicationUser, ApplicationDbContext>(configureServices);

    private static Dictionary<string, Action<IServiceCollection>> AddIdentityActions { get; } = new()
    {
        [nameof(AddIdentityApiEndpoints)] = services => AddIdentityApiEndpoints(services),
        [nameof(AddIdentityApiEndpointsBearerOnly)] = services => AddIdentityApiEndpointsBearerOnly(services),
    };

    public static object[][] AddIdentityModes => AddIdentityActions.Keys.Select(key => new object[] { key }).ToArray();

    private static string GetEmailConfirmationLink(TestEmail email)
    {
        // Update if we add more links to the email.
        var confirmationMatch = Regex.Match(email.HtmlMessage, "href='(.*?)'");
        Assert.True(confirmationMatch.Success);
        Assert.Equal(2, confirmationMatch.Groups.Count);

        return WebUtility.HtmlDecode(confirmationMatch.Groups[1].Value);
    }

    private static string GetPasswordResetCode(TestEmail email)
    {
        // Update if we add more links to the email.
        var confirmationMatch = Regex.Match(email.HtmlMessage, "code: (.*?)$");
        Assert.True(confirmationMatch.Success);
        Assert.Equal(2, confirmationMatch.Groups.Count);

        return WebUtility.HtmlDecode(confirmationMatch.Groups[1].Value);
    }

    private async Task RegisterAsync(HttpClient client, string? groupPrefix = null, string? email = null)
    {
        groupPrefix ??= "/identity";
        email ??= Email;

        AssertOkAndEmpty(await client.PostAsJsonAsync($"{groupPrefix}/register", new { email, Password }));
    }

    private async Task<string> LoginAsync(HttpClient client, string? groupPrefix = null, string? email = null)
    {
        groupPrefix ??= "/identity";
        email ??= Email;

        await client.PostAsJsonAsync($"{groupPrefix}/login", new { email, Password });
        var loginResponse = await client.PostAsJsonAsync("/identity/login", new { email, Password });
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginContent.GetProperty("access_token").GetString();
        var refreshToken = loginContent.GetProperty("refresh_token").GetString();
        Assert.NotNull(accessToken);
        Assert.NotNull(refreshToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        return refreshToken;
    }

    private async Task<string> LoginWithEmailConfirmationAsync(HttpClient client, TestEmailSender emailSender, string? groupPrefix = null, string? email = null)
    {
        groupPrefix ??= "/identity";
        email ??= Email;

        var receivedEmail = emailSender.Emails.Last();

        Assert.Equal("Confirm your email", receivedEmail.Subject);
        Assert.Equal(email, receivedEmail.Address);

        await AssertProblemAsync(await client.PostAsJsonAsync($"{groupPrefix}/login", new { email, Password }),
            "NotAllowed");

        AssertOk(await client.GetAsync(GetEmailConfirmationLink(receivedEmail)));

        return await LoginAsync(client, groupPrefix, email);
    }

    private static void AssertOk(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static void AssertOkAndEmpty(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    private static void AssertBadRequestAndEmpty(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    private static void AssertUnauthorizedAndEmpty(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string detail, HttpStatusCode status = HttpStatusCode.Unauthorized)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(ReasonPhrases.GetReasonPhrase((int)status), problem.Title);
        Assert.Equal(detail, problem.Detail);
    }

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        var errorEntry = Assert.Single(problem.Errors);
        Assert.Equal(error, errorEntry.Key);
    }

    private static void ApplyCookies(HttpClient client, HttpResponseMessage response)
    {
        AssertOk(response);

        Assert.True(response.Headers.TryGetValues(HeaderNames.SetCookie, out var setCookieHeaders));
        foreach (var setCookieHeader in setCookieHeaders)
        {
            if (setCookieHeader.Split(';', 2) is not [var cookie, _])
            {
                throw new XunitException("Invalid Set-Cookie header!");
            }

            // Cookies starting with "CookieName=;" are being deleted
            if (!cookie.EndsWith("=", StringComparison.Ordinal))
            {
                client.DefaultRequestHeaders.Add(HeaderNames.Cookie, cookie);
            }
        }
    }

    private sealed class TestTokenProvider<TUser> : IUserTwoFactorTokenProvider<TUser>
        where TUser : class
    {
        public async Task<string> GenerateAsync(string purpose, UserManager<TUser> manager, TUser user)
        {
            return MakeToken(purpose, await manager.GetUserIdAsync(user));
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<TUser> manager, TUser user)
        {
            return token == MakeToken(purpose, await manager.GetUserIdAsync(user));
        }

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user)
        {
            return Task.FromResult(true);
        }

        private static string MakeToken(string purpose, string userId)
        {
            return string.Join(":", userId, purpose, "ImmaToken");
        }
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public List<TestEmail> Emails { get; set; } = new();

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Emails.Add(new(email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }

    private sealed record TestEmail(string Address, string Subject, string HtmlMessage);
}
