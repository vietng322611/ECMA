namespace ECMA.Domain.Interfaces;

public interface IRegistrationCodeGenerator
{
    // BR-REG-03: "REG-" + 12 random ASCII uppercase letters/digits.
    string Generate();
}
