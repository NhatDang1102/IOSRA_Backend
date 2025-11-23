using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Repository.Entities;

namespace Repository.DBContext;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ContentMod> ContentMods { get; set; }

    public virtual DbSet<OperationMod> OperationMods { get; set; }

    public virtual DbSet<account> accounts { get; set; }

    public virtual DbSet<account_role> account_roles { get; set; }

    public virtual DbSet<admin> admins { get; set; }

    public virtual DbSet<author> authors { get; set; }

    public virtual DbSet<author_rank> author_ranks { get; set; }

    public virtual DbSet<chapter> chapters { get; set; }

    public virtual DbSet<chapter_comment> chapter_comments { get; set; }

    public virtual DbSet<chapter_comment_reaction> chapter_comment_reactions { get; set; }

    public virtual DbSet<chapter_localization> chapter_localizations { get; set; }

    public virtual DbSet<chapter_purchase_log> chapter_purchase_logs { get; set; }

    public virtual DbSet<chapter_voice> chapter_voices { get; set; }

    public virtual DbSet<author_rank_upgrade_request> author_rank_upgrade_requests { get; set; }

    public virtual DbSet<content_approve> content_approves { get; set; }

    public virtual DbSet<dia_payment> dia_payments { get; set; }

    public virtual DbSet<dia_wallet> dia_wallets { get; set; }

    public virtual DbSet<topup_pricing> topup_pricings { get; set; }

    public virtual DbSet<favorite_story> favorite_stories { get; set; }

    public virtual DbSet<follow> follows { get; set; }
    public virtual DbSet<notification> notifications { get; set; }
    public virtual DbSet<chapter_price_rule> chapter_price_rules { get; set; }

    public virtual DbSet<language_list> language_lists { get; set; }

    public virtual DbSet<op_request> op_requests { get; set; }

    public virtual DbSet<reader> readers { get; set; }

    public virtual DbSet<report> reports { get; set; }

    public virtual DbSet<role> roles { get; set; }

    public virtual DbSet<story> stories { get; set; }

    public virtual DbSet<story_tag> story_tags { get; set; }

    public virtual DbSet<story_rating> story_ratings { get; set; }

    public virtual DbSet<story_weekly_view> story_weekly_views { get; set; }

    public virtual DbSet<subcription> subcriptions { get; set; }

    public virtual DbSet<subscription_plan> subscription_plans { get; set; }

    public virtual DbSet<subscription_payment> subscription_payments { get; set; }

    public virtual DbSet<tag> tags { get; set; }

    public virtual DbSet<voice_list> voice_lists { get; set; }

    public virtual DbSet<wallet_payment> wallet_payments { get; set; }

    public virtual DbSet<voice_wallet> voice_wallets { get; set; }

    public virtual DbSet<voice_payment> voice_payments { get; set; }

    public virtual DbSet<voice_topup_pricing> voice_topup_pricings { get; set; }

    public virtual DbSet<voice_wallet_payment> voice_wallet_payments { get; set; }

    public virtual DbSet<payment_receipt> payment_receipts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");


        modelBuilder.Entity<ContentMod>(entity =>
        {
            entity.HasKey(e => e.account_id).HasName("PRIMARY");

            entity.Property(e => e.account_id).ValueGeneratedNever();
            entity.Property(e => e.assigned_date).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithOne(p => p.ContentMod).HasConstraintName("fk_cmod_account");
        });

        modelBuilder.Entity<OperationMod>(entity =>
        {
            entity.HasKey(e => e.account_id).HasName("PRIMARY");

            entity.Property(e => e.account_id).ValueGeneratedNever();
            entity.Property(e => e.assigned_date).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithOne(p => p.OperationMod).HasConstraintName("fk_omod_account");
        });

        modelBuilder.Entity<account>(entity =>
        {
            entity.HasKey(e => e.account_id).HasName("PRIMARY");

            entity.Property(e => e.account_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'unbanned'");
            entity.Property(e => e.strike_status).HasDefaultValueSql("'none'");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<account_role>(entity =>
        {
            entity.HasKey(e => new { e.account_id, e.role_id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithMany(p => p.account_roles).HasConstraintName("fk_account_roles_account");

            entity.HasOne(d => d.role).WithMany(p => p.account_roles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_account_roles_role");
        });

        modelBuilder.Entity<admin>(entity =>
        {
            entity.HasKey(e => e.account_id).HasName("PRIMARY");

            entity.Property(e => e.account_id).ValueGeneratedNever();

            entity.HasOne(d => d.account).WithOne(p => p.admin).HasConstraintName("fk_admin_account");
        });

        modelBuilder.Entity<author>(entity =>
        {
            entity.HasKey(e => e.account_id).HasName("PRIMARY");

            entity.Property(e => e.account_id).ValueGeneratedNever();

            entity.HasOne(d => d.account).WithOne(p => p.author).HasConstraintName("fk_author_account");

            entity.HasOne(d => d.rank).WithMany(p => p.authors)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_author_rank");
        });

        modelBuilder.Entity<author_rank>(entity =>
        {
            entity.HasKey(e => e.rank_id).HasName("PRIMARY");
        });

        modelBuilder.Entity<author_rank_upgrade_request>(entity =>
        {
            entity.HasKey(e => e.request_id).HasName("PRIMARY");

            entity.Property(e => e.request_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.author).WithMany(p => p.rank_upgrade_requests)
                .HasForeignKey(d => d.author_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_rank_upgrade_author");

            entity.HasOne(d => d.target_rank).WithMany()
                .HasForeignKey(d => d.target_rank_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_rank_upgrade_target");

            entity.HasOne(d => d.moderator).WithMany()
                .HasForeignKey(d => d.omod_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_rank_upgrade_omod");
        });

        modelBuilder.Entity<chapter>(entity =>
        {
            entity.HasKey(e => e.chapter_id).HasName("PRIMARY");

            entity.Property(e => e.chapter_id).ValueGeneratedNever(); 
            entity.Property(e => e.access_type).HasDefaultValueSql("'free'");
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'draft'");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.story).WithMany(p => p.chapters).HasConstraintName("fk_chapter_story");
        });

        modelBuilder.Entity<chapter_comment>(entity =>
        {
            entity.HasKey(e => e.comment_id).HasName("PRIMARY");

            entity.Property(e => e.comment_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'visible'");
            entity.Property(e => e.is_locked).HasDefaultValue(false);
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.story_id).HasDatabaseName("ix_cmt_story");

            entity.HasOne(d => d.chapter).WithMany(p => p.chapter_comments).HasConstraintName("fk_cmt_chapter");
            entity.HasOne(d => d.story).WithMany(p => p.chapter_comments).HasConstraintName("fk_cmt_story");
            entity.HasOne(d => d.reader).WithMany(p => p.chapter_comments).HasConstraintName("fk_cmt_reader");
        });

        modelBuilder.Entity<chapter_comment_reaction>(entity =>
        {
            entity.HasKey(e => e.reaction_id).HasName("PRIMARY");

            entity.Property(e => e.reaction_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.comment).WithMany(p => p.chapter_comment_reactions).HasConstraintName("fk_ccr_comment");
            entity.HasOne(d => d.reader).WithMany(p => p.chapter_comment_reactions).HasConstraintName("fk_ccr_reader");
        });

        modelBuilder.Entity<story_rating>(entity =>
        {
            entity.HasKey(e => new { e.story_id, e.reader_id }).HasName("PRIMARY");

            entity.Property(e => e.score).HasColumnType("tinyint");
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasIndex(e => e.reader_id).HasDatabaseName("ix_rating_reader");

            entity.HasOne(d => d.reader).WithMany(p => p.story_ratings).HasConstraintName("fk_rating_reader");
            entity.HasOne(d => d.story).WithMany(p => p.story_ratings).HasConstraintName("fk_rating_story");
        });

        modelBuilder.Entity<chapter_localization>(entity =>
        {
            entity.HasKey(e => new { e.chapter_id, e.lang_id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.HasOne(d => d.chapter).WithMany(p => p.chapter_localizations).HasConstraintName("fk_chloc_chapter");

            entity.HasOne(d => d.lang).WithMany(p => p.chapter_localizations)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_chloc_lang");
        });

        modelBuilder.Entity<chapter_purchase_log>(entity =>
        {
            entity.HasKey(e => e.chapter_purchase_id).HasName("PRIMARY");

            entity.Property(e => e.chapter_purchase_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithMany(p => p.chapter_purchase_logs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cpl_account");

            entity.HasOne(d => d.chapter).WithMany(p => p.chapter_purchase_logs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cpl_chapter");
        });

        modelBuilder.Entity<chapter_voice>(entity =>
        {
            entity.HasKey(e => new { e.chapter_id, e.voice_id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.cloud_url).HasMaxLength(512);
            entity.Property(e => e.storage_path).HasMaxLength(512);
            entity.Property(e => e.status)
                .HasDefaultValueSql("'pending'")
                .HasColumnType("enum('pending','processing','ready','failed')");
            entity.Property(e => e.requested_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.char_cost).HasDefaultValue(0);
            entity.Property(e => e.error_message).HasColumnType("text");

            entity.HasOne(d => d.chapter).WithMany(p => p.chapter_voices).HasConstraintName("fk_chvoice_chapter");

            entity.HasOne(d => d.voice).WithMany(p => p.chapter_voices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_chvoice_voice");
        });

        modelBuilder.Entity<content_approve>(entity =>
        {
            entity.HasKey(e => e.review_id).HasName("PRIMARY");

            entity.Property(e => e.review_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'pending'");
            entity.Property(e => e.moderator_feedback).HasColumnType("text");

            entity.HasOne(d => d.chapter).WithMany(p => p.content_approves)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_cappr_chapter");

            entity.HasOne(d => d.moderator).WithMany(p => p.content_approves)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_cappr_moderator");

            entity.HasOne(d => d.story).WithMany(p => p.content_approves)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_cappr_story");
        });

        modelBuilder.Entity<dia_payment>(entity =>
        {
            entity.HasKey(e => e.topup_id).HasName("PRIMARY");

            entity.Property(e => e.topup_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'pending'");

            entity.HasOne(d => d.wallet).WithMany(p => p.dia_payments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_topup_wallet");
        });

        modelBuilder.Entity<dia_wallet>(entity =>
        {
            entity.HasKey(e => e.wallet_id).HasName("PRIMARY");

            entity.Property(e => e.wallet_id).ValueGeneratedNever(); 
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithOne(p => p.dia_wallet).HasConstraintName("fk_wallet_account");
        });

        modelBuilder.Entity<voice_wallet>(entity =>
        {
            entity.HasKey(e => e.wallet_id).HasName("PRIMARY");

            entity.Property(e => e.wallet_id).ValueGeneratedNever();
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithOne(p => p.voice_wallet).HasConstraintName("fk_voice_wallet_account");
        });

        modelBuilder.Entity<voice_payment>(entity =>
        {
            entity.HasKey(e => e.topup_id).HasName("PRIMARY");

            entity.Property(e => e.topup_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'pending'");

            entity.HasOne(d => d.voice_wallet).WithMany(p => p.voice_payments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_voice_payment_wallet");
        });

        modelBuilder.Entity<voice_wallet_payment>(entity =>
        {
            entity.HasKey(e => e.trs_id).HasName("PRIMARY");

            entity.Property(e => e.trs_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.note).HasMaxLength(255);
            entity.Property(e => e.type).HasColumnType("enum('topup','purchase','refund')");

            entity.HasOne(d => d.voice_wallet).WithMany(p => p.voice_wallet_payments)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_voice_wallet_payment_wallet");
        });

        modelBuilder.Entity<favorite_story>(entity =>
        {
            entity.HasKey(e => new { e.reader_id, e.story_id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.noti_new_chapter).HasDefaultValueSql("'1'");

            entity.HasOne(d => d.reader).WithMany(p => p.favorite_stories).HasConstraintName("fk_fav_reader");

            entity.HasOne(d => d.story).WithMany(p => p.favorite_stories).HasConstraintName("fk_fav_story");
        });

        modelBuilder.Entity<follow>(entity =>
        {
            entity.HasKey(e => new { e.follower_id, e.followee_id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.noti_new_story).HasDefaultValueSql("'1'");

            entity.HasOne(d => d.followee).WithMany(p => p.follows).HasConstraintName("fk_follow_followee");

            entity.HasOne(d => d.follower).WithMany(p => p.follows).HasConstraintName("fk_follow_follower");
        });

        modelBuilder.Entity<notification>(entity =>
        {
            entity.HasKey(e => e.notification_id).HasName("PRIMARY");
            entity.HasIndex(e => new { e.recipient_id, e.created_at }, "ix_notifications_recipient_created");

            entity.Property(e => e.notification_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.type).HasDefaultValueSql("'general'");

            entity.HasOne(d => d.recipient)
                .WithMany()
                .HasForeignKey(d => d.recipient_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_notifications_recipient");
        });

        modelBuilder.Entity<chapter_price_rule>(entity =>
        {
            entity.HasKey(e => e.rule_id).HasName("PRIMARY");
            entity.Property(e => e.rule_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<language_list>(entity =>
        {
            entity.HasKey(e => e.lang_id).HasName("PRIMARY");
        });

        modelBuilder.Entity<op_request>(entity =>
        {
            entity.HasKey(e => e.request_id).HasName("PRIMARY");

            entity.Property(e => e.request_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'pending'");

            // requester -> account (DUY NH?T quan h? này; không quan h? nào khác t?i account)
            entity.HasOne(d => d.requester)
                .WithMany(p => p.op_requests_as_requester)   // <-- tr? dúng collection
                .HasForeignKey(d => d.requester_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_opreq_requester");

            // omod (nullable)
            entity.HasOne(d => d.omod)
                .WithMany(p => p.op_requests)
                .HasForeignKey(d => d.omod_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_opreq_omod");
        });
        modelBuilder.Entity<reader>(entity =>
        {
            entity.HasKey(e => e.account_id).HasName("PRIMARY");

            entity.Property(e => e.account_id).ValueGeneratedNever();
            entity.Property(e => e.gender).HasDefaultValueSql("'unspecified'");

            entity.HasOne(d => d.account).WithOne(p => p.reader).HasConstraintName("fk_reader_account");
        });

        modelBuilder.Entity<report>(entity =>
        {
            entity.HasKey(e => e.report_id).HasName("PRIMARY");

            entity.Property(e => e.report_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'pending'");

            entity.HasOne(d => d.moderator).WithMany(p => p.reports)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_reports_moderator");

            entity.HasOne(d => d.reporter).WithMany(p => p.reports).HasConstraintName("fk_reports_reporter");
        });

        modelBuilder.Entity<role>(entity =>
        {
            entity.HasKey(e => e.role_id).HasName("PRIMARY");
        });

        modelBuilder.Entity<story>(entity =>
        {
            entity.HasKey(e => e.story_id).HasName("PRIMARY");

            entity.Property(e => e.story_id).ValueGeneratedNever(); 
            entity.HasIndex(e => new { e.title, e.desc }, "ft_story_title_desc").HasAnnotation("MySql:FullTextIndex", true);

            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'draft'");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.published_at);

            entity.HasOne(d => d.author).WithMany(p => p.stories)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_story_author");
        });

        modelBuilder.Entity<story_tag>(entity =>
        {
            entity.HasKey(e => new { e.story_id, e.tag_id })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.story).WithMany(p => p.story_tags).HasConstraintName("fk_story_tags_story");

            entity.HasOne(d => d.tag).WithMany(p => p.story_tags)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_story_tags_tag");
        });

        modelBuilder.Entity<story_weekly_view>(entity =>
        {
            entity.HasKey(e => e.story_weekly_view_id).HasName("PRIMARY");

            entity.Property(e => e.story_weekly_view_id).ValueGeneratedNever();
            entity.Property(e => e.view_count).HasDefaultValueSql("0");
            entity.Property(e => e.week_start_utc).HasColumnType("datetime");
            entity.Property(e => e.captured_at_utc).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.story).WithMany(p => p.story_weekly_views)
                .HasForeignKey(d => d.story_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_story_week_story");
        });

        modelBuilder.Entity<subcription>(entity =>
        {
            entity.HasKey(e => e.sub_id).HasName("PRIMARY");

            entity.Property(e => e.sub_id).ValueGeneratedNever(); 
            entity.HasOne(d => d.plan_codeNavigation).WithMany(p => p.subcriptions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_sub_plan");

            entity.HasOne(d => d.user).WithMany(p => p.subcriptions).HasConstraintName("fk_sub_user");
        });

        modelBuilder.Entity<subscription_plan>(entity =>
        {
            entity.HasKey(e => e.plan_code).HasName("PRIMARY");
            entity.Property(e => e.plan_code).ValueGeneratedNever();
            entity.Property(e => e.plan_name).HasMaxLength(64);
            entity.Property(e => e.price_vnd).HasColumnType("bigint unsigned");
            entity.Property(e => e.daily_dias).HasDefaultValue(0);
        });

        modelBuilder.Entity<subscription_payment>(entity =>
        {
            entity.HasKey(e => e.payment_id).HasName("PRIMARY");

            entity.Property(e => e.payment_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.status).HasDefaultValueSql("'pending'");

            entity.HasOne(d => d.account).WithMany(p => p.subscription_payments)
                .HasForeignKey(d => d.account_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_subpay_account");

            entity.HasOne(d => d.plan).WithMany(p => p.subscription_payments)
                .HasForeignKey(d => d.plan_code)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_subpay_plan");
        });
        modelBuilder.Entity<tag>(entity =>
        {
            entity.HasKey(e => e.tag_id).HasName("PRIMARY");
        });

        modelBuilder.Entity<voice_list>(entity =>
        {
            entity.HasKey(e => e.voice_id).HasName("PRIMARY");

            entity.Property(e => e.voice_id).ValueGeneratedNever();
            entity.Property(e => e.voice_name).HasMaxLength(64);
            entity.Property(e => e.voice_code).HasMaxLength(32);
            entity.Property(e => e.provider_voice_id).HasMaxLength(128);
            entity.Property(e => e.description).HasMaxLength(256);
            entity.Property(e => e.is_active).HasDefaultValueSql("1");
        });

        modelBuilder.Entity<voice_topup_pricing>(entity =>
        {
            entity.HasKey(e => e.pricing_id).HasName("PRIMARY");

            entity.Property(e => e.pricing_id).ValueGeneratedNever();
            entity.Property(e => e.updated_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<payment_receipt>(entity =>
        {
            entity.HasKey(e => e.receipt_id).HasName("PRIMARY");

            entity.Property(e => e.receipt_id).ValueGeneratedNever();
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.account).WithMany(p => p.payment_receipts).HasConstraintName("fk_receipt_account");
        });

        modelBuilder.Entity<wallet_payment>(entity =>
        {
            entity.HasKey(e => e.trs_id).HasName("PRIMARY");

            entity.Property(e => e.trs_id).ValueGeneratedNever(); 
            entity.Property(e => e.created_at).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasOne(d => d.wallet).WithMany(p => p.wallet_payments).HasConstraintName("fk_wpay_wallet");
        });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}







