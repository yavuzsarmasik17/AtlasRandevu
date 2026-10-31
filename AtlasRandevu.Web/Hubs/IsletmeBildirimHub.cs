using AtlasRandevu.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AtlasRandevu.Web.Hubs
{
    [Authorize(Policy = "OnayliIsletmeSahibi")]
    public sealed class IsletmeBildirimHub : Hub
    {
        private readonly AtlasRandevuDbContext _db;
        private readonly UserManager<IdentityUser> _users;

        public IsletmeBildirimHub(AtlasRandevuDbContext db, UserManager<IdentityUser> users)
        {
            _db = db;
            _users = users;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = _users.GetUserId(Context.User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                Context.Abort();
                return;
            }

            var isletmeIdleri = await _db.IsletmeKullanicilari.AsNoTracking()
                .Where(x => x.KullaniciId == userId && x.AktifMi &&
                            x.Rol == "IsletmeSahibi" && x.Isletme.Durum == "Onaylandi")
                .Select(x => x.IsletmeId)
                .Distinct()
                .ToListAsync();

            if (isletmeIdleri.Count == 0)
            {
                Context.Abort();
                return;
            }

            foreach (var id in isletmeIdleri)
                await Groups.AddToGroupAsync(Context.ConnectionId, GrupAdi(id));

            await base.OnConnectedAsync();
        }

        public static string GrupAdi(int isletmeId) => $"isletme-{isletmeId}";
    }
}
