import { useEffect, useMemo, useState } from "react";
import { Badge, Button, Card, EmptyState, SkeletonLoader, useToast } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import {
  getPermissionMatrix,
  listRoles,
  updateRolePermissions,
  type AuthRole,
  type PermissionActionKey,
  type PermissionMatrix,
} from "../../services/settingsApi";
import { useI18n } from "../../i18n";

function roleOrder(role: AuthRole) {
  const order = ["owner", "viewer", "purchaser", "accountant"];
  const index = order.indexOf(role.templateKey ?? "");
  return index === -1 ? 99 : index;
}

function sortRoles(roles: AuthRole[]) {
  return [...roles].sort((left, right) => roleOrder(left) - roleOrder(right) || left.name.localeCompare(right.name));
}

function roleDescriptionKey(role: AuthRole | null) {
  switch (role?.templateKey) {
    case "owner":
      return "settings.roles.descriptions.owner";
    case "viewer":
      return "settings.roles.descriptions.viewer";
    case "purchaser":
      return "settings.roles.descriptions.purchaser";
    case "accountant":
      return "settings.roles.descriptions.accountant";
    default:
      return "settings.roles.descriptions.custom";
  }
}

function collectMatrixPermissionKeys(matrix: PermissionMatrix | null) {
  if (!matrix) {
    return [];
  }

  return Array.from(new Set(matrix.rows.flatMap((row) => Object.values(row.permissions).flatMap((permissions) => permissions ?? []))));
}

export function SettingsRolesPage() {
  const { t } = useI18n();
  const { showToast } = useToast();
  const [roles, setRoles] = useState<AuthRole[]>([]);
  const [matrix, setMatrix] = useState<PermissionMatrix | null>(null);
  const [selectedRoleId, setSelectedRoleId] = useState("");
  const [draftPermissions, setDraftPermissions] = useState<Set<string>>(() => new Set());
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [rolesResponse, matrixResponse] = await Promise.all([listRoles(), getPermissionMatrix()]);
        const orderedRoles = sortRoles(rolesResponse);
        if (active) {
          setRoles(orderedRoles);
          setMatrix(matrixResponse);
          const firstRole = orderedRoles[0];
          setSelectedRoleId(firstRole?.id ?? "");
          setDraftPermissions(new Set(firstRole?.permissions ?? []));
          setError("");
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("settings.loadError"));
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

  const selectedRole = roles.find((role) => role.id === selectedRoleId) ?? null;
  const matrixPermissionKeys = useMemo(() => collectMatrixPermissionKeys(matrix), [matrix]);
  const isOwnerRole = selectedRole?.templateKey === "owner";
  const hasChanges = selectedRole ? selectedRole.permissions.slice().sort().join("|") !== Array.from(draftPermissions).sort().join("|") : false;

  function selectRole(role: AuthRole) {
    setSelectedRoleId(role.id);
    setDraftPermissions(new Set(role.permissions));
  }

  function isActionChecked(permissions: string[]) {
    return permissions.length > 0 && permissions.every((permission) => draftPermissions.has(permission));
  }

  function toggleAction(permissions: string[]) {
    if (isOwnerRole || permissions.length === 0) {
      return;
    }

    setDraftPermissions((current) => {
      const next = new Set(current);
      const checked = permissions.every((permission) => next.has(permission));

      for (const permission of permissions) {
        if (checked) {
          next.delete(permission);
        } else {
          next.add(permission);
        }
      }

      return next;
    });
  }

  async function saveRole() {
    if (!selectedRole) {
      return;
    }

    try {
      setSaving(true);
      const matrixPermissions = new Set(matrixPermissionKeys);
      const preservedPermissions = selectedRole.permissions.filter((permission) => !matrixPermissions.has(permission));
      const nextPermissions = Array.from(new Set([...preservedPermissions, ...draftPermissions]));
      const updated = await updateRolePermissions(selectedRole, nextPermissions);
      setRoles((current) => sortRoles(current.map((role) => (role.id === updated.id ? updated : role))));
      setDraftPermissions(new Set(updated.permissions));
      showToast({
        tone: "success",
        title: t("settings.roles.savedTitle"),
        description: t("settings.roles.savedDescription"),
      });
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.saveError"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.saveError"),
      });
    } finally {
      setSaving(false);
    }
  }

  return (
    <SettingsScaffold title="settings.roles.title" description="settings.roles.description">
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
      ) : roles.length === 0 || !matrix ? (
        <Card padding="lg">
          <EmptyState title="settings.roles.emptyTitle" description="settings.roles.emptyDescription" />
        </Card>
      ) : (
        <div className="hc-access-roles-layout">
          <div className="hc-access-role-list" aria-label={t("settings.roles.roleList")}>
            {roles.map((role) => (
              <button
                key={role.id}
                className={`hc-access-role-list__item ${role.id === selectedRoleId ? "hc-access-role-list__item--active" : ""}`}
                type="button"
                onClick={() => selectRole(role)}
              >
                <span className="hc-access-role-list__title">{role.name}</span>
                <span className="hc-access-role-list__meta">
                  {role.isSystemRole ? t("settings.roles.systemRole") : t("settings.roles.customRole")}
                </span>
              </button>
            ))}
          </div>

          <Card className="hc-access-matrix-card" padding="md">
            <div className="hc-access-matrix-card__header">
              <div className="hc-access-matrix-card__copy">
                <div className="hc-access-matrix-card__title-row">
                  <h2 className="hc-access-matrix-card__title">{selectedRole?.name}</h2>
                  {selectedRole?.isProtected ? <Badge tone="primary">settings.roles.protected</Badge> : null}
                  {selectedRole?.isActive ? <Badge tone="success">status.active</Badge> : <Badge tone="warning">status.inactive</Badge>}
                </div>
                <p className="hc-access-matrix-card__description">{t(roleDescriptionKey(selectedRole))}</p>
                {isOwnerRole ? <p className="hc-access-matrix-card__notice">{t("settings.roles.ownerProtectedNotice")}</p> : null}
              </div>
              <Button disabled={!hasChanges || isOwnerRole} isLoading={saving} onClick={saveRole}>
                settings.save
              </Button>
            </div>

            <div className="hc-access-matrix-scroll">
              <table className="hc-access-matrix">
                <thead>
                  <tr>
                    <th scope="col">{t("settings.roles.matrix.module")}</th>
                    {matrix.actions.map((action) => (
                      <th key={action.key} scope="col">{t(action.labelKey)}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {matrix.rows.map((row) => (
                    <tr key={row.key}>
                      <th scope="row">{t(row.labelKey)}</th>
                      {matrix.actions.map((action) => {
                        const permissions = row.permissions[action.key as PermissionActionKey] ?? [];
                        const disabled = isOwnerRole || permissions.length === 0;
                        const checked = isOwnerRole ? permissions.length > 0 : isActionChecked(permissions);

                        return (
                          <td key={action.key}>
                            {permissions.length > 0 ? (
                              <input
                                aria-label={t("settings.roles.matrix.toggle", { module: t(row.labelKey), action: t(action.labelKey) })}
                                checked={checked}
                                className="hc-access-matrix__checkbox"
                                disabled={disabled}
                                type="checkbox"
                                onChange={() => toggleAction(permissions)}
                              />
                            ) : (
                              <span className="hc-access-matrix__empty" aria-hidden="true">-</span>
                            )}
                          </td>
                        );
                      })}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </div>
      )}
    </SettingsScaffold>
  );
}
