using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Orchard.Liquid.Services
{
    public class LiquidTemplateManager : ILiquidTemplateManager
    {
        private readonly IMemoryCache _memoryCache;
        private readonly LiquidOptions _liquidOptions;
        private readonly IServiceProvider _serviceProvider;

        public LiquidTemplateManager(
            IMemoryCache memoryCache,
            IOptions<LiquidOptions> options,
            IServiceProvider serviceProvider)
        {
            _memoryCache = memoryCache;
            _liquidOptions = options.Value;
            _serviceProvider = serviceProvider;
        }

        public Task RenderAsync(string source, TextWriter textWriter, TextEncoder encoder, TemplateContext context)
        {
            if (String.IsNullOrWhiteSpace(source))
            {
                return Task.CompletedTask;
            }

            var errors = Enumerable.Empty<string>();

            var result = _memoryCache.GetOrCreate<IFluidTemplate>(source, (ICacheEntry e) =>
            {
                if (FluidTemplate.TryParse(source, out var parsed, out errors))
                {
                    // Define a default sliding expiration to prevent the 
                    // cache from being filled and still apply some micro-caching
                    // in case the template is use commonly
                    e.SetSlidingExpiration(TimeSpan.FromSeconds(30));
                    return parsed;
                }
                else
                {
                    return null;
                }
            });

            if (result == null)
            {
                FluidTemplate.TryParse(String.Join(System.Environment.NewLine, errors), out result, out errors);
            }

            foreach (var registration in _liquidOptions.FilterRegistrations)
            {
                context.Filters.AddAsyncFilter(registration.Key, (input, arguments, ctx) =>
                {
                    var type = registration.Value;
                    var filter = _serviceProvider.GetService(registration.Value) as ILiquidFilter;
                    return filter.ProcessAsync(input, arguments, ctx);
                });
            }

            return result.RenderAsync(textWriter, encoder, context);
        }
    }
}
