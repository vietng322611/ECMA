using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Web.Services;
using Xunit;

namespace ECMA.Domain.Tests;

public sealed class Tv5FeatureTests
{
    [Theory]
    [InlineData("  reg-123456abcdef  ", "REG-123456ABCDEF")]
    [InlineData("REG-AAAAAAAAAAAA", "REG-AAAAAAAAAAAA")]
    [InlineData("reg-１２３４５６７８９０１２", "REG-１２３４５６７８９０１２")]
    public void CheckIn_Normalizes_Only_Ascii_And_Whitespace(string input, string expected)
    {
        Assert.Equal(expected, CheckInService.NormalizeCode(input));
    }

    [Fact]
    public void Changing_Role_And_Status_Does_Not_Remove_User_Identity()
    {
        var user = new User("tester", "hash", "stamp", "Tên kiểm thử", UserRole.Organizer, UserStatus.Active);
        var createdAt = user.CreatedAt;
        user.SetRole(UserRole.Attendee);
        user.SetStatus(UserStatus.Inactive);
        user.RotateSecurityStamp("new-stamp");
        Assert.Equal(UserRole.Attendee, user.Role);
        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.Equal("new-stamp", user.SecurityStamp);
        Assert.Equal("tester", user.Username);
        Assert.Equal(createdAt, user.CreatedAt);
    }
}
