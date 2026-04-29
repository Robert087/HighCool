import { useEffect, useState } from "react";
import { Badge, Card, DataTable, EmptyState, SkeletonLoader } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { listInvitations, type Invitation } from "../../services/settingsApi";
import { useI18n } from "../../i18n";

export function SettingsInvitationsPage() {
  const { formatDate } = useI18n();
  const [invitations, setInvitations] = useState<Invitation[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await listInvitations();
        if (active) {
          setInvitations(response);
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
    <SettingsScaffold title="settings.invitations.title" description="settings.invitations.description">
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
          hasData={invitations.length > 0}
          columns={
            <tr>
              <th scope="col">auth.email</th>
              <th scope="col">auth.fullName</th>
              <th scope="col">common.status</th>
              <th scope="col">settings.invitations.columns.expiresAt</th>
              <th scope="col">settings.invitations.columns.roles</th>
            </tr>
          }
          rows={invitations.map((invitation) => (
            <tr key={invitation.id}>
              <td>{invitation.email}</td>
              <td>{invitation.fullName || "settings.notSet"}</td>
              <td><Badge tone={invitation.status === "Pending" ? "warning" : "neutral"}>{`settings.invitations.status.${invitation.status.toLowerCase()}`}</Badge></td>
              <td>{formatDate(invitation.expiresAt, { day: "numeric", month: "short", year: "numeric" })}</td>
              <td>{invitation.roleIds.length}</td>
            </tr>
          ))}
          emptyState={<EmptyState title="settings.invitations.emptyTitle" description="settings.invitations.emptyDescription" />}
        />
      )}
    </SettingsScaffold>
  );
}
