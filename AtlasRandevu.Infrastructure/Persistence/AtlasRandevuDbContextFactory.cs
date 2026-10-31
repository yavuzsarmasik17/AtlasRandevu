
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AtlasRandevu.Infrastructure.Persistence
{
    public class AtlasRandevuDbContextFactory
        : IDesignTimeDbContextFactory<AtlasRandevuDbContext>
    {
        public AtlasRandevuDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder =
                new DbContextOptionsBuilder<AtlasRandevuDbContext>();

            optionsBuilder.UseSqlServer(
                @"Server=DESKTOP-G1OIEJD\YAVUZ;Database=AtlasRandevuDb;Trusted_Connection=True;TrustServerCertificate=True;");

            return new AtlasRandevuDbContext(optionsBuilder.Options);
        }
    }
}
