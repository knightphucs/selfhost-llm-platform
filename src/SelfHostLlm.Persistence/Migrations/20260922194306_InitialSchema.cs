using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SelfHostLlm.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissions = table.Column<List<string>>(type: "text[]", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claim_role_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    username = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                    table.ForeignKey(
                        name: "fk_app_user_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consumer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumer", x => x.id);
                    table.UniqueConstraint("ak_consumer_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_consumer_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dataset",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    storage_uri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    record_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dataset", x => x.id);
                    table.UniqueConstraint("ak_dataset_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_dataset_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "model",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    param_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantization = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    context_length = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capabilities = table.Column<string[]>(type: "text[]", nullable: false),
                    task_tags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_model", x => x.id);
                    table.UniqueConstraint("ak_model_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_model_capabilities", "capabilities <@ ARRAY['Chat', 'Embedding', 'Vision']::text[]");
                    table.ForeignKey(
                        name: "fk_model_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_provider", x => x.id);
                    table.UniqueConstraint("ak_provider_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_provider_kind", "kind IN ('Ollama', 'Vllm', 'Tgi', 'LlamaCpp', 'Mlx', 'OpenAiCompatible')");
                    table.ForeignKey(
                        name: "fk_provider_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "virtual_model",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    task = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_virtual_model", x => x.id);
                    table.UniqueConstraint("ak_virtual_model_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_virtual_model_task", "task IN ('Chat', 'Coding', 'Embedding', 'Summarization', 'Classification')");
                    table.ForeignKey(
                        name: "fk_virtual_model_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    before_value = table.Column<string>(type: "jsonb", nullable: true),
                    after_value = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.CheckConstraint("ck_audit_log_action", "action IN ('Create', 'Update', 'Delete', 'Revoke', 'Enable', 'Disable', 'AssignRole', 'Login')");
                    table.ForeignKey(
                        name: "fk_audit_log_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_log_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claim_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_login",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_login", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_login_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_role_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_role_role_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_token",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_token", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_token_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_key",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_key", x => x.id);
                    table.UniqueConstraint("ak_api_key_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_api_key_consumer_id",
                        columns: x => new { x.tenant_id, x.consumer_id },
                        principalTable: "consumer",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_api_key_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quota",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tokens_per_minute = table.Column<int>(type: "integer", nullable: true),
                    tokens_per_month = table.Column<long>(type: "bigint", nullable: true),
                    max_concurrent_requests = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quota", x => x.id);
                    table.ForeignKey(
                        name: "fk_quota_consumer_id",
                        columns: x => new { x.tenant_id, x.consumer_id },
                        principalTable: "consumer",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quota_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usage_aggregate",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    bucket_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    prompt_tokens = table.Column<long>(type: "bigint", nullable: false),
                    completion_tokens = table.Column<long>(type: "bigint", nullable: false),
                    request_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usage_aggregate", x => x.id);
                    table.CheckConstraint("ck_usage_aggregate_period", "period IN ('Hour', 'Day', 'Month')");
                    table.ForeignKey(
                        name: "fk_usage_aggregate_consumer_id",
                        columns: x => new { x.tenant_id, x.consumer_id },
                        principalTable: "consumer",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usage_aggregate_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "collection",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding_dim = table.Column<int>(type: "integer", nullable: false),
                    chunk_size = table.Column<int>(type: "integer", nullable: false),
                    chunk_overlap = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection", x => x.id);
                    table.UniqueConstraint("ak_collection_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_collection_chunk_overlap", "chunk_overlap >= 0 AND chunk_overlap < chunk_size");
                    table.CheckConstraint("ck_collection_embedding_dim", "embedding_dim = 1024");
                    table.ForeignKey(
                        name: "fk_collection_embedding_model_id",
                        columns: x => new { x.tenant_id, x.embedding_model_id },
                        principalTable: "model",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_collection_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_job",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    log_uri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hyperparameters = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_job", x => x.id);
                    table.UniqueConstraint("ak_training_job_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_training_job_method", "method IN ('LoRA', 'QLoRA')");
                    table.CheckConstraint("ck_training_job_status", "status IN ('Queued', 'Running', 'Succeeded', 'Failed', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_training_job_base_model_id",
                        columns: x => new { x.tenant_id, x.base_model_id },
                        principalTable: "model",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_job_dataset_id",
                        columns: x => new { x.tenant_id, x.dataset_id },
                        principalTable: "dataset",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_training_job_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_uri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ingested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document", x => x.id);
                    table.UniqueConstraint("ak_document_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_document_collection_id",
                        columns: x => new { x.tenant_id, x.collection_id },
                        principalTable: "collection",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_document_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "model_version",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    adapter_uri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    training_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    eval_metrics = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_model_version", x => x.id);
                    table.UniqueConstraint("ak_model_version_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_model_version_model_id",
                        columns: x => new { x.tenant_id, x.model_id },
                        principalTable: "model",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_model_version_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_model_version_training_job_id",
                        columns: x => new { x.tenant_id, x.training_job_id },
                        principalTable: "training_job",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chunk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chunk", x => x.id);
                    table.ForeignKey(
                        name: "fk_chunk_collection_id",
                        columns: x => new { x.tenant_id, x.collection_id },
                        principalTable: "collection",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chunk_document_id",
                        columns: x => new { x.tenant_id, x.document_id },
                        principalTable: "document",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chunk_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deployment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: true),
                    remote_model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    health_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    latency_ms_p50 = table.Column<int>(type: "integer", nullable: true),
                    last_probed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deployment", x => x.id);
                    table.UniqueConstraint("ak_deployment_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_deployment_health_status", "health_status IN ('Unknown', 'Healthy', 'Unhealthy')");
                    table.ForeignKey(
                        name: "fk_deployment_model_id",
                        columns: x => new { x.tenant_id, x.model_id },
                        principalTable: "model",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deployment_model_version_id",
                        columns: x => new { x.tenant_id, x.model_version_id },
                        principalTable: "model_version",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deployment_provider_id",
                        columns: x => new { x.tenant_id, x.provider_id },
                        principalTable: "provider",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deployment_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    virtual_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deployment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_deployment_id",
                        columns: x => new { x.tenant_id, x.deployment_id },
                        principalTable: "deployment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_route_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_route_virtual_model_id",
                        columns: x => new { x.tenant_id, x.virtual_model_id },
                        principalTable: "virtual_model",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usage_record",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deployment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    task = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false),
                    tokens_estimated = table.Column<bool>(type: "boolean", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    used_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usage_record", x => x.id);
                    table.CheckConstraint("ck_usage_record_task", "task IN ('Chat', 'Coding', 'Embedding', 'Summarization', 'Classification')");
                    table.ForeignKey(
                        name: "fk_usage_record_api_key_id",
                        columns: x => new { x.tenant_id, x.api_key_id },
                        principalTable: "api_key",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usage_record_deployment_id",
                        columns: x => new { x.tenant_id, x.deployment_id },
                        principalTable: "deployment",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_usage_record_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "role",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name", "permissions" },
                values: new object[,]
                {
                    { new Guid("0b1d0f7e-7a51-4c3e-9f0a-000000000001"), "0b1d0f7e-7a51-4c3e-9f0a-000000000001", "PlatformAdmin", "PLATFORMADMIN", new List<string> { "apikeys:manage", "audit:read", "consumers:read", "consumers:write", "deployments:read", "deployments:write", "models:read", "models:write", "providers:read", "providers:write", "quotas:read", "quotas:write", "rag:read", "rag:write", "rbac:manage", "routes:read", "routes:write", "tenants:manage", "training:read", "training:write", "usage:read" } },
                    { new Guid("0b1d0f7e-7a51-4c3e-9f0a-000000000002"), "0b1d0f7e-7a51-4c3e-9f0a-000000000002", "TenantAdmin", "TENANTADMIN", new List<string> { "apikeys:manage", "audit:read", "consumers:read", "consumers:write", "deployments:read", "deployments:write", "models:read", "models:write", "providers:read", "providers:write", "quotas:read", "quotas:write", "rag:read", "rag:write", "rbac:manage", "routes:read", "routes:write", "training:read", "training:write", "usage:read" } },
                    { new Guid("0b1d0f7e-7a51-4c3e-9f0a-000000000003"), "0b1d0f7e-7a51-4c3e-9f0a-000000000003", "Operator", "OPERATOR", new List<string> { "consumers:read", "deployments:read", "deployments:write", "models:read", "models:write", "providers:read", "providers:write", "quotas:read", "rag:read", "routes:read", "routes:write", "training:read", "usage:read" } },
                    { new Guid("0b1d0f7e-7a51-4c3e-9f0a-000000000004"), "0b1d0f7e-7a51-4c3e-9f0a-000000000004", "Viewer", "VIEWER", new List<string> { "audit:read", "consumers:read", "deployments:read", "models:read", "providers:read", "quotas:read", "rag:read", "routes:read", "training:read", "usage:read" } }
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_key_hash",
                table: "api_key",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_key_tenant_id_consumer_id",
                table: "api_key",
                columns: new[] { "tenant_id", "consumer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_normalized_email",
                table: "app_user",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_app_user_normalized_user_name",
                table: "app_user",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_user_tenant_id",
                table: "app_user",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor_user_id",
                table: "audit_log",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tenant_id_occurred_at",
                table: "audit_log",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_chunk_document_id_ordinal",
                table: "chunk",
                columns: new[] { "document_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chunk_tenant_id_collection_id",
                table: "chunk",
                columns: new[] { "tenant_id", "collection_id" });

            migrationBuilder.CreateIndex(
                name: "ix_chunk_tenant_id_document_id",
                table: "chunk",
                columns: new[] { "tenant_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_collection_tenant_id_embedding_model_id",
                table: "collection",
                columns: new[] { "tenant_id", "embedding_model_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deployment_provider_id_base_url_remote_model_name",
                table: "deployment",
                columns: new[] { "provider_id", "base_url", "remote_model_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deployment_tenant_id_model_id",
                table: "deployment",
                columns: new[] { "tenant_id", "model_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deployment_tenant_id_model_version_id",
                table: "deployment",
                columns: new[] { "tenant_id", "model_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deployment_tenant_id_provider_id",
                table: "deployment",
                columns: new[] { "tenant_id", "provider_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_tenant_id_collection_id_content_hash",
                table: "document",
                columns: new[] { "tenant_id", "collection_id", "content_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_model_tenant_id_name",
                table: "model",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_model_version_model_id_version_tag",
                table: "model_version",
                columns: new[] { "model_id", "version_tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_model_version_tenant_id_model_id",
                table: "model_version",
                columns: new[] { "tenant_id", "model_id" });

            migrationBuilder.CreateIndex(
                name: "ix_model_version_tenant_id_training_job_id",
                table: "model_version",
                columns: new[] { "tenant_id", "training_job_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quota_consumer_id",
                table: "quota",
                column: "consumer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quota_tenant_id_consumer_id",
                table: "quota",
                columns: new[] { "tenant_id", "consumer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_normalized_name",
                table: "role",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_claim_role_id",
                table: "role_claim",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_tenant_id_deployment_id",
                table: "route",
                columns: new[] { "tenant_id", "deployment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_route_tenant_id_virtual_model_id",
                table: "route",
                columns: new[] { "tenant_id", "virtual_model_id" });

            migrationBuilder.CreateIndex(
                name: "ix_route_virtual_model_id_deployment_id",
                table: "route",
                columns: new[] { "virtual_model_id", "deployment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_route_virtual_model_id_priority",
                table: "route",
                columns: new[] { "virtual_model_id", "priority" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_slug",
                table: "tenant",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_training_job_tenant_id_base_model_id",
                table: "training_job",
                columns: new[] { "tenant_id", "base_model_id" });

            migrationBuilder.CreateIndex(
                name: "ix_training_job_tenant_id_dataset_id",
                table: "training_job",
                columns: new[] { "tenant_id", "dataset_id" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_aggregate_tenant_id_consumer_id_period_bucket_start",
                table: "usage_aggregate",
                columns: new[] { "tenant_id", "consumer_id", "period", "bucket_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usage_record_tenant_id_api_key_id",
                table: "usage_record",
                columns: new[] { "tenant_id", "api_key_id" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_record_tenant_id_deployment_id",
                table: "usage_record",
                columns: new[] { "tenant_id", "deployment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_record_tenant_id_occurred_at",
                table: "usage_record",
                columns: new[] { "tenant_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_claim_user_id",
                table: "user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_user_id",
                table: "user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_role_id",
                table: "user_role",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_virtual_model_tenant_id_name",
                table: "virtual_model",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // ---- SQL viết tay (không sinh bằng EF) ----

            // QĐ-1: index HNSW cho truy vấn cosine. Truy vấn phải dùng toán tử <=> mới dùng được index này.
            migrationBuilder.Sql(
                "CREATE INDEX ix_chunk_embedding_hnsw ON chunk USING hnsw (embedding vector_cosine_ops);");

            // Audit log append-only ở tầng DB: chặn UPDATE / DELETE / TRUNCATE kể cả khi đi vòng qua API.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_log_reject_mutation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_log là append-only: không cho phép %', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                CREATE TRIGGER trg_audit_log_append_only
                    BEFORE UPDATE OR DELETE ON audit_log
                    FOR EACH ROW EXECUTE FUNCTION audit_log_reject_mutation();

                CREATE TRIGGER trg_audit_log_no_truncate
                    BEFORE TRUNCATE ON audit_log
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_log_reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_log_no_truncate ON audit_log;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_audit_log_append_only ON audit_log;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_log_reject_mutation();");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_chunk_embedding_hnsw;");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "chunk");

            migrationBuilder.DropTable(
                name: "quota");

            migrationBuilder.DropTable(
                name: "role_claim");

            migrationBuilder.DropTable(
                name: "route");

            migrationBuilder.DropTable(
                name: "usage_aggregate");

            migrationBuilder.DropTable(
                name: "usage_record");

            migrationBuilder.DropTable(
                name: "user_claim");

            migrationBuilder.DropTable(
                name: "user_login");

            migrationBuilder.DropTable(
                name: "user_role");

            migrationBuilder.DropTable(
                name: "user_token");

            migrationBuilder.DropTable(
                name: "document");

            migrationBuilder.DropTable(
                name: "virtual_model");

            migrationBuilder.DropTable(
                name: "api_key");

            migrationBuilder.DropTable(
                name: "deployment");

            migrationBuilder.DropTable(
                name: "role");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "collection");

            migrationBuilder.DropTable(
                name: "consumer");

            migrationBuilder.DropTable(
                name: "model_version");

            migrationBuilder.DropTable(
                name: "provider");

            migrationBuilder.DropTable(
                name: "training_job");

            migrationBuilder.DropTable(
                name: "model");

            migrationBuilder.DropTable(
                name: "dataset");

            migrationBuilder.DropTable(
                name: "tenant");
        }
    }
}
