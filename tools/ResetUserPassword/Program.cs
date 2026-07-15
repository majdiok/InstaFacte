using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

if (args.Length < 2)
{
    await Console.Error.WriteLineAsync("Usage: ResetUserPassword <email> <newPassword>");
    return 1;
}

var email = args[0].Trim();
var newPassword = args[1];

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Backend", "FactuTrust.API")))
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var connectionString = config.GetConnectionString("MasterConnection")
    ?? throw new InvalidOperationException("MasterConnection not configured.");

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddDbContext<MasterDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(MasterDbContext).Assembly.FullName)));

services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
    options.Password.RequiredUniqueChars = 4;
})
.AddEntityFrameworkStores<MasterDbContext>()
.AddDefaultTokenProviders();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

var user = await userManager.FindByEmailAsync(email);
if (user is null)
{
    await Console.Error.WriteLineAsync($"User not found: {email}");
    return 2;
}

var token = await userManager.GeneratePasswordResetTokenAsync(user);
var reset = await userManager.ResetPasswordAsync(user, token, newPassword);
if (!reset.Succeeded)
{
    await Console.Error.WriteLineAsync(string.Join("; ", reset.Errors.Select(e => e.Description)));
    return 3;
}

user.AccessFailedCount = 0;
user.LockoutEnd = null;
await userManager.UpdateAsync(user);

await Console.Out.WriteLineAsync($"Password reset for {email}");
return 0;
