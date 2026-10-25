using ECMA.Domain.Interfaces;
using ECMA.Infrastructure.Data;
using ECMA.Infrastructure.Repositories;
using ECMA.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECMA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EcmaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ECMA")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<EMCA.Application.Interfaces.IEventService, EMCA.Application.Services.EventService>();
        services.AddScoped<IPasswordService, PasswordHasherAdapter>();
        services.AddScoped<ISecurityStampGenerator, SecurityStampGenerator>();
        services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        services.AddScoped<IRegistrationCodeGenerator, RegistrationCodeGenerator>();
        services.AddScoped<IClock, Clock>();
        services.AddScoped<AdminSeeder>();

        return services;
    }
}