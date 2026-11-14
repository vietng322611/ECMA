using ECMA.Domain.Enums;

namespace ECMA.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string SecurityStamp { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { } // For EF

    public User(
        string username,
        string passwordHash,
        string securityStamp,
        string fullName,
        UserRole role,
        UserStatus status)
    {
        Username = username;
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
        FullName = fullName;
        Role = role;
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateFullName(string fullName)
    {
        FullName = fullName;
    }

    public void UpdatePassword(string passwordHash, string securityStamp)
    {
        PasswordHash = passwordHash;
        SecurityStamp = securityStamp;
    }

    public void SetRole(UserRole role) => Role = role;

    public void RotateSecurityStamp(string stamp) => SecurityStamp = stamp;

    public void SetStatus(UserStatus status)
    {
        Status = status;
    }
}