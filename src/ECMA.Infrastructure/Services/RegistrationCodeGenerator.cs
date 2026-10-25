using System.Security.Cryptography;
using ECMA.Domain.Interfaces;

namespace ECMA.Infrastructure.Services;

public sealed class RegistrationCodeGenerator : IRegistrationCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public string Generate() => "REG-" + RandomNumberGenerator.GetString(Alphabet, 12);
}
