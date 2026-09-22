-- Bật pgvector ngay khi khởi tạo DB. Migration EF Core vẫn khai báo lại
-- HasPostgresExtension("vector") để schema tự mô tả đầy đủ.
CREATE EXTENSION IF NOT EXISTS vector;
