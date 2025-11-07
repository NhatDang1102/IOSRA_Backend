-- MySQL 8.0+ schema for IOSRA (GUID-based IDs)
SET NAMES utf8mb4;
SET time_zone = "+00:00";

CREATE DATABASE IF NOT EXISTS IOSRA_DB
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;
USE IOSRA_DB;

-- ===================== Core accounts & roles =====================
CREATE TABLE account (
  account_id      CHAR(36) NOT NULL,
  username        VARCHAR(50) NOT NULL,
  email           VARCHAR(255) NOT NULL,
  password_hash   VARCHAR(255) NOT NULL,
  status          ENUM('unbanned','banned') NOT NULL DEFAULT 'unbanned',
  strike          TINYINT UNSIGNED NOT NULL DEFAULT 0,
  avatar_url      VARCHAR(512) NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (account_id),
  UNIQUE KEY ux_account_username (username),
  UNIQUE KEY ux_account_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE roles (
  role_id     CHAR(36) NOT NULL,
  role_code   VARCHAR(32) NOT NULL,
  role_name   VARCHAR(64) NOT NULL,
  PRIMARY KEY (role_id),
  UNIQUE KEY ux_roles_code (role_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE account_roles (
  account_id  CHAR(36) NOT NULL,
  role_id     CHAR(36) NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (account_id, role_id),
  KEY fk_account_roles_role (role_id),
  CONSTRAINT fk_account_roles_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_account_roles_role FOREIGN KEY (role_id)
    REFERENCES roles(role_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ===================== Personas =====================
CREATE TABLE author_rank (
  rank_id       CHAR(36) NOT NULL,
  rank_name     VARCHAR(50) NOT NULL,
  reward_rate   DECIMAL(5,2) NOT NULL,
  min_followers INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (rank_id),
  UNIQUE KEY ux_author_rank_name (rank_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE author (
  account_id        CHAR(36) NOT NULL,
  restricted        TINYINT(1) NOT NULL DEFAULT 0,
  rank_id           CHAR(36) NULL,
  verified_status   TINYINT(1) NOT NULL DEFAULT 0,
  total_story       INT UNSIGNED NOT NULL DEFAULT 0,
  total_follower    INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id),
  KEY fk_author_rank (rank_id),
  CONSTRAINT fk_author_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_author_rank FOREIGN KEY (rank_id)
    REFERENCES author_rank(rank_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE reader (
  account_id   CHAR(36) NOT NULL,
  bio          TEXT NULL,
  gender       ENUM('male','female','other','unspecified') NOT NULL DEFAULT 'unspecified',
  birthdate    DATE NULL,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_reader_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Staff tables
CREATE TABLE admin (
  account_id   CHAR(36) NOT NULL,
  department   VARCHAR(100) NULL,
  notes        TEXT NULL,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_admin_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE ContentMod (
  account_id             CHAR(36) NOT NULL,
  assigned_date          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  phone                  VARCHAR(32) NULL,
  total_approved_stories INT UNSIGNED NOT NULL DEFAULT 0,
  total_rejected_stories INT UNSIGNED NOT NULL DEFAULT 0,
  total_reported_handled INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_cmod_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE OperationMod (
  account_id        CHAR(36) NOT NULL,
  assigned_date     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reports_generated INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_omod_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ===================== Catalog & content =====================
CREATE TABLE story (
  story_id     CHAR(36) NOT NULL,
  title        VARCHAR(255) NOT NULL,
  author_id    CHAR(36) NOT NULL,
  `desc`       MEDIUMTEXT NULL,
  outline      TEXT NOT NULL,
  length_plan  ENUM('novel','short','super_short') NOT NULL DEFAULT 'short',
  cover_url    VARCHAR(512) NULL,
  status       ENUM('draft','pending','rejected','published','completed','hidden','removed') NOT NULL DEFAULT 'draft',
  is_premium   TINYINT(1) NOT NULL DEFAULT 0,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  published_at DATETIME NULL,
  PRIMARY KEY (story_id),
  KEY ix_story_author (author_id),
  FULLTEXT KEY ft_story_title_desc (title, `desc`),
  CONSTRAINT fk_story_author FOREIGN KEY (author_id)
    REFERENCES author(account_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE tags (
  tag_id    CHAR(36) NOT NULL,
  tag_name  VARCHAR(64) NOT NULL,
  PRIMARY KEY (tag_id),
  UNIQUE KEY ux_tag_name (tag_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE language_list (
  lang_id   CHAR(36) NOT NULL,
  lang_code VARCHAR(8) NOT NULL,
  lang_name VARCHAR(64) NOT NULL,
  PRIMARY KEY (lang_id),
  UNIQUE KEY ux_language_code (lang_code),
  UNIQUE KEY ux_lang_name (lang_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE voice_list (
  voice_id   CHAR(36) NOT NULL,
  voice_name VARCHAR(64) NOT NULL,
  PRIMARY KEY (voice_id),
  UNIQUE KEY ux_voice_name (voice_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapters (
  chapter_id   CHAR(36) NOT NULL,
  story_id     CHAR(36) NOT NULL,
  chapter_no   INT UNSIGNED NOT NULL,
  language_id  CHAR(36) NOT NULL,
  title        VARCHAR(255) NOT NULL,
  summary      TEXT NULL,
  dias_price   INT UNSIGNED NOT NULL DEFAULT 0,
  access_type  ENUM('free','coin','sub_only') NOT NULL DEFAULT 'free',
  content_url  VARCHAR(512) NULL,
  word_count   INT UNSIGNED NOT NULL DEFAULT 0,
  ai_score     DECIMAL(5,2) NULL,
  ai_feedback  TEXT NULL,
  status       ENUM('draft','pending','rejected','published','hidden','removed') NOT NULL DEFAULT 'draft',
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  submitted_at DATETIME NULL,
  published_at DATETIME NULL,
  PRIMARY KEY (chapter_id),
  UNIQUE KEY ux_chapter_story_no (story_id, chapter_no),
  KEY ix_chapter_story (story_id),
  KEY fk_chapter_language (language_id),
  CONSTRAINT fk_chapter_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_chapter_language FOREIGN KEY (language_id)
    REFERENCES language_list(lang_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE story_tags (
  story_id   CHAR(36) NOT NULL,
  tag_id     CHAR(36) NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (story_id, tag_id),
  KEY ix_story_tags_tag (tag_id),
  CONSTRAINT fk_story_tags_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_story_tags_tag FOREIGN KEY (tag_id)
    REFERENCES tags(tag_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE content_approve (
  review_id     CHAR(36) NOT NULL,
  approve_type  ENUM('story','chapter') NOT NULL,
  story_id      CHAR(36) NULL,
  chapter_id    CHAR(36) NULL,
  ai_score      DECIMAL(5,2) NULL,
  ai_note       TEXT NULL,
  status        ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  moderator_id  CHAR(36) NULL,
  moderator_note TEXT NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (review_id),
  KEY ix_cappr_story (story_id),
  KEY ix_cappr_chapter (chapter_id),
  KEY fk_cappr_moderator (moderator_id),
  CONSTRAINT fk_cappr_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cappr_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cappr_moderator FOREIGN KEY (moderator_id)
    REFERENCES ContentMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_comment (
  comment_id CHAR(36) NOT NULL,
  reader_id  CHAR(36) NOT NULL,
  chapter_id CHAR(36) NOT NULL,
  content    TEXT NOT NULL,
  status     ENUM('visible','hidden','removed') NOT NULL DEFAULT 'visible',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (comment_id),
  KEY ix_cmt_reader (reader_id),
  KEY ix_cmt_chapter (chapter_id),
  CONSTRAINT fk_cmt_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cmt_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_localizations (
  chapter_id CHAR(36) NOT NULL,
  lang_id    CHAR(36) NOT NULL,
  content    LONGTEXT NOT NULL,
  word_count INT UNSIGNED NOT NULL DEFAULT 0,
  cloud_url  VARCHAR(512) NULL,
  PRIMARY KEY (chapter_id, lang_id),
  KEY fk_chloc_lang (lang_id),
  CONSTRAINT fk_chloc_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_chloc_lang FOREIGN KEY (lang_id)
    REFERENCES language_list(lang_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_voices (
  chapter_id CHAR(36) NOT NULL,
  voice_id   CHAR(36) NOT NULL,
  cloud_url  VARCHAR(512) NULL,
  PRIMARY KEY (chapter_id, voice_id),
  KEY fk_chvoice_voice (voice_id),
  CONSTRAINT fk_chvoice_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_chvoice_voice FOREIGN KEY (voice_id)
    REFERENCES voice_list(voice_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_purchase_log (
  chapter_purchase_id CHAR(36) NOT NULL,
  chapter_id          CHAR(36) NOT NULL,
  account_id          CHAR(36) NOT NULL,
  dia_price           INT UNSIGNED NOT NULL,
  created_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (chapter_purchase_id),
  UNIQUE KEY ux_purchase_unique (chapter_id, account_id),
  KEY ix_purchase_account (account_id),
  CONSTRAINT fk_cpl_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cpl_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ===================== Social =====================
CREATE TABLE follow (
  follower_id    CHAR(36) NOT NULL,
  followee_id    CHAR(36) NOT NULL,
  noti_new_story TINYINT(1) NOT NULL DEFAULT 1,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (follower_id, followee_id),
  KEY ix_follow_followee (followee_id),
  CONSTRAINT fk_follow_follower FOREIGN KEY (follower_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_follow_followee FOREIGN KEY (followee_id)
    REFERENCES author(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE favorite_story (
  reader_id        CHAR(36) NOT NULL,
  story_id         CHAR(36) NOT NULL,
  noti_new_chapter TINYINT(1) NOT NULL DEFAULT 1,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (reader_id, story_id),
  KEY ix_fav_story (story_id),
  CONSTRAINT fk_fav_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_fav_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE story_weekly_views (
  story_weekly_view_id CHAR(36) NOT NULL,
  story_id             CHAR(36) NOT NULL,
  week_start_utc       DATETIME NOT NULL,
  view_count           BIGINT UNSIGNED NOT NULL DEFAULT 0,
  captured_at_utc      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (story_weekly_view_id),
  UNIQUE KEY ux_story_week (story_id, week_start_utc),
  KEY fk_story_week_story (story_id),
  CONSTRAINT fk_story_week_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ===================== Economy =====================
CREATE TABLE dia_wallet (
  wallet_id   CHAR(36) NOT NULL,
  account_id  CHAR(36) NOT NULL,
  balance_coin BIGINT NOT NULL DEFAULT 0,
  locked_coin  BIGINT NOT NULL DEFAULT 0,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (wallet_id),
  UNIQUE KEY ux_wallet_account (account_id),
  CONSTRAINT fk_wallet_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE dia_payment (
  topup_id        CHAR(36) NOT NULL,
  wallet_id       CHAR(36) NOT NULL,
  provider        VARCHAR(50) NOT NULL,
  amount_vnd      BIGINT UNSIGNED NOT NULL,
  diamond_granted BIGINT UNSIGNED NOT NULL,
  status          ENUM('pending','success','failed','refunded') NOT NULL DEFAULT 'pending',
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (topup_id),
  KEY ix_topup_wallet (wallet_id),
  CONSTRAINT fk_topup_wallet FOREIGN KEY (wallet_id)
    REFERENCES dia_wallet(wallet_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE wallet_payment (
  trs_id     CHAR(36) NOT NULL,
  wallet_id  CHAR(36) NOT NULL,
  type       ENUM('purchase','withdraw','topup','adjust') NOT NULL,
  coin_delta BIGINT NOT NULL,
  coin_after BIGINT NOT NULL,
  ref_id     CHAR(36) NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (trs_id),
  KEY ix_wpay_wallet (wallet_id),
  KEY ix_wpay_type (type),
  CONSTRAINT fk_wpay_wallet FOREIGN KEY (wallet_id)
    REFERENCES dia_wallet(wallet_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ===================== Requests & moderation =====================
CREATE TABLE op_requests (
  request_id       CHAR(36) NOT NULL,
  requester_id     CHAR(36) NOT NULL,
  request_type     ENUM('withdraw','rank_up','become_author') NOT NULL DEFAULT 'withdraw',
  request_content  TEXT NULL,
  withdraw_amount  BIGINT UNSIGNED NULL,
  omod_id          CHAR(36) NULL,
  status           ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  withdraw_code    VARCHAR(64) NULL,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (request_id),
  KEY ix_opreq_requester (requester_id),
  KEY ix_opreq_omod (omod_id),
  CONSTRAINT fk_opreq_requester FOREIGN KEY (requester_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_opreq_omod FOREIGN KEY (omod_id)
    REFERENCES OperationMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE reports (
  report_id    CHAR(36) NOT NULL,
  target_type  ENUM('story','chapter','comment','user') NOT NULL,
  target_id    CHAR(36) NOT NULL,
  reporter_id  CHAR(36) NOT NULL,
  reason       VARCHAR(255) NOT NULL,
  details      TEXT NULL,
  status       ENUM('open','in_review','resolved','rejected') NOT NULL DEFAULT 'open',
  moderator_id CHAR(36) NULL,
  reviewed_at  DATETIME NULL,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (report_id),
  KEY ix_reports_reporter (reporter_id),
  KEY ix_reports_moderator (moderator_id),
  KEY ix_reports_target (target_type, target_id),
  CONSTRAINT fk_reports_reporter FOREIGN KEY (reporter_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_reports_moderator FOREIGN KEY (moderator_id)
    REFERENCES ContentMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- ===================== Subscriptions =====================
CREATE TABLE subscription_plan (
  plan_code        VARCHAR(32) NOT NULL,
  plan_name        VARCHAR(64) NOT NULL,
  price_coin       INT UNSIGNED NOT NULL,
  daily_claim_limit INT UNSIGNED NOT NULL,
  duration_days    INT UNSIGNED NOT NULL,
  PRIMARY KEY (plan_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE subcriptions (
  sub_id        CHAR(36) NOT NULL,
  user_id       CHAR(36) NOT NULL,
  plan_code     VARCHAR(32) NOT NULL,
  start_at      DATETIME NOT NULL,
  end_at        DATETIME NOT NULL,
  last_claim_date DATE NULL,
  claimed_today TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (sub_id),
  KEY ix_sub_user (user_id),
  KEY ix_sub_plan (plan_code),
  CONSTRAINT fk_sub_user FOREIGN KEY (user_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_sub_plan FOREIGN KEY (plan_code)
    REFERENCES subscription_plan(plan_code) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

