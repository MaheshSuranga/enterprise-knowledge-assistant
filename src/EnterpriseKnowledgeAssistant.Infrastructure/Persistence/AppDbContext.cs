using System.Text.Json;
using EnterpriseKnowledgeAssistant.Application.Interfaces;
using EnterpriseKnowledgeAssistant.Domain.Entities;
using EnterpriseKnowledgeAssistant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace EnterpriseKnowledgeAssistant.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Value comparers for collection properties with JSON conversion
        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var floatArrayComparer = new ValueComparer<float[]>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToArray());

        // Enable pgvector extension if supported by provider
        try
        {
            modelBuilder.HasPostgresExtension("vector");
            modelBuilder.HasPostgresExtension("uuid-ossp");
        }
        catch
        {
            // Non-postgres provider (e.g. InMemory) ignores pg extension
        }

        // 1. Tenant Configuration
        modelBuilder.Entity<Tenant>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Name).HasMaxLength(150).IsRequired();
            b.HasIndex(t => t.Name).IsUnique();
        });

        // 2. User Configuration
        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Email).HasMaxLength(200).IsRequired();
            b.HasOne(u => u.Tenant)
                .WithMany(t => t.Users)
                .HasForeignKey(u => u.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(u => u.Roles)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                )
                .Metadata.SetValueComparer(stringListComparer);
        });

        // 3. Document Configuration
        modelBuilder.Entity<Document>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Filename).HasMaxLength(255).IsRequired();
            b.Property(d => d.Checksum).HasMaxLength(64).IsRequired();
            b.Property(d => d.Status).HasConversion<string>();

            b.HasOne(d => d.Tenant)
                .WithMany(t => t.Documents)
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasIndex(d => new { d.TenantId, d.Checksum });
        });

        // 4. DocumentChunk Configuration (Hybrid vector & text search)
        modelBuilder.Entity<DocumentChunk>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Content).IsRequired();
            b.Property(c => c.ContentHash).HasMaxLength(64).IsRequired();

            b.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(c => c.AclRoles)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                )
                .Metadata.SetValueComparer(stringListComparer);

            b.Property(c => c.Embedding)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>()
                )
                .Metadata.SetValueComparer(floatArrayComparer);

            b.HasIndex(c => new { c.TenantId, c.DocumentId });
        });

        // 5. Conversation & ChatMessage Configuration
        modelBuilder.Entity<Conversation>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Property(m => m.RefusalReason).HasConversion<string>();
        });

        // 6. AuditLog Configuration
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => new { a.TenantId, a.CreatedAt });
        });

        // Seed initial enterprise demo tenants & users
        SeedInitialData(modelBuilder);
    }

    private static void SeedInitialData(ModelBuilder modelBuilder)
    {
        var tenantAcmeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenantGlobexId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        modelBuilder.Entity<Tenant>().HasData(
            new Tenant
            {
                Id = tenantAcmeId,
                Name = "Acme Aerospace",
                Description = "Defense & Aerospace Engineering Systems",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                IsActive = true
            },
            new Tenant
            {
                Id = tenantGlobexId,
                Name = "Globex Health",
                Description = "Pharmaceutical & Clinical Research Systems",
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                IsActive = true
            }
        );

        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                TenantId = tenantAcmeId,
                Email = "alice.lead@acme.local",
                FullName = "Alice Vance (Lead Propulsion Engineer)",
                Roles = new List<string> { "Engineering", "General" },
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new User
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                TenantId = tenantAcmeId,
                Email = "bob.hr@acme.local",
                FullName = "Bob Davis (HR Director)",
                Roles = new List<string> { "HR", "General" },
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            new User
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                TenantId = tenantGlobexId,
                Email = "dr.carol@globex.local",
                FullName = "Dr. Carol Danvers (Clinical Director)",
                Roles = new List<string> { "Executive", "General" },
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            }
        );
    }
}
