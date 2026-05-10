import { useEffect, useState } from "react";
import { Badge, Card, DataTable, EmptyState, SkeletonLoader } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { listSessions, type SessionItem } from "../../services/settingsApi";
import { useI18n } from "../../i18n";

export function SettingsSessionsPage() {
  const { formatDate } = useI18n();
  const [sessions, setSessions] = useState<SessionItem[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await listSessions();
        if (active) {
          setSessions(response);
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
  }, []);

  return (
    <SettingsScaffold title="settings.sessions.title" description="settings.sessions.description">
      {loading ? (
        <Card padding="lg">
          <div className="hc-skeleton-stack">
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
          hasData={sessions.length > 0}
          columns={
            <tr>
              <th scope="col">settings.sessions.columns.device</th>
              <th scope="col">settings.sessions.columns.browser</th>
              <th scope="col">settings.sessions.columns.ipAddress</th>
              <th scope="col">settings.sessions.columns.expiresAt</th>
              <th scope="col">common.status</th>
            </tr>
          }
          rows={sessions.map((session) => (
            <tr key={session.id}>
              <td>{session.deviceName || "settings.sessions.unknownDevice"}</td>
              <td>{session.browser || "settings.notSet"}</td>
              <td>{session.ipAddress || "settings.notSet"}</td>
              <td>{formatDate(session.expiresAt, { day: "numeric", month: "short", year: "numeric" })}</td>
              <td>
                <div className="hc-table__meta-list">
                  <Badge tone={session.isActive ? "success" : "neutral"}>{session.isActive ? "status.active" : "status.inactive"}</Badge>
                  {session.rememberMe ? <Badge tone="primary">settings.sessions.remembered</Badge> : null}
                </div>
              </td>
            </tr>
          ))}
          emptyState={<EmptyState title="settings.sessions.emptyTitle" description="settings.sessions.emptyDescription" />}
        />
      )}
    </SettingsScaffold>
  );
}
