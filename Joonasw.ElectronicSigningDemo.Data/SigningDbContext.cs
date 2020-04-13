using Microsoft.EntityFrameworkCore;

namespace Joonasw.ElectronicSigningDemo.Data
{
    public class SigningDbContext : DbContext
    {
        public SigningDbContext(DbContextOptions<SigningDbContext> options)
            : base(options)
        {
        }

        public DbSet<SigningRequest> Requests { get; set; }
        public DbSet<Signer> Signers { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Ensure an email is unique per request
            builder.Entity<Signer>()
                .HasIndex(nameof(Signer.Email), nameof(Signer.RequestId))
                .IsUnique();

            builder.Entity<SigningRequest>()
                .Property(r => r.CreatedAt)
                .HasDefaultValueSql("SYSDATETIMEOFFSET()");
        }
    }
}
