using Fgc.Catalog.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;

namespace Fgc.Catalog.Infrastructure.Search
{
    public static class OpenSearchServiceCollectionExtensions
    {
        // OpenSearch:Url obrigatório. OpenSearch:Username/Password opcionais: vazios no compose (security plugin
        // desligado); na AWS, usuário master do fine-grained access control do domínio, vindo de Secret.
        public static IServiceCollection AddOpenSearchGameSearch(this IServiceCollection services, IConfiguration configuration)
        {
            var url = configuration["OpenSearch:Url"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Missing configuration 'OpenSearch:Url' (env OpenSearch__Url).");

            var settings = new ConnectionSettings(new Uri(url))
                .DefaultIndex(OpenSearchGameSearchService.IndexName)
                .RequestTimeout(TimeSpan.FromSeconds(5));

            var username = configuration["OpenSearch:Username"];
            if (!string.IsNullOrEmpty(username))
                settings = settings.BasicAuthentication(username, configuration["OpenSearch:Password"]);

            // O client é thread-safe e mantém pool de conexões: uma instância por processo.
            services.AddSingleton<IOpenSearchClient>(new OpenSearchClient(settings));
            services.AddSingleton<IGameSearchService, OpenSearchGameSearchService>();
            return services;
        }
    }
}
