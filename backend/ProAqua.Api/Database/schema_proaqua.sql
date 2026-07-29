-- ============================================================
-- ProAqua: БД, пользователь приложения, таблицы, стартовые данные
-- Выполнить после create_deploy_user.sql (можно под root или под deploy):
--   mysql -u root -p < schema_proaqua.sql
--   или в Workbench под deploy — выполнить весь файл
-- ============================================================

CREATE DATABASE IF NOT EXISTS proaqua
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

-- Пользователь ТОЛЬКО для приложения (не для Workbench)
CREATE USER IF NOT EXISTS 'proaqua'@'%' IDENTIFIED BY 'ProAquaApp_ChangeMe_2026!';
CREATE USER IF NOT EXISTS 'proaqua'@'localhost' IDENTIFIED BY 'ProAquaApp_ChangeMe_2026!';
GRANT ALL PRIVILEGES ON proaqua.* TO 'proaqua'@'%';
GRANT ALL PRIVILEGES ON proaqua.* TO 'proaqua'@'localhost';
FLUSH PRIVILEGES;

USE proaqua;

-- ---------- таблицы ----------

CREATE TABLE IF NOT EXISTS Users (
  Id CHAR(36) NOT NULL,
  Phone VARCHAR(32) NOT NULL,
  Name VARCHAR(256) NULL,
  PinHash VARCHAR(200) NOT NULL DEFAULT '',
  MustChangePassword TINYINT(1) NOT NULL DEFAULT 1,
  Role INT NOT NULL DEFAULT 0,
  ReferralCode VARCHAR(16) NOT NULL,
  ReferredByUserId CHAR(36) NULL,
  LoyaltyPoints INT NOT NULL DEFAULT 0,
  LoyaltyLevel INT NOT NULL DEFAULT 1,
  AmoContactId BIGINT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (Id),
  UNIQUE KEY IX_Users_Phone (Phone),
  UNIQUE KEY IX_Users_ReferralCode (ReferralCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Vehicles (
  Id CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  Brand VARCHAR(256) NOT NULL,
  Model VARCHAR(256) NOT NULL,
  PlateNumber VARCHAR(64) NULL,
  Type INT NOT NULL DEFAULT 0,
  PRIMARY KEY (Id),
  KEY IX_Vehicles_UserId (UserId),
  CONSTRAINT FK_Vehicles_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Services (
  Id CHAR(36) NOT NULL,
  Title VARCHAR(256) NOT NULL,
  Description LONGTEXT NOT NULL,
  Category VARCHAR(64) NOT NULL,
  DurationMinutes INT NOT NULL,
  PriceFrom DECIMAL(10,2) NOT NULL,
  ImageUrl VARCHAR(512) NULL,
  BeforeAfterImageUrl VARCHAR(512) NULL,
  ImageData LONGBLOB NULL,
  ImageContentType VARCHAR(100) NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  SortOrder INT NOT NULL DEFAULT 0,
  PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS WorkBays (
  Id CHAR(36) NOT NULL,
  Name VARCHAR(256) NOT NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Bookings (
  Id CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  ServiceId CHAR(36) NOT NULL,
  VehicleId CHAR(36) NULL,
  WorkBayId CHAR(36) NULL,
  MasterUserId CHAR(36) NULL,
  StartAt DATETIME(6) NOT NULL,
  EndAt DATETIME(6) NOT NULL,
  Status INT NOT NULL DEFAULT 1,
  Comment LONGTEXT NULL,
  FinalPrice DECIMAL(10,2) NULL,
  AmoLeadId BIGINT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_Bookings_StartAt (StartAt),
  KEY IX_Bookings_UserId (UserId),
  KEY IX_Bookings_ServiceId (ServiceId),
  KEY IX_Bookings_VehicleId (VehicleId),
  KEY IX_Bookings_WorkBayId (WorkBayId),
  KEY IX_Bookings_MasterUserId (MasterUserId),
  CONSTRAINT FK_Bookings_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
  CONSTRAINT FK_Bookings_Services FOREIGN KEY (ServiceId) REFERENCES Services (Id) ON DELETE RESTRICT,
  CONSTRAINT FK_Bookings_Vehicles FOREIGN KEY (VehicleId) REFERENCES Vehicles (Id) ON DELETE SET NULL,
  CONSTRAINT FK_Bookings_WorkBays FOREIGN KEY (WorkBayId) REFERENCES WorkBays (Id) ON DELETE SET NULL,
  CONSTRAINT FK_Bookings_Masters FOREIGN KEY (MasterUserId) REFERENCES Users (Id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS DeviceTokens (
  Id CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  Token VARCHAR(512) NOT NULL,
  Platform VARCHAR(32) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY IX_DeviceTokens_UserId_Token (UserId, Token),
  CONSTRAINT FK_DeviceTokens_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS LoyaltyTransactions (
  Id CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  PointsDelta INT NOT NULL,
  Reason VARCHAR(512) NOT NULL,
  BookingId CHAR(36) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_LoyaltyTransactions_UserId (UserId),
  CONSTRAINT FK_LoyaltyTransactions_Users FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS PromoCodes (
  Id CHAR(36) NOT NULL,
  Code VARCHAR(64) NOT NULL,
  PercentOff INT NOT NULL,
  BonusPoints INT NULL,
  ValidUntil DATETIME(6) NULL,
  IsActive TINYINT(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (Id),
  UNIQUE KEY IX_PromoCodes_Code (Code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ---------- seed (идемпотентно через IGNORE) ----------
-- PIN сотрудников = 1234 (BCrypt)

INSERT IGNORE INTO Users (Id, Phone, Name, PinHash, MustChangePassword, Role, ReferralCode, LoyaltyPoints, LoyaltyLevel, CreatedAt, IsActive)
VALUES
('a1111111-1111-1111-1111-111111111111', '+79000000001', 'Администратор',
 '$2a$11$.ZzkEe093hNM.MhLWnoAM.mpwQ1K3.hEhb2c6z0gX5zwRm8s2p85S', 0,
 2, 'ADMIN01', 0, 3, UTC_TIMESTAMP(6), 1),
('a2222222-2222-2222-2222-222222222222', '+79000000002', 'Мастер Алексей',
 '$2a$11$.ZzkEe093hNM.MhLWnoAM.mpwQ1K3.hEhb2c6z0gX5zwRm8s2p85S', 0,
 1, 'MASTER1', 0, 1, UTC_TIMESTAMP(6), 1);

INSERT IGNORE INTO WorkBays (Id, Name, IsActive) VALUES
('b1111111-1111-1111-1111-111111111111', 'Бокс 1', 1),
('b2222222-2222-2222-2222-222222222222', 'Бокс 2', 1),
('b3333333-3333-3333-3333-333333333333', 'Детейлинг-пост', 1);

INSERT IGNORE INTO Services (Id, Title, Description, Category, DurationMinutes, PriceFrom, ImageUrl, BeforeAfterImageUrl, IsActive, SortOrder)
VALUES
('c1111111-1111-1111-1111-111111111111', 'Экспресс-мойка',
 'Кузов, диски, сушка. Быстро вернём блеск перед городом.',
 'wash', 40, 800.00, '/uploads/service-wash.png', NULL, 1, 1),
('c2222222-2222-2222-2222-222222222222', 'Комплексная мойка',
 'Снаружи и внутри: пылесос, пластик, стёкла, коврики.',
 'wash', 90, 1800.00, '/uploads/service-interior.png', NULL, 1, 2),
('c3333333-3333-3333-3333-333333333333', 'Детейлинг / керамика',
 'Глубокая очистка и защитное покрытие. Эффект «до / после» видно сразу.',
 'detailing', 240, 12000.00, '/uploads/service-ceramic.png', '/uploads/detailing-before-after.png', 1, 3);

INSERT IGNORE INTO PromoCodes (Id, Code, PercentOff, BonusPoints, ValidUntil, IsActive)
VALUES
('d1111111-1111-1111-1111-111111111111', 'WELCOME10', 10, 50, DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 1 YEAR), 1);

CREATE TABLE IF NOT EXISTS Promotions (
  Id char(36) NOT NULL,
  Title varchar(200) NOT NULL,
  Description longtext NOT NULL,
  StartsAt datetime(6) NOT NULL,
  EndsAt datetime(6) NOT NULL,
  IsActive tinyint(1) NOT NULL,
  ImageUrl varchar(500) NULL,
  ImageData longblob NULL,
  ImageContentType varchar(100) NULL,
  CreatedAt datetime(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_Promotions_EndsAt (EndsAt)
) CHARACTER SET utf8mb4;

INSERT IGNORE INTO Promotions (Id, Title, Description, StartsAt, EndsAt, IsActive, ImageUrl, CreatedAt)
VALUES
('e1111111-1111-1111-1111-111111111111', 'Комплекс со скидкой 15%',
 'При записи на комплексную мойку в будни — скидка 15%.',
 UTC_TIMESTAMP(6), DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 30 DAY), 1, NULL, UTC_TIMESTAMP(6)),
('e2222222-2222-2222-2222-222222222222', 'Керамика — бонусные баллы x2',
 'За керамическое покрытие начисляем двойные баллы лояльности.',
 UTC_TIMESTAMP(6), DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 45 DAY), 1, NULL, UTC_TIMESTAMP(6));

