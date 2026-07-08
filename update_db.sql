IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;


BEGIN TRANSACTION;


CREATE TABLE [Keycaps] (
    [KeycapId] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Brand] nvarchar(100) NULL,
    [Profile] nvarchar(50) NULL,
    [Material] nvarchar(50) NULL,
    [ImageUrl] nvarchar(500) NULL,
    CONSTRAINT [PK_Keycaps] PRIMARY KEY ([KeycapId])
);


CREATE TABLE [Kits] (
    [KitId] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Brand] nvarchar(100) NULL,
    [Layout] nvarchar(50) NULL,
    [MountType] nvarchar(50) NULL,
    [PcbType] nvarchar(50) NULL,
    [ImageUrl] nvarchar(500) NULL,
    CONSTRAINT [PK_Kits] PRIMARY KEY ([KitId])
);


CREATE TABLE [Switches] (
    [SwitchId] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Brand] nvarchar(100) NULL,
    [Type] nvarchar(20) NULL,
    [ActuationForce] nvarchar(20) NULL,
    CONSTRAINT [PK_Switches] PRIMARY KEY ([SwitchId])
);


CREATE TABLE [Users] (
    [UserId] uniqueidentifier NOT NULL,
    [Username] nvarchar(50) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
);


CREATE TABLE [Specs] (
    [SpecId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [KitId] int NOT NULL,
    [SwitchId] int NOT NULL,
    [KeycapId] int NULL,
    [BuildName] nvarchar(150) NOT NULL,
    [PlateMaterial] nvarchar(100) NULL,
    [FoamSetup] nvarchar(200) NULL,
    [Mods] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_Specs] PRIMARY KEY ([SpecId]),
    CONSTRAINT [FK_Specs_Keycaps_KeycapId] FOREIGN KEY ([KeycapId]) REFERENCES [Keycaps] ([KeycapId]) ON DELETE SET NULL,
    CONSTRAINT [FK_Specs_Kits_KitId] FOREIGN KEY ([KitId]) REFERENCES [Kits] ([KitId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Specs_Switches_SwitchId] FOREIGN KEY ([SwitchId]) REFERENCES [Switches] ([SwitchId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Specs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);


CREATE TABLE [SoundTests] (
    [TestId] uniqueidentifier NOT NULL,
    [SpecId] uniqueidentifier NOT NULL,
    [MicUsed] nvarchar(100) NULL,
    [AudioUrl] nvarchar(1000) NOT NULL,
    [Upvotes] int NOT NULL DEFAULT 0,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT [PK_SoundTests] PRIMARY KEY ([TestId]),
    CONSTRAINT [FK_SoundTests_Specs_SpecId] FOREIGN KEY ([SpecId]) REFERENCES [Specs] ([SpecId]) ON DELETE CASCADE
);


CREATE INDEX [IX_SoundTests_SpecId] ON [SoundTests] ([SpecId]);


CREATE INDEX [IX_Specs_KeycapId] ON [Specs] ([KeycapId]);


CREATE INDEX [IX_Specs_KitId] ON [Specs] ([KitId]);


CREATE INDEX [IX_Specs_SwitchId] ON [Specs] ([SwitchId]);


CREATE INDEX [IX_Specs_UserId] ON [Specs] ([UserId]);


CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);


CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260413232710_InitialCreate', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


ALTER TABLE [Users] ADD [Role] nvarchar(20) NOT NULL DEFAULT N'';


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260429133005_AddUserRole', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


ALTER TABLE [Specs] DROP CONSTRAINT [FK_Specs_Switches_SwitchId];


DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Specs]') AND [c].[name] = N'SwitchId');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Specs] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Specs] ALTER COLUMN [SwitchId] int NULL;


ALTER TABLE [Specs] ADD [CustomSwitchName] nvarchar(100) NULL;


ALTER TABLE [Specs] ADD CONSTRAINT [FK_Specs_Switches_SwitchId] FOREIGN KEY ([SwitchId]) REFERENCES [Switches] ([SwitchId]) ON DELETE SET NULL;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260501151839_AddCustomSwitchToSpec', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


ALTER TABLE [Keycaps] ADD [Description] nvarchar(500) NULL;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260503093036_AddKeycapDescription', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


ALTER TABLE [Switches] ADD [ImageUrl] nvarchar(500) NULL;


ALTER TABLE [Switches] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);


ALTER TABLE [Kits] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);


ALTER TABLE [Keycaps] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260503193029_UpdateSoftDeleteAndSwitchImage', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


ALTER TABLE [Users] ADD [IsEmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);


ALTER TABLE [Users] ADD [VerificationToken] nvarchar(max) NULL;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260607223741_AddEmailVerification', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


CREATE TABLE [SoundTestComments] (
    [Id] int NOT NULL IDENTITY,
    [Content] nvarchar(1000) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
    [UserId] uniqueidentifier NOT NULL,
    [SoundTestId] uniqueidentifier NOT NULL,
    [ParentCommentId] int NULL,
    CONSTRAINT [PK_SoundTestComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SoundTestComments_SoundTestComments_ParentCommentId] FOREIGN KEY ([ParentCommentId]) REFERENCES [SoundTestComments] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SoundTestComments_SoundTests_SoundTestId] FOREIGN KEY ([SoundTestId]) REFERENCES [SoundTests] ([TestId]) ON DELETE CASCADE,
    CONSTRAINT [FK_SoundTestComments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE NO ACTION
);


CREATE TABLE [SoundTestLikes] (
    [Id] int NOT NULL IDENTITY,
    [UserId] uniqueidentifier NOT NULL,
    [SoundTestId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SoundTestLikes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SoundTestLikes_SoundTests_SoundTestId] FOREIGN KEY ([SoundTestId]) REFERENCES [SoundTests] ([TestId]) ON DELETE CASCADE,
    CONSTRAINT [FK_SoundTestLikes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
);


CREATE INDEX [IX_SoundTestComments_ParentCommentId] ON [SoundTestComments] ([ParentCommentId]);


CREATE INDEX [IX_SoundTestComments_SoundTestId] ON [SoundTestComments] ([SoundTestId]);


CREATE INDEX [IX_SoundTestComments_UserId] ON [SoundTestComments] ([UserId]);


CREATE INDEX [IX_SoundTestLikes_SoundTestId] ON [SoundTestLikes] ([SoundTestId]);


CREATE UNIQUE INDEX [IX_SoundTestLikes_UserId_SoundTestId] ON [SoundTestLikes] ([UserId], [SoundTestId]);


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260621093148_AddSoundTestLikeAndComment', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


CREATE TABLE [KitImages] (
    [Id] int NOT NULL IDENTITY,
    [KitId] int NOT NULL,
    [ImageUrl] nvarchar(500) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_KitImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_KitImages_Kits_KitId] FOREIGN KEY ([KitId]) REFERENCES [Kits] ([KitId]) ON DELETE CASCADE
);


CREATE INDEX [IX_KitImages_KitId_SortOrder] ON [KitImages] ([KitId], [SortOrder]);


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260629184434_AddKitImagesTable', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


ALTER TABLE [KitImages] ADD [ColorHex] nvarchar(7) NULL;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702184054_AddKitImageColorHex', N'8.0.6');


COMMIT;


BEGIN TRANSACTION;


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260702221023_MakeColorHexNullable', N'8.0.6');


COMMIT;


