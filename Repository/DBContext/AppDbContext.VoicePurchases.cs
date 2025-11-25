using Microsoft.EntityFrameworkCore;
using Repository.Entities;

namespace Repository.DBContext
{
    public partial class AppDbContext
    {
        public virtual DbSet<voice_price_rule> voice_price_rules { get; set; } = null!;
        public virtual DbSet<voice_purchase_log> voice_purchase_logs { get; set; } = null!;
        public virtual DbSet<voice_purchase_item> voice_purchase_items { get; set; } = null!;

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<voice_price_rule>(entity =>
            {
                entity.HasKey(e => e.rule_id).HasName("PRIMARY");
                entity.Property(e => e.rule_id).ValueGeneratedNever();
                entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            modelBuilder.Entity<voice_purchase_log>(entity =>
            {
                entity.HasKey(e => e.voice_purchase_id).HasName("PRIMARY");
                entity.Property(e => e.voice_purchase_id).ValueGeneratedNever();
                entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(d => d.account)
                    .WithMany(p => p.voice_purchase_logs)
                    .HasForeignKey(d => d.account_id)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_voice_purchase_account");

                entity.HasOne(d => d.chapter)
                    .WithMany(p => p.voice_purchase_logs)
                    .HasForeignKey(d => d.chapter_id)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_voice_purchase_chapter");
            });

            modelBuilder.Entity<voice_purchase_item>(entity =>
            {
                entity.HasKey(e => e.purchase_item_id).HasName("PRIMARY");
                entity.Property(e => e.purchase_item_id).ValueGeneratedNever();
                entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(d => d.account)
                    .WithMany(p => p.voice_purchase_items)
                    .HasForeignKey(d => d.account_id)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_voice_purchase_item_account");

                entity.HasOne(d => d.chapter)
                    .WithMany(p => p.voice_purchase_items)
                    .HasForeignKey(d => d.chapter_id)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_voice_purchase_item_chapter");

                entity.HasOne(d => d.purchase)
                    .WithMany(p => p.voice_purchase_items)
                    .HasForeignKey(d => d.voice_purchase_id)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_voice_purchase_item_purchase");

                entity.HasOne(d => d.voice)
                    .WithMany(p => p.voice_purchase_items)
                    .HasForeignKey(d => d.voice_id)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("fk_voice_purchase_item_voice");
            });

            OnModelCreatingVoicePurchasesPartial(modelBuilder);
        }

        partial void OnModelCreatingVoicePurchasesPartial(ModelBuilder modelBuilder);
    }
}
