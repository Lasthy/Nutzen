using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nutzen;

public static class Extension
{
    /// <summary>
    /// Adds Nutzen core services to the service collection.
    /// Call the generated Add{AssemblyName}Nutzen() method to register handlers and interceptors.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddNutzen();
    /// services.AddMyAssemblyNutzen(); // Generated method
    /// </code>
    /// </example>
    public static IServiceCollection AddNutzen(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IEventBus, EventBus>();

        return services;
    }
}

