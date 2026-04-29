import { useEffect, useState } from "react";
import { Card, DataTable, EmptyState, Field, Input, SkeletonLoader } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { listAuditLog, type AuditLogItem } from "../../services/settingsApi";
import { useI18n } from "../../i18n";

export function SettingsAuditLogPage() {
  const { formatDate } = useI18n();
  const [entries, setEntries] = useState<AuditLogItem[]>([]);
  const [moduleFilter, setModuleFilter] = useState("");
  const [actionFilter, setActionFilter] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await listAuditLog({
          page: 1,
          pageSize: 50,
          module: moduleFilter || undefined,
          action: actionFilter || undefined,
        });
        if (active) {
          setEntries(response);
          setError("");
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "settings.loadError");
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [actionFilter, moduleFilter]);

  return (
    <SettingsScaffold title="settings.audit.title" description="settings.audit.description">
      <Card className="hc-settings-filter-card" padding="md">
        <div className="hc-settings-form__grid">
          <Field label="settings.audit.filters.module">
            <Input value={moduleFilter} onChange={(event) => setModuleFilter(event.target.value)} />
          </Field>
          <Field label="settings.audit.filters.action">
            <Input value={actionFilter} onChange={(event) => setActionFilter(event.target.value)} />
          </Field>
        </div>
      </Card>

      {loading ? (
        <Card padding="lg">
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="3.5rem" />
            <SkeletonLoader variant="rect" height="3.5rem" />
            <SkeletonLoader variant="rect" height="3.5rem" />
          </div>
        </Card>
      ) : error ? (
        <Card padding="lg">
          <EmptyState title="settings.errorTitle" description={error} />
        </Card>
      ) : (
        <DataTable
          hasData={entries.length > 0}
          columns={
            <tr>
              <th scope="col">settings.audit.columns.action</th>
              <th scope="col">settings.audit.columns.module</th>
              <th scope="col">settings.audit.columns.resource</th>
              <th scope="col">settings.audit.columns.createdAt</th>
              <th scope="col">settings.audit.columns.ipAddress</th>
            </tr>
          }
          rows={entries.map((entry) => (
            <tr key={entry.id}>
              <td>{entry.action}</td>
              <td>{entry.module}</td>
              <td>{`${entry.resourceType}${entry.resourceId ? ` · ${entry.resourceId}` : ""}`}</td>
              <td>{formatDate(entry.createdAt, { day: "numeric", month: "short", year: "numeric" })}</td>
              <td>{entry.ipAddress || "settings.notSet"}</td>
            </tr>
          ))}
          emptyState={<EmptyState title="settings.audit.emptyTitle" description="settings.audit.emptyDescription" />}
        />
      )}
    </SettingsScaffold>
  );
}
