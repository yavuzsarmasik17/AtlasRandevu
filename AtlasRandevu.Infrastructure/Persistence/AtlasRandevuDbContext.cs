
using AtlasRandevu.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AtlasRandevu.Infrastructure.Persistence
{
    public class AtlasRandevuDbContext : IdentityDbContext
    {
        public AtlasRandevuDbContext(
            DbContextOptions<AtlasRandevuDbContext> options)
            : base(options)
        {
        }

        // =====================================
        // VERİTABANI TABLOLARI
        // =====================================

        public DbSet<Isletme> Isletmeler { get; set; } = null!;
        public DbSet<Sube> Subeler { get; set; } = null!;
        public DbSet<IsletmeVergiBilgisi> IsletmeVergiBilgileri { get; set; } = null!;
        public DbSet<IsletmeBelgesi> IsletmeBelgeleri { get; set; } = null!;
        public DbSet<BelgeDogrulama> BelgeDogrulamalari { get; set; } = null!;
        public DbSet<Sektor> Sektorler { get; set; } = null!;
        public DbSet<IsletmeKullanicisi> IsletmeKullanicilari { get; set; } = null!;
        public DbSet<TelefonDogrulama> TelefonDogrulamalari { get; set; } = null!;
        public DbSet<IsletmeFotografi> IsletmeFotograflari => Set<IsletmeFotografi>();

        public DbSet<MusteriProfili> MusteriProfilleri { get; set; } = null!;
        public DbSet<SubeCalismaSaati> SubeCalismaSaatleri { get; set; } = null!;

        public DbSet<Hizmet> Hizmetler { get; set; } = null!;
        public DbSet<Randevu> Randevular { get; set; } = null!;
        public DbSet<Personel> Personeller { get; set; } = null!;
        public DbSet<RandevuHizmeti> RandevuHizmetleri { get; set; } = null!;
        public DbSet<MusteriYorumu> MusteriYorumlari { get; set; } = null!;
        public DbSet<IsletmeBildirimi> IsletmeBildirimleri { get; set; } = null!;
        public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;

        // Manuel saat kapatma
        public DbSet<PersonelKapaliZaman> PersonelKapaliZamanlar
        { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // =====================================
            // ŞUBE ÇALIŞMA SAATLERİ
            // =====================================

            builder.Entity<SubeCalismaSaati>(entity =>
            {
                entity.ToTable("SubeCalismaSaatleri");

                entity.HasIndex(x => new { x.SubeId, x.Gun })
                    .IsUnique();

                entity.HasOne(x => x.Sube)
                    .WithMany()
                    .HasForeignKey(x => x.SubeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // TELEFON DOĞRULAMA
            // =====================================

            builder.Entity<TelefonDogrulama>(entity =>
            {
                entity.Property(x => x.Telefon)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(x => x.KodOzeti)
                    .HasMaxLength(256)
                    .IsRequired();

                entity.HasIndex(x => new
                {
                    x.Telefon,
                    x.SonGecerlilikTarihi
                });
            });

            // =====================================
            // İŞLETME KULLANICISI
            // =====================================

            builder.Entity<IsletmeKullanicisi>(entity =>
            {
                entity.Property(x => x.KullaniciId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(x => x.Rol)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.HasIndex(x => new
                {
                    x.IsletmeId,
                    x.KullaniciId
                }).IsUnique();

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<IdentityUser>()
                    .WithMany()
                    .HasForeignKey(x => x.KullaniciId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // SEKTÖRLER
            // =====================================

            builder.Entity<Sektor>(entity =>
            {
                entity.Property(x => x.Ad)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.Slug)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.HasIndex(x => x.Slug)
                    .IsUnique();

                entity.HasData(
                    new Sektor
                    {
                        Id = 1,
                        Ad = "Erkek Berberi",
                        Slug = "erkek-berberi",
                        AktifMi = true,
                        Sira = 1
                    },
                    new Sektor
                    {
                        Id = 2,
                        Ad = "Kadın Kuaförü",
                        Slug = "kadin-kuaforu",
                        AktifMi = true,
                        Sira = 2
                    },
                    new Sektor
                    {
                        Id = 3,
                        Ad = "Güzellik Salonu",
                        Slug = "guzellik-salonu",
                        AktifMi = true,
                        Sira = 3
                    }
                );
            });

            // =====================================
            // İŞLETME FOTOĞRAFLARI
            // =====================================

            builder.Entity<IsletmeFotografi>(entity =>
            {
                entity.ToTable("IsletmeFotograflari");

                entity.Property(x => x.DosyaYolu)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.YuklenmeTarihi)
                    .IsRequired();

                entity.Property(x => x.Sira)
                    .HasDefaultValue(0);

                entity.HasIndex(x => new
                {
                    x.IsletmeId,
                    x.Sira
                });

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // İŞLETMELER
            // =====================================

            builder.Entity<Isletme>(entity =>
            {
                entity.HasOne(x => x.Sektor)
                    .WithMany(x => x.Isletmeler)
                    .HasForeignKey(x => x.SektorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(x => x.Subeler)
                    .WithOne(x => x.Isletme)
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(x => x.Ad)
                    .HasMaxLength(200)
                    .IsRequired();
            });

            // =====================================
            // ŞUBELER
            // =====================================

            builder.Entity<Sube>(entity =>
            {
                entity.Property(x => x.Ad)
                    .HasMaxLength(150)
                    .IsRequired();
            });

            // =====================================
            // VERGİ BİLGİLERİ
            // =====================================

            builder.Entity<IsletmeVergiBilgisi>(entity =>
            {
                entity.HasIndex(x => x.IsletmeId)
                    .IsUnique();

                entity.Property(x => x.VergiNumarasi)
                    .HasMaxLength(11)
                    .IsRequired();

                entity.Property(x => x.VergiDairesi)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.TicariUnvan)
                    .HasMaxLength(250)
                    .IsRequired();

                entity.HasOne(x => x.Isletme)
                    .WithOne()
                    .HasForeignKey<IsletmeVergiBilgisi>(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // İŞLETME BELGELERİ
            // =====================================

            builder.Entity<IsletmeBelgesi>(entity =>
            {
                entity.Property(x => x.BelgeTuru)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.DosyaYolu)
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(x => x.DosyaAdi)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(x => x.IcerikTuru)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // BELGE DOĞRULAMA
            // =====================================

            builder.Entity<BelgeDogrulama>(entity =>
            {
                entity.Property(x => x.Durum)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(x => x.ResmiDogrulamaDurumu)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.HasOne(x => x.Belge)
                    .WithMany()
                    .HasForeignKey(x => x.BelgeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // HİZMETLER
            // =====================================

            builder.Entity<Hizmet>(entity =>
            {
                entity.ToTable("Hizmetler", table =>
                {
                    table.HasCheckConstraint(
                        "CK_Hizmetler_Fiyat",
                        "[Fiyat] >= 0");

                    table.HasCheckConstraint(
                        "CK_Hizmetler_SureDakika",
                        "[SureDakika] > 0");
                });

                entity.Property(x => x.Ad)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Aciklama)
                    .HasMaxLength(2000);

                entity.Property(x => x.Fiyat)
                    .HasPrecision(18, 2);

                entity.HasIndex(x => x.IsletmeId);

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // PERSONEL YÖNETİMİ
            // =====================================

            builder.Entity<Personel>(entity =>
            {
                entity.ToTable("Personeller");

                entity.Property(x => x.AdSoyad)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(x => x.Unvan)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(x => x.Telefon)
                    .HasMaxLength(20);

                entity.Property(x => x.FotografYolu)
                    .HasMaxLength(500);

                entity.HasIndex(x => new
                {
                    x.IsletmeId,
                    x.SubeId,
                    x.AktifMi
                });

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Sube)
                    .WithMany()
                    .HasForeignKey(x => x.SubeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // PERSONEL KAPALI ZAMANLAR
            // =====================================

            builder.Entity<PersonelKapaliZaman>(entity =>
            {
                entity.ToTable("PersonelKapaliZamanlar", table =>
                {
                    table.HasCheckConstraint(
                        "CK_PersonelKapaliZamanlar_Tarih",
                        "[BitisTarihi] > [BaslangicTarihi]");
                });

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Aciklama)
                    .HasMaxLength(300);

                entity.HasIndex(x => new
                {
                    x.PersonelId,
                    x.BaslangicTarihi,
                    x.BitisTarihi
                });

                entity.HasOne<Isletme>()
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Sube>()
                    .WithMany()
                    .HasForeignKey(x => x.SubeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Personel>()
                    .WithMany()
                    .HasForeignKey(x => x.PersonelId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // RANDEVU - ÇOKLU HİZMET İLİŞKİSİ
            // =====================================

            builder.Entity<RandevuHizmeti>(entity =>
            {
                entity.ToTable("RandevuHizmetleri");

                entity.Property(x => x.Fiyat)
                    .HasPrecision(18, 2);

                entity.HasIndex(x => new
                {
                    x.RandevuId,
                    x.HizmetId
                }).IsUnique();

                entity.HasOne(x => x.Randevu)
                    .WithMany()
                    .HasForeignKey(x => x.RandevuId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Hizmet)
                    .WithMany()
                    .HasForeignKey(x => x.HizmetId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // RANDEVULAR
            // =====================================

            builder.Entity<Randevu>(entity =>
            {
                entity.HasOne(x => x.Personel)
                    .WithMany()
                    .HasForeignKey(x => x.PersonelId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new
                {
                    x.PersonelId,
                    x.BaslangicTarihi,
                    x.BitisTarihi
                });

                entity.ToTable("Randevular", table =>
                {
                    table.HasCheckConstraint(
                        "CK_Randevular_Tarih",
                        "[BitisTarihi] > [BaslangicTarihi]");

                    table.HasCheckConstraint(
                        "CK_Randevular_Durum",
                        "[Durum] IN (" +
                        "'OnayBekliyor'," +
                        "'Onaylandi'," +
                        "'Reddedildi'," +
                        "'IptalEdildi'," +
                        "'Geldi'," +
                        "'Gelmedi'," +
                        "'Tamamlandi')");
                });

                // Online müşteride kullanıcı ID bulunur.
                // Manuel randevuda NULL olabilir.
                entity.Property(x => x.MusteriKullaniciId)
                    .HasMaxLength(450)
                    .IsRequired(false);

                entity.Property(x => x.RandevuKaynagi)
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasDefaultValue("Online");

                entity.Property(x => x.ManuelMusteriAdi)
                    .HasMaxLength(150);

                entity.Property(x => x.ManuelMusteriTelefonu)
                    .HasMaxLength(20);

                entity.Property(x => x.Durum)
                    .HasMaxLength(30)
                    .IsRequired();

                // Ortak QR ile müşteri giriş bilgileri
                entity.Property(x => x.GelisTarihi);

                entity.Property(x => x.GelisYontemi)
                    .HasMaxLength(30);

                entity.Property(x => x.GelisDogrulandiMi)
                    .HasDefaultValue(false);

                entity.HasIndex(x => new
                {
                    x.MusteriKullaniciId,
                    x.Durum,
                    x.BaslangicTarihi
                });

                entity.Property(x => x.MusteriNotu)
                    .HasMaxLength(1000);

                entity.Property(x => x.IsletmeAciklamasi)
                    .HasMaxLength(1000);

                entity.HasIndex(x => new
                {
                    x.SubeId,
                    x.BaslangicTarihi,
                    x.BitisTarihi
                });

                entity.HasIndex(x => new
                {
                    x.MusteriKullaniciId,
                    x.BaslangicTarihi
                });

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Sube)
                    .WithMany()
                    .HasForeignKey(x => x.SubeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Hizmet)
                    .WithMany()
                    .HasForeignKey(x => x.HizmetId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<IdentityUser>()
                    .WithMany()
                    .HasForeignKey(x => x.MusteriKullaniciId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // KALICI İŞLETME BİLDİRİMLERİ
            // =====================================

            builder.Entity<IsletmeBildirimi>(entity =>
            {
                entity.ToTable("IsletmeBildirimleri");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Tur)
                    .HasMaxLength(40)
                    .IsRequired();

                entity.Property(x => x.Baslik)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(x => x.Mesaj)
                    .HasMaxLength(1000)
                    .IsRequired();

                entity.Property(x => x.OlusturulmaTarihi)
                    .IsRequired();

                entity.Property(x => x.OkunduMu)
                    .HasDefaultValue(false);

                entity.HasIndex(x => new
                {
                    x.IsletmeId,
                    x.OkunduMu,
                    x.OlusturulmaTarihi
                });

                entity.HasOne<Isletme>()
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<Randevu>()
                    .WithMany()
                    .HasForeignKey(x => x.RandevuId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // WEB PUSH ABONELİKLERİ
            // =====================================

            builder.Entity<PushSubscription>(entity =>
            {
                entity.ToTable("PushSubscriptions");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.KullaniciId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(x => x.Endpoint)
                    .HasMaxLength(2048)
                    .IsRequired();

                entity.Property(x => x.P256dh)
                    .HasMaxLength(512)
                    .IsRequired();

                entity.Property(x => x.Auth)
                    .HasMaxLength(256)
                    .IsRequired();

                entity.HasIndex(x => x.Endpoint)
                    .IsUnique();

                entity.HasIndex(x => new
                {
                    x.IsletmeId,
                    x.KullaniciId,
                    x.AktifMi
                });

                entity.HasOne<Isletme>()
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<IdentityUser>()
                    .WithMany()
                    .HasForeignKey(x => x.KullaniciId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================
            // MÜŞTERİ YORUMLARI
            // =====================================

            builder.Entity<MusteriYorumu>(entity =>
            {
                entity.ToTable("MusteriYorumlari", table =>
                {
                    table.HasCheckConstraint(
                        "CK_MusteriYorumlari_Puan",
                        "[Puan] BETWEEN 1 AND 5");
                });

                entity.Property(x => x.MusteriKullaniciId)
                    .HasMaxLength(450)
                    .IsRequired();

                entity.Property(x => x.Yorum)
                    .HasMaxLength(3000)
                    .IsRequired();

                entity.Property(x => x.IsletmeCevabi)
                    .HasMaxLength(2000);

                entity.HasIndex(x => x.RandevuId)
                    .IsUnique();

                entity.HasIndex(x => new
                {
                    x.IsletmeId,
                    x.YayinlandiMi
                });

                entity.HasOne(x => x.Isletme)
                    .WithMany()
                    .HasForeignKey(x => x.IsletmeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Randevu)
                    .WithMany()
                    .HasForeignKey(x => x.RandevuId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne<IdentityUser>()
                    .WithMany()
                    .HasForeignKey(x => x.MusteriKullaniciId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
