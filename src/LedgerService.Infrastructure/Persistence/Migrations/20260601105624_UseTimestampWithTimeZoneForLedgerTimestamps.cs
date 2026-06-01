using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseTimestampWithTimeZoneForLedgerTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE ledger_entries
                    ALTER COLUMN occurred_at TYPE timestamp with time zone USING occurred_at AT TIME ZONE 'UTC',
                    ALTER COLUMN created_at TYPE timestamp with time zone USING created_at AT TIME ZONE 'UTC';

                ALTER TABLE idempotency_records
                    ALTER COLUMN created_at TYPE timestamp with time zone USING created_at AT TIME ZONE 'UTC',
                    ALTER COLUMN expires_at TYPE timestamp with time zone USING expires_at AT TIME ZONE 'UTC';

                ALTER TABLE outbox_messages
                    ALTER COLUMN occurred_at TYPE timestamp with time zone USING occurred_at AT TIME ZONE 'UTC',
                    ALTER COLUMN next_retry_at TYPE timestamp with time zone USING next_retry_at AT TIME ZONE 'UTC',
                    ALTER COLUMN processed_at TYPE timestamp with time zone USING processed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN locked_until TYPE timestamp with time zone USING locked_until AT TIME ZONE 'UTC',
                    ALTER COLUMN last_requeued_at TYPE timestamp with time zone USING last_requeued_at AT TIME ZONE 'UTC';

                ALTER TABLE estornos_lancamentos
                    ALTER COLUMN created_at TYPE timestamp with time zone USING created_at AT TIME ZONE 'UTC',
                    ALTER COLUMN processing_started_at TYPE timestamp with time zone USING processing_started_at AT TIME ZONE 'UTC',
                    ALTER COLUMN completed_at TYPE timestamp with time zone USING completed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN rejected_at TYPE timestamp with time zone USING rejected_at AT TIME ZONE 'UTC',
                    ALTER COLUMN failed_at TYPE timestamp with time zone USING failed_at AT TIME ZONE 'UTC';

                ALTER TABLE reprocessamentos_lancamentos
                    ALTER COLUMN created_at TYPE timestamp with time zone USING created_at AT TIME ZONE 'UTC',
                    ALTER COLUMN processing_started_at TYPE timestamp with time zone USING processing_started_at AT TIME ZONE 'UTC',
                    ALTER COLUMN completed_at TYPE timestamp with time zone USING completed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN failed_at TYPE timestamp with time zone USING failed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN rejected_at TYPE timestamp with time zone USING rejected_at AT TIME ZONE 'UTC';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE ledger_entries
                    ALTER COLUMN occurred_at TYPE timestamp without time zone USING occurred_at AT TIME ZONE 'UTC',
                    ALTER COLUMN created_at TYPE timestamp without time zone USING created_at AT TIME ZONE 'UTC';

                ALTER TABLE idempotency_records
                    ALTER COLUMN created_at TYPE timestamp without time zone USING created_at AT TIME ZONE 'UTC',
                    ALTER COLUMN expires_at TYPE timestamp without time zone USING expires_at AT TIME ZONE 'UTC';

                ALTER TABLE outbox_messages
                    ALTER COLUMN occurred_at TYPE timestamp without time zone USING occurred_at AT TIME ZONE 'UTC',
                    ALTER COLUMN next_retry_at TYPE timestamp without time zone USING next_retry_at AT TIME ZONE 'UTC',
                    ALTER COLUMN processed_at TYPE timestamp without time zone USING processed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN locked_until TYPE timestamp without time zone USING locked_until AT TIME ZONE 'UTC',
                    ALTER COLUMN last_requeued_at TYPE timestamp without time zone USING last_requeued_at AT TIME ZONE 'UTC';

                ALTER TABLE estornos_lancamentos
                    ALTER COLUMN created_at TYPE timestamp without time zone USING created_at AT TIME ZONE 'UTC',
                    ALTER COLUMN processing_started_at TYPE timestamp without time zone USING processing_started_at AT TIME ZONE 'UTC',
                    ALTER COLUMN completed_at TYPE timestamp without time zone USING completed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN rejected_at TYPE timestamp without time zone USING rejected_at AT TIME ZONE 'UTC',
                    ALTER COLUMN failed_at TYPE timestamp without time zone USING failed_at AT TIME ZONE 'UTC';

                ALTER TABLE reprocessamentos_lancamentos
                    ALTER COLUMN created_at TYPE timestamp without time zone USING created_at AT TIME ZONE 'UTC',
                    ALTER COLUMN processing_started_at TYPE timestamp without time zone USING processing_started_at AT TIME ZONE 'UTC',
                    ALTER COLUMN completed_at TYPE timestamp without time zone USING completed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN failed_at TYPE timestamp without time zone USING failed_at AT TIME ZONE 'UTC',
                    ALTER COLUMN rejected_at TYPE timestamp without time zone USING rejected_at AT TIME ZONE 'UTC';
                """);
        }
    }
}
