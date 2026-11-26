using Microsoft.EntityFrameworkCore;
namespace PostTxHub.Worker.Persistence;

public sealed class PostTxHubDbContext(DbContextOptions<PostTxHubDbContext> options) : DbContext(options)
{
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tx = modelBuilder.Entity<TransactionEntity>();

        tx.HasKey(t => t.Id);
        tx.Property(t => t.Id).ValueGeneratedNever();
        tx.Property(t => t.ExternalId).HasMaxLength(64);
        tx.HasIndex(t => t.ExternalId).IsUnique();
        tx.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        tx.Property(t => t.Amount).HasPrecision(19, 4);
        tx.Property(t => t.Currency).HasMaxLength(3).IsFixedLength();
        tx.Property(t => t.AccountId).HasMaxLength(34);

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Type).HasMaxLength(100).IsRequired();
            e.Property(o => o.Payload).IsRequired();
            e.HasIndex(o => o.SentAt);
        });
    }
}