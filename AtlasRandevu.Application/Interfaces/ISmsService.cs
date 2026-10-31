
using System.Threading;
using System.Threading.Tasks;

namespace AtlasRandevu.Application.Interfaces
{
    public interface ISmsService
    {
        Task<bool> SendAsync(
            string telefon,
            string mesaj,
            CancellationToken cancellationToken = default);
    }
}
