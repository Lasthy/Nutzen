using Microsoft.Extensions.DependencyInjection;
using Nutzen.Common.MemoryCache;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nutzen.Common;

public static class Extension
{
    public static IServiceCollection AddNutzenCommon(this IServiceCollection services)
    {
        services.AddMemoryCacheInterceptor();

        return services;
    }
}
