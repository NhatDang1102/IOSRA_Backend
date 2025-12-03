-- MySQL 8.0+ schema for IOSRA (GUID-based IDs)
SET NAMES utf8mb4;
SET time_zone = "+00:00";

CREATE DATABASE IF NOT EXISTS IOSRA_DB
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;
USE IOSRA_DB;

-- ===================== Core accounts & role =====================
CREATE TABLE account (
  account_id      CHAR(36) NOT NULL,
  username        VARCHAR(50) NOT NULL,
  email           VARCHAR(255) NOT NULL,
  password_hash   VARCHAR(255) NOT NULL,
  status          ENUM('unbanned','banned') NOT NULL DEFAULT 'unbanned',
  strike_status   ENUM('none','restricted') NOT NULL DEFAULT 'none',
  strike          TINYINT UNSIGNED NOT NULL DEFAULT 0,
  strike_restricted_until DATETIME NULL,
  avatar_url      VARCHAR(512) NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (account_id),
  UNIQUE KEY ux_account_username (username),
  UNIQUE KEY ux_account_email (email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE role (
  role_id     CHAR(36) NOT NULL,
  role_code   VARCHAR(32) NOT NULL,
  role_name   VARCHAR(64) NOT NULL,
  PRIMARY KEY (role_id),
  UNIQUE KEY ux_roles_code (role_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `role` VALUES ('a781ee08-b7bf-11f0-9007-e27119d7eafa','reader','Reader'),('a781f21b-b7bf-11f0-9007-e27119d7eafa','author','Author'),('a781f323-b7bf-11f0-9007-e27119d7eafa','cmod','Content Moderator'),('a781f390-b7bf-11f0-9007-e27119d7eafa','omod','Operation Moderator'),('a781f3ef-b7bf-11f0-9007-e27119d7eafa','admin','Administrator');


CREATE TABLE account_role (
  account_id  CHAR(36) NOT NULL,
  role_id     CHAR(36) NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (account_id, role_id),
  KEY fk_account_roles_role (role_id),
  CONSTRAINT fk_account_roles_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_account_roles_role FOREIGN KEY (role_id)
    REFERENCES role(role_id) ON DELETE RESTRICT ON UPDATE CASCADE
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

INSERT INTO `author_rank` VALUES ('a7814236-b7bf-11f0-9007-e27119d7eafa','Casual',0.00,0),('a7815277-b7bf-11f0-9007-e27119d7eafa','Bronze',50.00,5),('a781563b-b7bf-11f0-9007-e27119d7eafa','Gold',60.00,10),('a7815711-b7bf-11f0-9007-e27119d7eafa','Diamond',70.00,15);


CREATE TABLE author (
  account_id        CHAR(36) NOT NULL,
  restricted        TINYINT(1) NOT NULL DEFAULT 0,
  rank_id           CHAR(36) NULL,
  verified_status   TINYINT(1) NOT NULL DEFAULT 0,
  total_story       INT UNSIGNED NOT NULL DEFAULT 0,
  total_follower    INT UNSIGNED NOT NULL DEFAULT 0,
  revenue_balance_vnd   BIGINT NOT NULL DEFAULT 0,
  revenue_pending_vnd   BIGINT NOT NULL DEFAULT 0,
  revenue_withdrawn_vnd BIGINT NOT NULL DEFAULT 0,
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
  total_approved_chapters INT UNSIGNED NOT NULL DEFAULT 0,
  total_rejected_chapters INT UNSIGNED NOT NULL DEFAULT 0,
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

CREATE TABLE tag (
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
  voice_id          CHAR(36) NOT NULL,
  voice_name        VARCHAR(64) NOT NULL,
  voice_code        VARCHAR(32) NOT NULL,
  provider_voice_id VARCHAR(128) NOT NULL,
  description       VARCHAR(256) NULL,
  is_active         TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (voice_id),
  UNIQUE KEY ux_voice_name (voice_name),
  UNIQUE KEY ux_voice_code (voice_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter (
  chapter_id   CHAR(36) NOT NULL,
  story_id     CHAR(36) NOT NULL,
  chapter_no   INT UNSIGNED NOT NULL,
  language_id  CHAR(36) NOT NULL,
  title        VARCHAR(255) NOT NULL,
  summary      TEXT NULL,
  dias_price   INT UNSIGNED NOT NULL DEFAULT 0,
  access_type  ENUM('free','dias','sub_only') NOT NULL DEFAULT 'free',
    content_url  VARCHAR(512) NULL,
    word_count   INT UNSIGNED NOT NULL DEFAULT 0,
    char_count   INT UNSIGNED NOT NULL DEFAULT 0,
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

CREATE TABLE story_tag (
  story_id   CHAR(36) NOT NULL,
  tag_id     CHAR(36) NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (story_id, tag_id),
  KEY ix_story_tags_tag (tag_id),
  CONSTRAINT fk_story_tags_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_story_tags_tag FOREIGN KEY (tag_id)
    REFERENCES tag(tag_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE content_approve (
  review_id     CHAR(36) NOT NULL,
  approve_type  ENUM('story','chapter') NOT NULL,
  story_id      CHAR(36) NULL,
  chapter_id    CHAR(36) NULL,
  ai_score      DECIMAL(5,2) NULL,
  ai_feedback   TEXT NULL,
  status        ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  moderator_id  CHAR(36) NULL,
  moderator_feedback TEXT NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (review_id),
  KEY ix_cappr_story (story_id),
  KEY ix_cappr_chapter (chapter_id),
  KEY fk_cappr_moderator (moderator_id),
  CONSTRAINT fk_cappr_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cappr_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cappr_moderator FOREIGN KEY (moderator_id)
    REFERENCES ContentMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_comment (
  comment_id CHAR(36) NOT NULL,
  reader_id  CHAR(36) NOT NULL,
  story_id   CHAR(36) NOT NULL,
  chapter_id CHAR(36) NOT NULL,
  parent_comment_id CHAR(36) NULL,
  content    TEXT NOT NULL,
  status     ENUM('visible','hidden','removed') NOT NULL DEFAULT 'visible',
  is_locked  TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (comment_id),
  KEY ix_cmt_reader (reader_id),
  KEY ix_cmt_chapter (chapter_id),
  KEY ix_cmt_story (story_id),
  KEY ix_cmt_parent (parent_comment_id),
  CONSTRAINT fk_cmt_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cmt_chapter FOREIGN KEY (chapter_id)
    REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cmt_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_cmt_parent FOREIGN KEY (parent_comment_id)
    REFERENCES chapter_comment(comment_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_comment_reaction (
  reaction_id   CHAR(36) NOT NULL,
  comment_id    CHAR(36) NOT NULL,
  reader_id     CHAR(36) NOT NULL,
  reaction_type ENUM('like','dislike') NOT NULL,
  created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (reaction_id),
  UNIQUE KEY ux_ccr_comment_reader (comment_id, reader_id),
  KEY ix_ccr_comment (comment_id),
  KEY ix_ccr_reader (reader_id),
  CONSTRAINT fk_ccr_comment FOREIGN KEY (comment_id)
    REFERENCES chapter_comment(comment_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_ccr_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_mood (
  mood_code varchar(32) NOT NULL,
  mood_name varchar(64) NOT NULL,
  description varchar(255) DEFAULT NULL,
  created_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (mood_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE chapter_mood_track (
  track_id         CHAR(36) NOT NULL,
  mood_code        VARCHAR(32) NOT NULL,
  title            VARCHAR(128) NOT NULL,
  duration_seconds INT NOT NULL DEFAULT 30,
  storage_path     VARCHAR(512) NOT NULL,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (track_id),
  KEY ix_mood_track_mood (mood_code),
  CONSTRAINT fk_mood_track_mood FOREIGN KEY (mood_code)
    REFERENCES chapter_mood(mood_code) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


CREATE TABLE story_rating (
  story_id   CHAR(36) NOT NULL,
  reader_id  CHAR(36) NOT NULL,
  score      TINYINT UNSIGNED NOT NULL,
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (story_id, reader_id),
  KEY ix_rating_reader (reader_id),
  CONSTRAINT fk_rating_story FOREIGN KEY (story_id)
    REFERENCES story(story_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_rating_reader FOREIGN KEY (reader_id)
    REFERENCES reader(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

  CREATE TABLE chapter_localization (
    chapter_id CHAR(36) NOT NULL,
    lang_id    CHAR(36) NOT NULL,
    word_count INT UNSIGNED NOT NULL DEFAULT 0,
    content_url  VARCHAR(512) NULL,
    PRIMARY KEY (chapter_id, lang_id),
    KEY fk_chloc_lang (lang_id),
    CONSTRAINT fk_chloc_chapter FOREIGN KEY (chapter_id)
      REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT fk_chloc_lang FOREIGN KEY (lang_id)
      REFERENCES language_list(lang_id) ON DELETE RESTRICT ON UPDATE CASCADE
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

  CREATE TABLE chapter_voice (
      chapter_id   CHAR(36) NOT NULL,
      voice_id     CHAR(36) NOT NULL,
      storage_path VARCHAR(512) NULL,
      status       ENUM('pending','processing','ready','failed') NOT NULL DEFAULT 'pending',
      requested_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
      completed_at DATETIME NULL,
      char_cost    INT NOT NULL DEFAULT 0,
      dias_price   INT UNSIGNED NOT NULL DEFAULT 0,
      error_message TEXT NULL,
      PRIMARY KEY (chapter_id, voice_id),
    KEY fk_chvoice_voice (voice_id),
    CONSTRAINT fk_chvoice_chapter FOREIGN KEY (chapter_id)
      REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
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
    REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
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

CREATE TABLE notification (
  notification_id CHAR(36) NOT NULL,
  recipient_id    CHAR(36) NOT NULL,
  type            ENUM('op_request','story_decision','chapter_decision','new_story','new_chapter','general','new_follower','chapter_comment','story_rating','strike_warning','author_rank_upgrade','subscription_reminder','comment_reply','chapter_purchase','voice_purchase') NOT NULL,
  title           VARCHAR(200) NOT NULL,
  message         TEXT NOT NULL,
  payload         JSON NULL,
  is_read         TINYINT(1) NOT NULL DEFAULT 0,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (notification_id),
  KEY ix_notifications_recipient (recipient_id),
  CONSTRAINT fk_notifications_recipient FOREIGN KEY (recipient_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE story_weekly_view (
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

CREATE TABLE topup_pricing (
  pricing_id       CHAR(36) NOT NULL,
  amount_vnd       BIGINT UNSIGNED NOT NULL,
  diamond_granted  BIGINT UNSIGNED NOT NULL,
  is_active        TINYINT(1) NOT NULL DEFAULT 1,
  updated_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (pricing_id),
  UNIQUE KEY ux_topup_amount (amount_vnd)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `topup_pricing` VALUES ('530b368b-c2d6-11f0-831d-be03b0a058d6',2000,550,1,'2025-12-02 08:14:33'),('530b3bc7-c2d6-11f0-831d-be03b0a058d6',3000,1150,1,'2025-12-02 08:14:33'),('530b3fab-c2d6-11f0-831d-be03b0a058d6',4000,2400,1,'2025-12-02 08:14:34');


CREATE TABLE voice_wallet (
  wallet_id    CHAR(36) NOT NULL,
  account_id   CHAR(36) NOT NULL,
  balance_chars BIGINT NOT NULL DEFAULT 0,
  updated_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (wallet_id),
  UNIQUE KEY ux_voice_wallet_account (account_id),
  CONSTRAINT fk_voice_wallet_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

  CREATE TABLE voice_topup_pricing (
    pricing_id       CHAR(36) NOT NULL,
    amount_vnd       BIGINT UNSIGNED NOT NULL,
    chars_granted    BIGINT UNSIGNED NOT NULL,
    is_active        TINYINT(1) NOT NULL DEFAULT 1,
    updated_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (pricing_id),
    UNIQUE KEY ux_voice_topup_amount (amount_vnd)
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `voice_topup_pricing` VALUES ('4ff48f2a-c5f2-11f0-a5ae-4ac6068f4a77',5000,10000,1,'2025-12-02 12:44:18'),('4ff4ae7d-c5f2-11f0-a5ae-4ac6068f4a77',6000,21500,1,'2025-12-02 12:44:43');


  CREATE TABLE chapter_price_rule (
  rule_id char(36) NOT NULL,
  min_word_count int unsigned NOT NULL,
  max_word_count int unsigned DEFAULT NULL,
  dias_price int unsigned NOT NULL,
  created_at datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (rule_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `chapter_price_rule` VALUES ('867e4963-be24-11f0-9007-e27119d7eafa',0,3000,5,'2025-11-10 11:00:53'),('867e5120-be24-11f0-9007-e27119d7eafa',3001,4000,6,'2025-11-10 11:00:53'),('867e5246-be24-11f0-9007-e27119d7eafa',4001,5000,7,'2025-11-10 11:00:53'),('867e528a-be24-11f0-9007-e27119d7eafa',5001,6000,8,'2025-11-10 11:00:53'),('867e52b9-be24-11f0-9007-e27119d7eafa',6001,7000,9,'2025-11-10 11:00:53'),('867e52e8-be24-11f0-9007-e27119d7eafa',7001,NULL,10,'2025-11-10 11:00:53');


CREATE TABLE voice_price_rule (
    rule_id        CHAR(36) NOT NULL,
    min_char_count INT UNSIGNED NOT NULL,
    max_char_count INT UNSIGNED NULL,
    dias_price     INT UNSIGNED NOT NULL,
    created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (rule_id),
    KEY ix_voice_price_min (min_char_count)
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO `voice_price_rule` VALUES ('9f3fb829-4d70-4fe2-957b-0d4b6f3be7cc',5001,10000,10,'2025-11-25 11:01:19'),('a3f3e55e-90de-4d63-9ef2-6b1744931fcb',1501,5000,7,'2025-11-25 11:01:19'),('bb65ce30-0bdd-4f33-9f2d-7c28d21a5e3b',0,1500,5,'2025-11-25 11:01:19'),('c4af9db3-1dde-4b14-9b94-0888405c81f0',10001,NULL,15,'2025-11-25 11:01:19');


CREATE TABLE voice_payment (
  topup_id        CHAR(36) NOT NULL,
  wallet_id       CHAR(36) NOT NULL,
  provider        VARCHAR(50) NOT NULL,
  order_code      VARCHAR(50) NOT NULL,
  amount_vnd      BIGINT UNSIGNED NOT NULL,
  chars_granted   BIGINT UNSIGNED NOT NULL,
  status          ENUM('pending','success','failed','refunded','cancelled') NOT NULL DEFAULT 'pending',
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (topup_id),
  UNIQUE KEY ux_voice_order_code (order_code),
  KEY ix_voice_payment_wallet (wallet_id),
  CONSTRAINT fk_voice_payment_wallet FOREIGN KEY (wallet_id)
    REFERENCES voice_wallet(wallet_id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

  CREATE TABLE voice_wallet_payment (
    trs_id     CHAR(36) NOT NULL,
    wallet_id  CHAR(36) NOT NULL,
    type       ENUM('topup','purchase','refund') NOT NULL DEFAULT 'purchase',
    char_delta BIGINT NOT NULL,
    char_after BIGINT NOT NULL,
    ref_id     CHAR(36) NULL,
    note       VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (trs_id),
    KEY ix_voice_wallet_payment_wallet (wallet_id),
    CONSTRAINT fk_voice_wallet_payment_wallet FOREIGN KEY (wallet_id)
      REFERENCES voice_wallet(wallet_id) ON DELETE CASCADE ON UPDATE CASCADE
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

  CREATE TABLE voice_purchase_log (
    voice_purchase_id CHAR(36) NOT NULL,
    chapter_id        CHAR(36) NOT NULL,
    account_id        CHAR(36) NOT NULL,
    total_dias        INT UNSIGNED NOT NULL,
    created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (voice_purchase_id),
    KEY ix_voice_purchase_chapter (chapter_id),
    KEY ix_voice_purchase_account (account_id),
    CONSTRAINT fk_voice_purchase_chapter FOREIGN KEY (chapter_id)
      REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT fk_voice_purchase_account FOREIGN KEY (account_id)
      REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

  CREATE TABLE voice_purchase_item (
    purchase_item_id  CHAR(36) NOT NULL,
    voice_purchase_id CHAR(36) NOT NULL,
    account_id        CHAR(36) NOT NULL,
    chapter_id        CHAR(36) NOT NULL,
    voice_id          CHAR(36) NOT NULL,
    dia_price         INT UNSIGNED NOT NULL,
    created_at        DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (purchase_item_id),
    UNIQUE KEY ux_voice_purchase_item (account_id, chapter_id, voice_id),
    KEY ix_voice_purchase_item_purchase (voice_purchase_id),
    KEY ix_voice_purchase_item_voice (voice_id),
    CONSTRAINT fk_voice_purchase_item_purchase FOREIGN KEY (voice_purchase_id)
      REFERENCES voice_purchase_log(voice_purchase_id) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT fk_voice_purchase_item_account FOREIGN KEY (account_id)
      REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT fk_voice_purchase_item_chapter FOREIGN KEY (chapter_id)
      REFERENCES chapter(chapter_id) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT fk_voice_purchase_item_voice FOREIGN KEY (voice_id)
      REFERENCES voice_list(voice_id) ON DELETE CASCADE ON UPDATE CASCADE
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE payment_receipt (
  receipt_id  CHAR(36) NOT NULL,
  account_id  CHAR(36) NOT NULL,
  ref_id      CHAR(36) NOT NULL,
  type        ENUM('dia_topup','voice_topup','subscription') NOT NULL,
  amount_vnd  BIGINT UNSIGNED NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (receipt_id),
  KEY ix_receipt_account (account_id),
  CONSTRAINT fk_receipt_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE subscription_payment (
  payment_id  CHAR(36) NOT NULL,
  account_id  CHAR(36) NOT NULL,
  plan_code   VARCHAR(32) NOT NULL,
  provider    VARCHAR(50) NOT NULL,
  order_code  VARCHAR(50) NOT NULL,
  amount_vnd  BIGINT UNSIGNED NOT NULL,
  status      ENUM('pending','success','failed','cancelled') NOT NULL DEFAULT 'pending',
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (payment_id),
  UNIQUE KEY ux_sub_payment_order (order_code),
  KEY ix_sub_payment_account (account_id),
  KEY ix_sub_payment_plan (plan_code),
  CONSTRAINT fk_subpay_account FOREIGN KEY (account_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_subpay_plan FOREIGN KEY (plan_code)
    REFERENCES subscription_plan(plan_code) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE dia_payment (
  topup_id        CHAR(36) NOT NULL,
  wallet_id       CHAR(36) NOT NULL,
  provider        VARCHAR(50) NOT NULL,
  amount_vnd      BIGINT UNSIGNED NOT NULL,
  diamond_granted BIGINT UNSIGNED NOT NULL,
  status          ENUM('pending','success','failed','refunded','cancelled') NOT NULL DEFAULT 'pending',
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

INSERT INTO voice_list (voice_id, voice_name, voice_code, provider_voice_id, description, is_active)
VALUES
  ('6f800e5e-7d15-4a7d-9d35-1c50b0f395e5', 'Nam trầm', 'male_low', 'TxGEqnHWrfWFTfGW9XjX', 'Male deep tone', 1),
  ('a1300832-4e02-426a-87c6-6b436a3d7909', 'Nam cao', 'male_high', 'VR6AewLTigWG4xSOukaG', 'Male bright tone', 1),
  ('4fcb206b-544f-4d32-920f-b1c3159af645', 'Nữ trầm', 'female_low', '21m00Tcm4TlvDq8ikWAM', 'Female warm tone', 1),
  ('9c77afca-88f9-41bc-a13d-08d601b93a60', 'Nữ cao', 'female_high', 'AZnzlk1XvdvUeBnXmlld', 'Female bright tone', 1)
ON DUPLICATE KEY UPDATE
    voice_name = VALUES(voice_name),
    provider_voice_id = VALUES(provider_voice_id),
    description = VALUES(description),
    is_active = VALUES(is_active);

  INSERT INTO voice_price_rule (rule_id, min_char_count, max_char_count, dias_price)
  VALUES
    ('bb65ce30-0bdd-4f33-9f2d-7c28d21a5e3b', 0, 1500, 5),
    ('a3f3e55e-90de-4d63-9ef2-6b1744931fcb', 1501, 5000, 7),
    ('9f3fb829-4d70-4fe2-957b-0d4b6f3be7cc', 5001, 10000, 10),
    ('c4af9db3-1dde-4b14-9b94-0888405c81f0', 10001, NULL, 15)
  ON DUPLICATE KEY UPDATE
    min_char_count = VALUES(min_char_count),
    max_char_count = VALUES(max_char_count),
    dias_price = VALUES(dias_price);

-- ===================== Requests & moderation =====================
CREATE TABLE op_request (
  request_id       CHAR(36) NOT NULL,
  requester_id     CHAR(36) NOT NULL,
  request_type     ENUM('withdraw','rank_up','become_author') NOT NULL DEFAULT 'withdraw',
  request_content  TEXT NULL,
  withdraw_amount  BIGINT UNSIGNED NULL,
  omod_id          CHAR(36) NULL,
  omod_note        TEXT NULL,
  status           ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
  withdraw_code    VARCHAR(64) NULL,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  reviewed_at      DATETIME NULL,
  PRIMARY KEY (request_id),
  KEY ix_opreq_requester (requester_id),
  KEY ix_opreq_omod (omod_id),
  CONSTRAINT fk_opreq_requester FOREIGN KEY (requester_id)
    REFERENCES account(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_opreq_omod FOREIGN KEY (omod_id)
    REFERENCES OperationMod(account_id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE author_revenue_transaction (
  trans_id         CHAR(36) NOT NULL,
  author_id        CHAR(36) NOT NULL,
    type             ENUM('purchase','withdraw_reserve','withdraw_release') NOT NULL,
  amount_vnd       BIGINT NOT NULL DEFAULT 0,
    purchase_log_id  CHAR(36) NULL,
    voice_purchase_id CHAR(36) NULL,
    request_id       CHAR(36) NULL,
    metadata         JSON NULL,
  created_at       DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (trans_id),
  KEY ix_art_author (author_id),
  KEY ix_art_purchase (purchase_log_id),
    KEY ix_art_request (request_id),
    KEY ix_art_voice_purchase (voice_purchase_id),
  CONSTRAINT fk_art_author FOREIGN KEY (author_id)
    REFERENCES author(account_id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_art_purchase FOREIGN KEY (purchase_log_id)
    REFERENCES chapter_purchase_log(chapter_purchase_id) ON DELETE SET NULL ON UPDATE CASCADE,
    CONSTRAINT fk_art_request FOREIGN KEY (request_id)
      REFERENCES op_request(request_id) ON DELETE SET NULL ON UPDATE CASCADE,
    CONSTRAINT fk_art_voice_purchase FOREIGN KEY (voice_purchase_id)
      REFERENCES voice_purchase_log(voice_purchase_id) ON DELETE SET NULL ON UPDATE CASCADE
  ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE report (
  report_id    CHAR(36) NOT NULL,
  target_type  ENUM('story','chapter','comment','user') NOT NULL,
  target_id    CHAR(36) NOT NULL,
  reporter_id  CHAR(36) NOT NULL,
  reason       ENUM('negative_content','misinformation','spam','ip_infringement') NOT NULL,
  details      TEXT NULL,
  status       ENUM('pending','resolved','rejected') NOT NULL DEFAULT 'pending',
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
  price_vnd       BIGINT UNSIGNED NOT NULL,
  daily_claim_limit INT UNSIGNED NOT NULL,
  duration_days    INT UNSIGNED NOT NULL,
  daily_dias       INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (plan_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE subcription (
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



INSERT INTO subscription_plan (plan_code, plan_name, price_vnd, daily_claim_limit, duration_days, daily_dias)
VALUES ('premium_month', 'Premium Monthly', 199000, 1, 30, 50)
ON DUPLICATE KEY UPDATE
  price_vnd = VALUES(price_vnd),
  duration_days = VALUES(duration_days),
  daily_dias = VALUES(daily_dias);

-- ===================== Chapter mood support =====================
CREATE TABLE IF NOT EXISTS chapter_mood (
  mood_code      VARCHAR(32) NOT NULL,
  mood_name      VARCHAR(64) NOT NULL,
  description    VARCHAR(255) NULL,
  created_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at     DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (mood_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO chapter_mood (mood_code, mood_name, description) VALUES
  ('calm', 'Calm', 'Light, soothing ambient mood'),
  ('sad', 'Melancholic', 'Somber and emotional tone'),
  ('mysterious', 'Mysterious', 'Enigmatic and suspenseful atmosphere'),
  ('excited', 'Excited', 'High energy, adventurous feel'),
  ('romantic', 'Romantic', 'Warm, heartfelt mood'),
  ('neutral', 'Neutral', 'Balanced or unidentified feeling')
ON DUPLICATE KEY UPDATE mood_name = VALUES(mood_name), description = VALUES(description);

ALTER TABLE chapter
  ADD COLUMN mood_code VARCHAR(32) NULL AFTER char_count,
  ADD CONSTRAINT fk_chapter_mood FOREIGN KEY (mood_code)
    REFERENCES chapter_mood(mood_code) ON DELETE SET NULL ON UPDATE CASCADE;

CREATE TABLE IF NOT EXISTS chapter_mood_tracks (
  track_id        CHAR(36) NOT NULL,
  mood_code       VARCHAR(32) NOT NULL,
  title           VARCHAR(128) NOT NULL,
  duration_seconds INT NOT NULL DEFAULT 30,
  storage_path    VARCHAR(512) NOT NULL,
  created_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (track_id),
  KEY ix_mood_track_mood (mood_code),
  CONSTRAINT fk_mood_track_mood FOREIGN KEY (mood_code) REFERENCES chapter_mood(mood_code) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
