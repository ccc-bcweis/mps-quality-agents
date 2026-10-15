// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using Microsoft.EntityFrameworkCore;

namespace Mps.Api.Persistence;

public sealed class QualityDbContext : DbContext
{
    public QualityDbContext(DbContextOptions<QualityDbContext> options)
        : base(options)
    {
    }

    public DbSet<ToolRow> Tools => Set<ToolRow>();

    public DbSet<ChamberRow> Chambers => Set<ChamberRow>();

    public DbSet<AlertRow> Alerts => Set<AlertRow>();

    public DbSet<IncidentRow> Incidents => Set<IncidentRow>();

    public DbSet<TicketRow> Tickets => Set<TicketRow>();

    public DbSet<TicketSequenceRow> TicketSequences => Set<TicketSequenceRow>();

    public DbSet<InvestigationRow> Investigations => Set<InvestigationRow>();

    public DbSet<CapaRow> Capas => Set<CapaRow>();

    public DbSet<FaultRow> Faults => Set<FaultRow>();

    public DbSet<ApprovalRow> Approvals => Set<ApprovalRow>();

    public DbSet<MetricSampleRow> MetricSamples => Set<MetricSampleRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<ToolRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasMany(row => row.Chambers).WithOne(row => row.Tool!).HasForeignKey(row => row.ToolId);
        });
        modelBuilder.Entity<ChamberRow>(entity =>
        {
            entity.HasKey(row => new { row.ToolId, row.ChamberId });
        });
        modelBuilder.Entity<AlertRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new { row.ToolId, row.Parameter, row.RaisedAt });
        });
        modelBuilder.Entity<IncidentRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => new { row.ToolId, row.ParameterKey, row.Stage });
            entity.HasIndex(row => new { row.ToolId, row.ParameterKey })
                .IsUnique()
                .HasFilter("Stage <> 'Closed' AND Stage <> 'ClosedByDemoReset'");
        });
        modelBuilder.Entity<TicketRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.Number).IsUnique();
        });
        modelBuilder.Entity<TicketSequenceRow>(entity => entity.HasKey(row => row.Id));
        modelBuilder.Entity<InvestigationRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.IncidentId);
        });
        modelBuilder.Entity<CapaRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.IncidentId);
        });
        modelBuilder.Entity<FaultRow>(entity => entity.HasKey(row => row.Id));
        modelBuilder.Entity<ApprovalRow>(entity =>
        {
            entity.HasKey(row => row.TokenId);
            entity.HasIndex(row => row.InvestigationId);
        });
        modelBuilder.Entity<MetricSampleRow>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.HasIndex(row => row.InvestigationId);
        });
    }
}
