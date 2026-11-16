using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RobotMissionMS.DataAccess.Entities;
using RobotMissionMS.DataAccess.Persistence;

namespace RobotMissionMS.Seeder;

internal static class AdminBootstrapper
{
    public static async Task RunAsync(RobotMissionDbContext db)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("Run --create-admin in an interactive terminal.");

        Console.Write("Admin username: ");
        var username = Console.ReadLine()?.Trim();
        Console.Write("Full name: ");
        var fullName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 100
            || string.IsNullOrWhiteSpace(fullName) || fullName.Length > 200)
            throw new InvalidOperationException("Username (1-100) and full name (1-200) are required.");

        var password = ReadPassword("Password (at least 8 characters, letters and digits): ");
        var confirmation = ReadPassword("Confirm password: ");
        if (password.Length < 8 || password.Length > 1024
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit)
            || string.Equals(password, username, StringComparison.OrdinalIgnoreCase)
            || password != confirmation)
            throw new InvalidOperationException("Password is invalid or confirmation does not match.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(726667, 1)");
        if (await db.Users.AnyAsync(user => user.Role.Name == "ADMIN" && user.IsActive))
            throw new InvalidOperationException("An active Admin already exists. Use that account; bootstrap made no changes.");
        if (await db.Users.AnyAsync(user => user.Username == username))
            throw new InvalidOperationException("Username already exists. Bootstrap does not overwrite accounts.");
        var role = await db.Roles.SingleOrDefaultAsync(role => role.Name == "ADMIN")
            ?? throw new InvalidOperationException("ADMIN role is missing. Run reference seed --apply first.");

        var admin = new User
        {
            Id = Guid.NewGuid(), Username = username, FullName = fullName,
            RoleId = role.Id, IsActive = true, ZoneId = null,
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine("Admin created. Log in through POST /api/v1/auth/login with the credentials you entered.");
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var value = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0) value.Length--;
            }
            else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
        Console.WriteLine();
        return value.ToString();
    }
}
