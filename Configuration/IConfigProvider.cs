using System.Threading;
using System.Threading.Tasks;

namespace SqlWebApi.Configuration
{
    public interface IConfigProvider
    {
        Task<string> GetAsync(string key, CancellationToken cancellationToken = default);
        Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    }
}