-- ========================================
-- ThreeFlowPlatform - ApplicationProcessor Database Schema
-- ========================================

-- Создание схемы processor
CREATE SCHEMA IF NOT EXISTS processor;

-- Таблица для хранения заявок
CREATE TABLE IF NOT EXISTS processor.applications
(
  id           UUID PRIMARY KEY,
  weight       INT         NOT NULL CHECK (weight >= 0 AND weight <= 100),
  data         TEXT        NOT NULL,
  files        TEXT[],
  channel      VARCHAR(50) NOT NULL,
  created_at   TIMESTAMP   NOT NULL DEFAULT NOW(),
  processed_at TIMESTAMP,
  status       VARCHAR(50),
  error_message TEXT
);

-- Индекс для быстрого поиска по ID
CREATE INDEX IF NOT EXISTS idx_applications_id ON processor.applications (id);

-- Индекс для поиска по статусу
CREATE INDEX IF NOT EXISTS idx_applications_status ON processor.applications (status);

-- Индекс для поиска по дате создания
CREATE INDEX IF NOT EXISTS idx_applications_created_at ON processor.applications (created_at DESC);

-- ========================================
-- Функция: f_insert_application
-- Назначение: Вставка новой заявки в БД
-- ========================================
CREATE OR REPLACE FUNCTION processor.f_insert_application(
  p_id UUID,
  p_weight INT,
  p_data TEXT,
  p_files TEXT[],
  p_channel VARCHAR(50),
  p_created_at TIMESTAMP
)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO processor.applications (id, weight, data, files, channel, created_at, status)
  VALUES (p_id, p_weight, p_data, p_files, p_channel, p_created_at, 'pending');

  RETURN TRUE;
EXCEPTION
  WHEN OTHERS THEN
    RAISE WARNING 'Error inserting application: %', SQLERRM;
    RETURN FALSE;
END;
$$;

-- ========================================
-- Функция: f_update_application_status
-- Назначение: Обновление статуса заявки
-- ========================================
CREATE OR REPLACE FUNCTION processor.f_update_application_status(
  p_id UUID,
  p_status VARCHAR(50),
  p_error_message TEXT,
  p_processed_at TIMESTAMP
)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
  UPDATE processor.applications
  SET status = p_status,
      error_message = p_error_message,
      processed_at = p_processed_at
  WHERE id = p_id;

  RETURN FOUND;
EXCEPTION
  WHEN OTHERS THEN
    RAISE WARNING 'Error updating application status: %', SQLERRM;
    RETURN FALSE;
END;
$$;

-- ========================================
-- Функция: f_get_application_by_id
-- Назначение: Получение заявки по ID
-- ========================================
CREATE OR REPLACE FUNCTION processor.f_get_application_by_id(
  p_id UUID
)
RETURNS TABLE (
  id UUID,
  weight INT,
  data TEXT,
  files TEXT[],
  channel VARCHAR(50),
  created_at TIMESTAMP,
  processed_at TIMESTAMP,
  status VARCHAR(50),
  error_message TEXT
)
LANGUAGE plpgsql
AS $$
BEGIN
  RETURN QUERY
  SELECT a.id, a.weight, a.data, a.files, a.channel, a.created_at, a.processed_at, a.status, a.error_message
  FROM processor.applications a
  WHERE a.id = p_id;
END;
$$;

-- ========================================
-- Функция: f_save_statistics
-- Назначение: Сохранение статистики обработки
-- ========================================
-- Таблица для статистики (опционально)
CREATE TABLE IF NOT EXISTS processor.statistics
(
  id               SERIAL PRIMARY KEY,
  total_processed  BIGINT    NOT NULL,
  total_failed     BIGINT    NOT NULL,
  total_validation_errors BIGINT NOT NULL,
  channel_priority BIGINT DEFAULT 0,
  channel_main     BIGINT DEFAULT 0,
  channel_secondary BIGINT DEFAULT 0,
  saved_at         TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION processor.f_save_statistics(
  p_total_processed BIGINT,
  p_total_failed BIGINT,
  p_total_validation_errors BIGINT,
  p_channel_priority BIGINT,
  p_channel_main BIGINT,
  p_channel_secondary BIGINT
)
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
  INSERT INTO processor.statistics (total_processed, total_failed, total_validation_errors, channel_priority, channel_main, channel_secondary)
  VALUES (p_total_processed, p_total_failed, p_total_validation_errors, p_channel_priority, p_channel_main, p_channel_secondary);

  RETURN TRUE;
EXCEPTION
  WHEN OTHERS THEN
    RAISE WARNING 'Error saving statistics: %', SQLERRM;
    RETURN FALSE;
END;
$$;

-- ========================================
-- Тестовые данные (опционально)
-- ========================================

-- Вставка тестовой заявки
-- SELECT processor.f_insert_application(
--   gen_random_uuid(),
--   75,
--   'Тестовая заявка',
--   ARRAY['file1.pdf', 'file2.docx'],
--   'Main',
--   NOW()
-- );
