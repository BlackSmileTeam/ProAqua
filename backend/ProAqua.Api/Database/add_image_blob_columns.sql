-- Add image BLOB columns (run once if seed fails with Unknown column ImageData)
ALTER TABLE Services ADD COLUMN ImageData longblob NULL;
ALTER TABLE Services ADD COLUMN ImageContentType varchar(100) NULL;
ALTER TABLE Promotions ADD COLUMN ImageData longblob NULL;
ALTER TABLE Promotions ADD COLUMN ImageContentType varchar(100) NULL;
