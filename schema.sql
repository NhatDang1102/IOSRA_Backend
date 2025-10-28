-- MySQL 8.0+ schema for IOSRA, single-DB
SET NAMES utf8mb4;
SET time_zone = '+00:00';

CREATE DATABASE IF NOT EXISTS IOSRA_DB
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;
USE IOSRA_DB;

-- ===================== Core accounts & roles =====================
CREATE TABLE account (
  account_id      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
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
) ENGINE=InnoDB;

CREATE TABLE roles (
  role_id     SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
  role_code   VARCHAR(32) NOT NULL,
  role_name   VARCHAR(64) NOT NULL,
  PRIMARY KEY (role_id),
  UNIQUE KEY ux_roles_code (role_code)
) ENGINE=InnoDB;

CREATE TABLE account_roles (
  account_id  BIGINT UNSIGNED NOT NULL,
  role_id     SMALLINT UNSIGNED NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (account_id, role_id),
  CONSTRAINT fk_account_roles_account
    FOREIGN KEY (account_id) REFERENCES account(account_id)
    ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_account_roles_role
    FOREIGN KEY (role_id) REFERENCES roles(role_id)
    ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Personas =====================
CREATE TABLE author_rank (
  rank_id       SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
  rank_name     VARCHAR(50) NOT NULL,
  reward_rate   DECIMAL(5,2) NOT NULL,
  min_followers INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (rank_id),
  UNIQUE KEY ux_author_rank_name (rank_name)
) ENGINE=InnoDB;

CREATE TABLE author (
  account_id        BIGINT UNSIGNED NOT NULL,
  restricted        TINYINT(1) NOT NULL DEFAULT 0,
  rank_id           SMALLINT UNSIGNED NULL,
  verified_status   TINYINT(1) NOT NULL DEFAULT 0,
  total_story       INT UNSIGNED NOT NULL DEFAULT 0,
  total_follower    INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_author_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_author_rank FOREIGN KEY (rank_id)
    REFERENCES author_rank(rank_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE reader (
  account_id   BIGINT UNSIGNED NOT NULL,
  bio          TEXT NULL,
  gender       ENUM('male','female','other','unspecified') NOT NULL DEFAULT 'unspecified',
  birthdate    DATE NULL,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_reader_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

-- Staff
CREATE TABLE admin (
  account_id   BIGINT UNSIGNED NOT NULL,
  department   VARCHAR(100) NULL,
  notes        TEXT NULL,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_admin_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE ContentMod (
  account_id             BIGINT UNSIGNED NOT NULL,
  assigned_date          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  phone                  VARCHAR(32) NULL,
  total_approved_stories INT UNSIGNED NOT NULL DEFAULT 0,
  total_rejected_stories INT UNSIGNED NOT NULL DEFAULT 0,
  total_reported_handled INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_cmod_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE OperationMod (
  account_id        BIGINT UNSIGNED NOT NULL,
  assigned_date     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reports_generated INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id),
  CONSTRAINT fk_omod_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Catalog & content =====================
CREATE TABLE story (
  story_id     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  title        VARCHAR(255) NOT NULL,
  author_id    BIGINT UNSIGNED NOT NULL,  -- author.account_id
  `desc`       MEDIUMTEXT NULL,
  cover_url    VARCHAR(512) NULL,
  status       ENUM('draft','published','hidden','removed') NOT NULL DEFAULT 'draft',
  is_premium   TINYINT(1) NOT NULL DEFAULT 0,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (story_id),
  KEY ix_story_author (author_id),
  FULLTEXT KEY ft_story_title_desc (title, `desc`),
  CONSTRAINT fk_story_author FOREIGN KEY (author_id)
    REFERENCES author(account_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE chapters (
  chapter_id   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  story_id     BIGINT UNSIGNED NOT NULL,
  chapter_no   INT UNSIGNED NOT NULL,
  dias_price   INT UNSIGNED NOT NULL DEFAULT 0,
  access_type  ENUM('free','coin','sub_only') NOT NULL DEFAULT 'free',
  status       ENUM('draft','published','locked','removed') NOT NULL DEFAULT 'draft',
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (chapter_id),
  UNIQUE KEY ux_chapter_story_no (story_id, chapter_no),
  KEY ix_chapter_story (story_id),
  CONSTRAINT fk_chapter_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE language_list (
  lang_id     SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
  lang_name   VARCHAR(64) NOT NULL,
  PRIMARY KEY (lang_id),
  UNIQUE KEY ux_lang_name (lang_name)
) ENGINE=InnoDB;

CREATE TABLE chapter_localizations (
  chapter_id  BIGINT UNSIGNED NOT NULL,
  lang_id     SMALLINT UNSIGNED NOT NULL,
  content     LONGTEXT NOT NULL,
  word_count  INT UNSIGNED NOT NULL DEFAULT 0,
  cloud_url   VARCHAR(512) NULL,
  PRIMARY KEY (chapter_id, lang_id),
  CONSTRAINT fk_chloc_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_chloc_lang FOREIGN KEY (lang_id)
    REFERENCES language_list(lang_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE tags (
  tag_id     INT UNSIGNED NOT NULL AUTO_INCREMENT,
  tag_name   VARCHAR(64) NOT NULL,
  PRIMARY KEY (tag_id),
  UNIQUE KEY ux_tag_name (tag_name)
) ENGINE=InnoDB;

CREATE TABLE story_tags (
  story_id    BIGINT UNSIGNED NOT NULL,
  tag_id      INT UNSIGNED NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (story_id, tag_id),
  KEY ix_story_tags_tag (tag_id),
  CONSTRAINT fk_story_tags_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_story_tags_tag FOREIGN KEY (tag_id)
    REFERENCES tags(tag_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Social =====================
CREATE TABLE follow (
  follower_id    BIGINT UNSIGNED NOT NULL, -- reader.account_id
  followee_id    BIGINT UNSIGNED NOT NULL, -- author.account_id
  noti_new_story TINYINT(1) NOT NULL DEFAULT 1,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (follower_id, followee_id),
  KEY ix_follow_followee (followee_id),
  CONSTRAINT fk_follow_follower FOREIGN KEY (follower_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_follow_followee FOREIGN KEY (followee_id)
    REFERENCES author(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE favorite_story (
  reader_id        BIGINT UNSIGNED NOT NULL, -- reader.account_id
  story_id         BIGINT UNSIGNED NOT NULL,
  noti_new_chapter TINYINT(1) NOT NULL DEFAULT 1,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (reader_id, story_id),
  KEY ix_fav_story (story_id),
  CONSTRAINT fk_fav_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_fav_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE chapter_comment (
  comment_id  BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  reader_id   BIGINT UNSIGNED NOT NULL,
  chapter_id  BIGINT UNSIGNED NOT NULL,
  content     TEXT NOT NULL,
  status      ENUM('visible','hidden','removed') NOT NULL DEFAULT 'visible',
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (comment_id),
  KEY ix_cmt_reader (reader_id),
  KEY ix_cmt_chapter (chapter_id),
  CONSTRAINT fk_cmt_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cmt_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Voice (TTS) =====================
CREATE TABLE voice_list (
  voice_id    SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
  voice_name  VARCHAR(64) NOT NULL,
  PRIMARY KEY (voice_id),
  UNIQUE KEY ux_voice_name (voice_name)
) ENGINE=InnoDB;

CREATE TABLE chapter_voices (
  chapter_id  BIGINT UNSIGNED NOT NULL,
  voice_id    SMALLINT UNSIGNED NOT NULL,
  cloud_url   VARCHAR(512) NULL,
  PRIMARY KEY (chapter_id, voice_id),
  CONSTRAINT fk_chvoice_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_chvoice_voice FOREIGN KEY (voice_id)
    REFERENCES voice_list(voice_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Moderation =====================
CREATE TABLE reports (
  report_id     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  target_type   ENUM('story','chapter','comment','user') NOT NULL,
  target_id     BIGINT UNSIGNED NOT NULL,
  reporter_id   BIGINT UNSIGNED NOT NULL,
  reason        VARCHAR(255) NOT NULL,
  details       TEXT NULL,
  status        ENUM('open','in_review','resolved','rejected') NOT NULL DEFAULT 'open',
  moderator_id  BIGINT UNSIGNED NULL,
  reviewed_at   DATETIME NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (report_id),
  KEY ix_reports_target (target_type, target_id),
  KEY ix_reports_reporter (reporter_id),
  KEY ix_reports_moderator (moderator_id),
  CONSTRAINT fk_reports_reporter FOREIGN KEY (reporter_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_reports_moderator FOREIGN KEY (moderator_id)
    REFERENCES ContentMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE content_approve (
  review_id     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  approve_type  ENUM('story','chapter') NOT NULL,
  story_id      BIGINT UNSIGNED NULL,
  chapter_id    BIGINT UNSIGNED NULL,
  source        ENUM('ai','human') NOT NULL DEFAULT 'human',
  ai_score      DECIMAL(5,2) NULL,
  status        ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  moderator_id  BIGINT UNSIGNED NOT NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (review_id),
  KEY ix_cappr_story (story_id),
  KEY ix_cappr_chapter (chapter_id),
  CONSTRAINT fk_cappr_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cappr_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cappr_moderator FOREIGN KEY (moderator_id)
    REFERENCES ContentMod(account_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE op_requests (
  request_id       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  requester_id     BIGINT UNSIGNED NOT NULL,
  request_type     ENUM('withdraw','other') NOT NULL,
  request_content  TEXT NULL,
  withdraw_amount  BIGINT UNSIGNED NULL,
  omod_id          BIGINT UNSIGNED NULL,
  status           ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  withdraw_code    VARCHAR(64) NULL,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (request_id),
  KEY ix_opreq_requester (requester_id),
  KEY ix_opreq_omod (omod_id),
  CONSTRAINT fk_opreq_requester FOREIGN KEY (requester_id)
    REFERENCES account(account_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_opreq_omod FOREIGN KEY (omod_id)
    REFERENCES OperationMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Wallet & Payments =====================
CREATE TABLE dia_wallet (
  wallet_id     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  account_id    BIGINT UNSIGNED NOT NULL,
  balance_coin  BIGINT NOT NULL DEFAULT 0,
  locked_coin   BIGINT NOT NULL DEFAULT 0,
  updated_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (wallet_id),
  UNIQUE KEY ux_wallet_account (account_id),
  CONSTRAINT fk_wallet_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE wallet_payment (
  trs_id        BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  wallet_id     BIGINT UNSIGNED NOT NULL,
  type          ENUM('purchase','withdraw','topup','adjust') NOT NULL,
  coin_delta    BIGINT NOT NULL,
  coin_after    BIGINT NOT NULL,
  ref_id        BIGINT UNSIGNED NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (trs_id),
  KEY ix_wpay_wallet (wallet_id),
  KEY ix_wpay_type (type),
  CONSTRAINT fk_wpay_wallet FOREIGN KEY (wallet_id)
    REFERENCES dia_wallet(wallet_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE dia_payment (
  topup_id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  wallet_id        BIGINT UNSIGNED NOT NULL,
  provider         VARCHAR(50) NOT NULL,
  amount_vnd       BIGINT UNSIGNED NOT NULL,
  diamond_granted  BIGINT UNSIGNED NOT NULL,
  status           ENUM('pending','success','failed','refunded') NOT NULL DEFAULT 'pending',
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (topup_id),
  KEY ix_topup_wallet (wallet_id),
  CONSTRAINT fk_topup_wallet FOREIGN KEY (wallet_id)
    REFERENCES dia_wallet(wallet_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE chapter_purchase_log (
  chapter_purchase_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  chapter_id    BIGINT UNSIGNED NOT NULL,
  account_id    BIGINT UNSIGNED NOT NULL,
  dia_price     INT UNSIGNED NOT NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (chapter_purchase_id),
  UNIQUE KEY ux_purchase_unique (chapter_id, account_id),
  KEY ix_purchase_account (account_id),
  CONSTRAINT fk_cpl_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapters(chapter_id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_cpl_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

-- ===================== Subscriptions =====================
CREATE TABLE subscription_plan (
  plan_code          VARCHAR(32) NOT NULL,
  plan_name          VARCHAR(64) NOT NULL,
  price_coin         INT UNSIGNED NOT NULL,
  daily_claim_limit  INT UNSIGNED NOT NULL DEFAULT 0,
  duration_days      INT UNSIGNED NOT NULL,
  PRIMARY KEY (plan_code)
) ENGINE=InnoDB;

CREATE TABLE subcriptions ( 
  sub_id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id         BIGINT UNSIGNED NOT NULL, -- account_id
  plan_code       VARCHAR(32) NOT NULL,
  start_at        DATETIME NOT NULL,
  end_at          DATETIME NOT NULL,
  last_claim_date DATE NULL,
  claimed_today   TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (sub_id),
  KEY ix_sub_user (user_id),
  KEY ix_sub_plan (plan_code),
  CONSTRAINT fk_sub_user FOREIGN KEY (user_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_sub_plan FOREIGN KEY (plan_code)
    REFERENCES subscription_plan(plan_code) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;


