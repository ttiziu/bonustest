using BonusTest.Application.Bonuses;
using Microsoft.Extensions.DependencyInjection;
using BonusTest.Application.Catalogs;

namespace BonusTest.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();

        services.AddScoped<BonusService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
