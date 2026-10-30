CREATE TABLE "DynamicSchemaInfo"(
    "version" INTEGER NOT NULL PRIMARY KEY,
    "applied_at" TEXT NOT NULL
) STRICT;

INSERT INTO "DynamicSchemaInfo"("version","applied_at")
VALUES(3,strftime('%Y-%m-%dT%H:%M:%fZ','now'));

CREATE TABLE "DynamicRuns"(
    "run_id" TEXT NOT NULL PRIMARY KEY,
    "status" TEXT NOT NULL,
    "seed" INTEGER NOT NULL,
    "profile" TEXT NOT NULL,
    "settings_json" TEXT NOT NULL CHECK(json_valid("settings_json")),
    "app_version" TEXT NOT NULL DEFAULT '',
    "worker_version" TEXT NOT NULL DEFAULT '',
    "created_at" TEXT NOT NULL,
    "updated_at" TEXT NOT NULL,
    "started_at" TEXT NULL,
    "completed_at" TEXT NULL,
    "cancelled_at" TEXT NULL,
    "replayed_from_run_id" TEXT NULL,
    "deleted_at" TEXT NULL,
    "deleted_reason" TEXT NOT NULL DEFAULT '',
    FOREIGN KEY("replayed_from_run_id") REFERENCES "DynamicRuns"("run_id") ON DELETE SET NULL
) STRICT;

CREATE INDEX "index_run_updated_at" ON "DynamicRuns"("updated_at" DESC);
CREATE INDEX "index_run_deleted_at" ON "DynamicRuns"("deleted_at");

CREATE TABLE "DynamicRunSources"(
    "run_id" TEXT NOT NULL,
    "source_key" TEXT NOT NULL,
    "selected" INTEGER NOT NULL CHECK("selected" IN(0,1)),
    "display_order" INTEGER NOT NULL,
    PRIMARY KEY("run_id","source_key"),
    FOREIGN KEY("run_id") REFERENCES "DynamicRuns"("run_id") ON DELETE CASCADE
) STRICT;

CREATE TABLE "DynamicQueries"(
    "query_id" TEXT NOT NULL PRIMARY KEY,
    "run_id" TEXT NOT NULL,
    "ordinal" INTEGER NOT NULL,
    "path_key" TEXT NOT NULL,
    "generation_seed" INTEGER NOT NULL,
    "canonical_ir_json" TEXT NOT NULL CHECK(json_valid("canonical_ir_json")),
    "source_text" TEXT NOT NULL,
    "query_hash" TEXT NOT NULL,
    "status" TEXT NOT NULL,
    "ir_schema_version" INTEGER NOT NULL DEFAULT 1,
    "operator_count" INTEGER NOT NULL DEFAULT 0,
    "depth" INTEGER NOT NULL DEFAULT 0,
    "result_type" TEXT NOT NULL DEFAULT '',
    "order_semantics" TEXT NOT NULL DEFAULT '',
    "ir_generation_ns" INTEGER NOT NULL DEFAULT 0,
    "element_type" TEXT NOT NULL DEFAULT 'Int',
    "replayed_from_query_id" TEXT NULL,
    FOREIGN KEY("run_id") REFERENCES "DynamicRuns"("run_id") ON DELETE CASCADE,
    FOREIGN KEY("replayed_from_query_id") REFERENCES "DynamicQueries"("query_id") ON DELETE SET NULL,
    UNIQUE("run_id","ordinal")
) STRICT;

CREATE INDEX "index_query_run_path" ON "DynamicQueries"("run_id","path_key","ordinal");

CREATE TABLE "DynamicSourceExecutions"(
    "execution_id" TEXT NOT NULL PRIMARY KEY,
    "query_id" TEXT NOT NULL,
    "source_key" TEXT NOT NULL,
    "attempt" INTEGER NOT NULL,
    "execution_order" INTEGER NOT NULL,
    "status" TEXT NOT NULL,
    "capability_profile_id" TEXT NOT NULL DEFAULT '',
    "capability_profile_json" TEXT NOT NULL DEFAULT 'null' CHECK(json_valid("capability_profile_json")),
    "generated_source" TEXT NOT NULL DEFAULT '',
    "source_hash" TEXT NOT NULL DEFAULT '',
    "source_emission_ns" INTEGER NOT NULL DEFAULT 0,
    "source_text_ns" INTEGER NOT NULL DEFAULT 0,
    "syntax_tree_ns" INTEGER NOT NULL DEFAULT 0,
    "compilation_ns" INTEGER NOT NULL DEFAULT 0,
    "generator_ns" INTEGER NOT NULL DEFAULT 0,
    "emit_ns" INTEGER NOT NULL DEFAULT 0,
    "load_ns" INTEGER NOT NULL DEFAULT 0,
    "bind_ns" INTEGER NOT NULL DEFAULT 0,
    "rewrite_summary_json" TEXT NOT NULL DEFAULT 'null' CHECK(json_valid("rewrite_summary_json")),
    "fixed_point_termination" TEXT NOT NULL DEFAULT '',
    "assembly_unloaded" INTEGER NULL CHECK("assembly_unloaded" IN(0,1)),
    "loop_count" INTEGER NOT NULL DEFAULT 0,
    "warmup_count" INTEGER NOT NULL DEFAULT 0,
    "started_at" TEXT NULL,
    "completed_at" TEXT NULL,
    "result_digest" TEXT NOT NULL DEFAULT '',
    "error" TEXT NOT NULL DEFAULT '',
    "partial" INTEGER NOT NULL DEFAULT 0 CHECK("partial" IN(0,1)),
    FOREIGN KEY("query_id") REFERENCES "DynamicQueries"("query_id") ON DELETE CASCADE,
    UNIQUE("query_id","source_key","attempt")
) STRICT;

CREATE INDEX "index_source_execution_query" ON "DynamicSourceExecutions"("query_id","execution_order");

CREATE TABLE "DynamicValidationResults"(
    "validation_id" TEXT NOT NULL PRIMARY KEY,
    "execution_id" TEXT NOT NULL,
    "implementation_kind" TEXT NOT NULL,
    "status" TEXT NOT NULL,
    "result_summary" TEXT NOT NULL DEFAULT '',
    "full_result" TEXT NOT NULL CHECK(json_valid("full_result")),
    "exception_json" TEXT NOT NULL CHECK(json_valid("exception_json")),
    "symbol_validation_json" TEXT NOT NULL CHECK(json_valid("symbol_validation_json")),
    "validated_at" TEXT NOT NULL,
    FOREIGN KEY("execution_id") REFERENCES "DynamicSourceExecutions"("execution_id") ON DELETE CASCADE,
    UNIQUE("execution_id","implementation_kind")
) STRICT;

CREATE INDEX "index_validation_execution" ON "DynamicValidationResults"("execution_id","implementation_kind");

CREATE TABLE "DynamicMeasurements"(
    "measurement_id" TEXT NOT NULL PRIMARY KEY,
    "execution_id" TEXT NOT NULL,
    "implementation_kind" TEXT NOT NULL,
    "operation_count" INTEGER NOT NULL,
    "sample_count" INTEGER NOT NULL,
    "median_ns" REAL NULL,
    "mean_ns" REAL NULL,
    "min_ns" REAL NULL,
    "max_ns" REAL NULL,
    "standard_deviation_ns" REAL NULL,
    "standard_error_ns" REAL NULL,
    "allocated_bytes_per_operation" REAL NULL,
    "gen0_collections" REAL NULL,
    "gen1_collections" REAL NULL,
    "gen2_collections" REAL NULL,
    "started_at" TEXT NULL,
    "completed_at" TEXT NULL,
    FOREIGN KEY("execution_id") REFERENCES "DynamicSourceExecutions"("execution_id") ON DELETE CASCADE,
    UNIQUE("execution_id","implementation_kind")
) STRICT;

CREATE TABLE "DynamicMeasurementSamples"(
    "measurement_id" TEXT NOT NULL,
    "phase" TEXT NOT NULL,
    "sample_index" INTEGER NOT NULL,
    "elapsed_ns" REAL NOT NULL,
    "operation_count" INTEGER NOT NULL,
    "allocated_bytes" INTEGER NOT NULL,
    "gen0_collections" INTEGER NOT NULL,
    "gen1_collections" INTEGER NOT NULL,
    "gen2_collections" INTEGER NOT NULL,
    "recorded_at" TEXT NOT NULL,
    PRIMARY KEY("measurement_id","phase","sample_index"),
    FOREIGN KEY("measurement_id") REFERENCES "DynamicMeasurements"("measurement_id") ON DELETE CASCADE
) STRICT;

CREATE TABLE "DynamicDiagnostics"(
    "diagnostic_id" TEXT NOT NULL PRIMARY KEY,
    "execution_id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "numeric_value" REAL NULL,
    "text_value" TEXT NULL,
    "unit" TEXT NOT NULL DEFAULT '',
    "details_json" TEXT NOT NULL CHECK(json_valid("details_json")),
    "recorded_at" TEXT NOT NULL,
    FOREIGN KEY("execution_id") REFERENCES "DynamicSourceExecutions"("execution_id") ON DELETE CASCADE
) STRICT;

CREATE INDEX "index_diagnostic_execution" ON "DynamicDiagnostics"("execution_id","name");

CREATE TABLE "DynamicOptimizationEvents"(
    "optimization_event_id" TEXT NOT NULL PRIMARY KEY,
    "execution_id" TEXT NOT NULL,
    "query_id" TEXT NOT NULL,
    "source_key" TEXT NOT NULL,
    "sequence" INTEGER NOT NULL,
    "kind" TEXT NOT NULL,
    "layer" TEXT NOT NULL DEFAULT '',
    "phase" TEXT NOT NULL DEFAULT '',
    "rule_id" TEXT NOT NULL DEFAULT '',
    "round" INTEGER NOT NULL DEFAULT 0,
    "pass" INTEGER NOT NULL DEFAULT 0,
    "visit" INTEGER NOT NULL DEFAULT 0,
    "node_id" TEXT NOT NULL DEFAULT '',
    "node_path" TEXT NOT NULL DEFAULT '',
    "depth" INTEGER NULL,
    "stage_index" INTEGER NULL,
    "parent_kind" TEXT NOT NULL DEFAULT '',
    "is_left" INTEGER NOT NULL DEFAULT 0 CHECK("is_left" IN(0,1)),
    "is_right" INTEGER NOT NULL DEFAULT 0 CHECK("is_right" IN(0,1)),
    "is_inner" INTEGER NOT NULL DEFAULT 0 CHECK("is_inner" IN(0,1)),
    "before_text" TEXT NOT NULL DEFAULT '',
    "after_text" TEXT NOT NULL DEFAULT '',
    "changed" INTEGER NOT NULL DEFAULT 0 CHECK("changed" IN(0,1)),
    "revisited" INTEGER NOT NULL DEFAULT 0 CHECK("revisited" IN(0,1)),
    "details_json" TEXT NOT NULL CHECK(json_valid("details_json")),
    "recorded_at" TEXT NOT NULL,
    FOREIGN KEY("execution_id") REFERENCES "DynamicSourceExecutions"("execution_id") ON DELETE CASCADE,
    FOREIGN KEY("query_id") REFERENCES "DynamicQueries"("query_id") ON DELETE CASCADE,
    UNIQUE("execution_id","sequence")
) STRICT;

CREATE INDEX "index_optimization_execution" ON "DynamicOptimizationEvents"("execution_id","layer","round","sequence");

CREATE TABLE "DynamicRunEvents"(
    "run_event_id" TEXT NOT NULL PRIMARY KEY,
    "run_id" TEXT NOT NULL,
    "query_id" TEXT NULL,
    "execution_id" TEXT NULL,
    "sequence" INTEGER NOT NULL,
    "level" TEXT NOT NULL,
    "kind" TEXT NOT NULL,
    "message" TEXT NOT NULL,
    "details_json" TEXT NOT NULL CHECK(json_valid("details_json")),
    "recorded_at" TEXT NOT NULL,
    FOREIGN KEY("run_id") REFERENCES "DynamicRuns"("run_id") ON DELETE CASCADE,
    FOREIGN KEY("query_id") REFERENCES "DynamicQueries"("query_id") ON DELETE CASCADE,
    FOREIGN KEY("execution_id") REFERENCES "DynamicSourceExecutions"("execution_id") ON DELETE CASCADE,
    UNIQUE("run_id","sequence")
) STRICT;

CREATE INDEX "index_run_event_run" ON "DynamicRunEvents"("run_id","sequence");

PRAGMA user_version=3;

CREATE TABLE BenchmarkSchema(version INTEGER NOT NULL PRIMARY KEY,applied_at TEXT NOT NULL) STRICT;
INSERT INTO BenchmarkSchema VALUES(4,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
CREATE TABLE BenchmarkRuns(id TEXT NOT NULL PRIMARY KEY,application TEXT NOT NULL,status TEXT NOT NULL,created_at TEXT NOT NULL,payload TEXT NOT NULL CHECK(json_valid(payload))) STRICT;
CREATE TABLE BenchmarkMeasurements(id TEXT NOT NULL PRIMARY KEY,run_id TEXT NOT NULL,scope_key TEXT NOT NULL,interval INTEGER NOT NULL,status TEXT NOT NULL,payload TEXT NOT NULL CHECK(json_valid(payload)),FOREIGN KEY(run_id) REFERENCES BenchmarkRuns(id) ON DELETE CASCADE) STRICT;
CREATE INDEX index_benchmark_measurement_scope ON BenchmarkMeasurements(scope_key,run_id);
CREATE TABLE BenchmarkSamples(measurement_id TEXT NOT NULL,sample_index INTEGER NOT NULL,elapsed_ns REAL NOT NULL,operations INTEGER NOT NULL,allocated_bytes INTEGER NOT NULL,payload TEXT NOT NULL CHECK(json_valid(payload)),PRIMARY KEY(measurement_id,sample_index),FOREIGN KEY(measurement_id) REFERENCES BenchmarkMeasurements(id) ON DELETE CASCADE) STRICT;
CREATE TABLE BenchmarkChanges(id TEXT NOT NULL PRIMARY KEY,run_id TEXT NOT NULL,interval INTEGER NOT NULL,payload TEXT NOT NULL CHECK(json_valid(payload)),FOREIGN KEY(run_id) REFERENCES BenchmarkRuns(id) ON DELETE CASCADE) STRICT;
CREATE TABLE BenchmarkBaselines(scope_key TEXT NOT NULL PRIMARY KEY,measurement_id TEXT NOT NULL,selected_at TEXT NOT NULL,FOREIGN KEY(measurement_id) REFERENCES BenchmarkMeasurements(id)) STRICT;
CREATE TABLE BenchmarkComparisons(id TEXT NOT NULL PRIMARY KEY,baseline_id TEXT NOT NULL,candidate_id TEXT NOT NULL,payload TEXT NOT NULL CHECK(json_valid(payload)),FOREIGN KEY(baseline_id) REFERENCES BenchmarkMeasurements(id),FOREIGN KEY(candidate_id) REFERENCES BenchmarkMeasurements(id)) STRICT;
CREATE TABLE BenchmarkProfiles(id TEXT NOT NULL PRIMARY KEY,created_at TEXT NOT NULL,payload TEXT NOT NULL CHECK(json_valid(payload))) STRICT;
CREATE TABLE BenchmarkImports(source TEXT NOT NULL PRIMARY KEY,imported_at TEXT NOT NULL,backup_path TEXT NOT NULL) STRICT;
UPDATE DynamicSchemaInfo SET version=4;
PRAGMA user_version=4;
