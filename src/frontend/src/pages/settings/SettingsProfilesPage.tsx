import { useEffect, useState } from "react";
import { Card, DataTable, EmptyState, SkeletonLoader } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { listProfiles, type Profile } from "../../services/settingsApi";

export function SettingsProfilesPage() {
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await listProfiles();
        if (active) {
          setProfiles(response);
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
    <SettingsScaffold title="settings.profiles.title" description="settings.profiles.description">
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
          hasData={profiles.length > 0}
          columns={
            <tr>
              <th scope="col">settings.profiles.columns.jobTitle</th>
              <th scope="col">settings.profiles.columns.department</th>
              <th scope="col">settings.profiles.columns.phone</th>
              <th scope="col">settings.profiles.columns.language</th>
              <th scope="col">settings.profiles.columns.defaultBranch</th>
            </tr>
          }
          rows={profiles.map((profile) => (
            <tr key={profile.id}>
              <td>{profile.jobTitle || "settings.notSet"}</td>
              <td>{profile.department || "settings.notSet"}</td>
              <td>{profile.phone || "settings.notSet"}</td>
              <td>{profile.languagePreference.toUpperCase()}</td>
              <td>{profile.defaultBranchCode || "settings.notSet"}</td>
            </tr>
          ))}
          emptyState={<EmptyState title="settings.profiles.emptyTitle" description="settings.profiles.emptyDescription" />}
        />
      )}
    </SettingsScaffold>
  );
}
