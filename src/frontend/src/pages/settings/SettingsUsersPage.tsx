import { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  Field,
  FilterDropdown,
  FiltersToolbar,
  FilterTextInput,
  Input,
  OverflowMenu,
  Pagination,
  Select,
  SkeletonLoader,
  type FilterChip,
  useConfirmationDialog,
  useToast,
} from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import {
  activateUser,
  changeUserRoles,
  inviteUser,
  listRoles,
  listUsers,
  suspendUser,
  type AuthRole,
  type UserListItem,
} from "../../services/settingsApi";
import { useI18n } from "../../i18n";

const PAGE_SIZE = 10;
const INITIAL_FILTERS = {
  search: "",
  status: "",
  roleId: "",
};

interface InviteDialogProps {
  error: string;
  form: {
    email: string;
    fullName: string;
    roleId: string;
  };
  loading: boolean;
  open: boolean;
  roles: AuthRole[];
  onCancel: () => void;
  onChange: (field: "email" | "fullName" | "roleId", value: string) => void;
  onSubmit: () => void;
}

function statusTone(status: UserListItem["accessStatus"]) {
  if (status === "Active") return "success" as const;
  if (status === "Suspended") return "warning" as const;
  if (status === "Disabled") return "danger" as const;
  return "primary" as const;
}

function sortRoles(roles: AuthRole[]) {
  const order = ["owner", "viewer", "purchaser", "accountant"];
  return [...roles].sort((left, right) => {
    const leftIndex = order.indexOf(left.templateKey ?? "");
    const rightIndex = order.indexOf(right.templateKey ?? "");
    return (leftIndex === -1 ? 99 : leftIndex) - (rightIndex === -1 ? 99 : rightIndex) || left.name.localeCompare(right.name);
  });
}

function InviteDialog({ error, form, loading, onCancel, onChange, onSubmit, open, roles }: InviteDialogProps) {
  const { t } = useI18n();

  if (!open) {
    return null;
  }

  return (
    <div className="hc-access-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-invite-user-title">
      <button className="hc-access-dialog__backdrop" type="button" aria-label={t("app.close")} onClick={onCancel} />
      <div className="hc-access-dialog__panel">
        <div className="hc-access-dialog__header">
          <div>
            <p className="hc-access-dialog__eyebrow">{t("settings.users.inviteEyebrow")}</p>
            <h2 className="hc-access-dialog__title" id="hc-invite-user-title">{t("settings.users.inviteTitle")}</h2>
          </div>
          <Button size="sm" variant="ghost" onClick={onCancel}>app.close</Button>
        </div>

        <div className="hc-access-dialog__body">
          <Field label="settings.users.fields.email" required>
            <Input
              aria-label="settings.users.fields.email"
              autoComplete="email"
              placeholder="settings.users.placeholders.email"
              type="email"
              value={form.email}
              onChange={(event) => onChange("email", event.target.value)}
            />
          </Field>
          <Field label="settings.users.fields.fullName">
            <Input
              aria-label="settings.users.fields.fullName"
              autoComplete="name"
              placeholder="settings.users.placeholders.fullName"
              value={form.fullName}
              onChange={(event) => onChange("fullName", event.target.value)}
            />
          </Field>
          <Field label="settings.users.fields.role" required>
            <Select
              aria-label="settings.users.fields.role"
              value={form.roleId}
              onChange={(event) => onChange("roleId", event.target.value)}
            >
              <option value="">{t("settings.users.placeholders.role")}</option>
              {sortRoles(roles.filter((role) => role.isActive)).map((role) => (
                <option key={role.id} value={role.id}>{role.name}</option>
              ))}
            </Select>
          </Field>
          {error ? <p className="hc-field-error">{error}</p> : null}
        </div>

        <div className="hc-access-dialog__actions">
          <Button variant="ghost" onClick={onCancel}>app.close</Button>
          <Button isLoading={loading} onClick={onSubmit}>settings.users.inviteSubmit</Button>
        </div>
      </div>
    </div>
  );
}

export function SettingsUsersPage() {
  const { confirm, dialog } = useConfirmationDialog();
  const { formatDate, t } = useI18n();
  const { showToast } = useToast();
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [roles, setRoles] = useState<AuthRole[]>([]);
  const [roleEdits, setRoleEdits] = useState<Record<string, string>>({});
  const [filters, setFilters] = useState(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [reloadKey, setReloadKey] = useState(0);
  const [inviteOpen, setInviteOpen] = useState(false);
  const [inviteLoading, setInviteLoading] = useState(false);
  const [inviteError, setInviteError] = useState("");
  const [inviteForm, setInviteForm] = useState({ email: "", fullName: "", roleId: "" });

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [usersResponse, rolesResponse] = await Promise.all([
          listUsers({
            ...filters,
            page,
            pageSize: PAGE_SIZE,
            sortBy: "name",
            sortDirection: "Asc",
          }),
          listRoles(),
        ]);
        if (active) {
          setUsers(usersResponse.items);
          setRoles(sortRoles(rolesResponse));
          setTotalCount(usersResponse.totalCount);
          setTotalPages(usersResponse.totalPages);
          setError("");
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("settings.loadError"));
          setUsers([]);
          setTotalCount(0);
          setTotalPages(0);
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
  }, [filters, page, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [filters]);

  useEffect(() => {
    setRoleEdits((current) => {
      const next = { ...current };
      for (const user of users) {
        if (user.membershipId && !next[user.membershipId]) {
          next[user.membershipId] = user.roles[0]?.id ?? "";
        }
      }

      return next;
    });
  }, [users]);

  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const selectedRole = roles.find((role) => role.id === filters.roleId);

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("settings.users.filters.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilters((current) => ({ ...current, search: "" })),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("settings.users.filters.statusChip", { value: t(`status.${filters.status.toLowerCase()}`) }),
        onRemove: () => setFilters((current) => ({ ...current, status: "" })),
      });
    }

    if (selectedRole) {
      chips.push({
        key: "role",
        label: t("settings.users.filters.roleChip", { value: selectedRole.name }),
        onRemove: () => setFilters((current) => ({ ...current, roleId: "" })),
      });
    }

    return chips;
  }, [filters, roles]);

  function resetInviteForm() {
    setInviteForm({ email: "", fullName: "", roleId: "" });
    setInviteError("");
  }

  async function submitInvite() {
    if (!inviteForm.email.trim()) {
      setInviteError(t("settings.users.validation.emailRequired"));
      return;
    }

    if (!inviteForm.roleId) {
      setInviteError(t("settings.users.validation.roleRequired"));
      return;
    }

    try {
      setInviteLoading(true);
      setInviteError("");
      await inviteUser({
        email: inviteForm.email.trim(),
        fullName: inviteForm.fullName.trim() || null,
        roleIds: [inviteForm.roleId],
      });
      showToast({
        tone: "success",
        title: t("settings.users.invitedTitle"),
        description: t("settings.users.invitedDescription"),
      });
      setInviteOpen(false);
      resetInviteForm();
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      setInviteError(requestError instanceof ApiError ? requestError.message : t("settings.saveError"));
    } finally {
      setInviteLoading(false);
    }
  }

  async function updateStatus(user: UserListItem, nextStatus: "Active" | "Suspended") {
    if (!user.membershipId) {
      return;
    }

    const confirmed = await confirm({
      title: nextStatus === "Suspended" ? t("settings.users.suspendTitle") : t("settings.users.activateTitle"),
      description: nextStatus === "Suspended" ? t("settings.users.suspendDescription") : t("settings.users.activateDescription"),
      confirmLabel: nextStatus === "Suspended" ? t("settings.users.suspendAction") : t("settings.users.activateAction"),
      cancelLabel: t("app.close"),
      tone: nextStatus === "Suspended" ? "warning" : "default",
    });

    if (!confirmed) {
      return;
    }

    try {
      if (nextStatus === "Suspended") {
        await suspendUser(user.membershipId);
      } else {
        await activateUser(user.membershipId);
      }

      showToast({
        tone: "success",
        title: nextStatus === "Suspended" ? t("settings.users.suspendedTitle") : t("settings.users.activatedTitle"),
      });
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.users.ownerProtectionTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.saveError"),
      });
    }
  }

  async function saveRole(user: UserListItem) {
    if (!user.membershipId) {
      return;
    }

    const roleId = roleEdits[user.membershipId];
    if (!roleId) {
      showToast({
        tone: "warning",
        title: t("settings.users.validation.roleRequired"),
      });
      return;
    }

    try {
      await changeUserRoles(user.membershipId, [roleId]);
      showToast({
        tone: "success",
        title: t("settings.users.roleUpdatedTitle"),
      });
      setReloadKey((current) => current + 1);
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.users.ownerProtectionTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.saveError"),
      });
    }
  }

  const resultLabel = totalCount === 1
    ? t("settings.users.resultLabel.one", { count: totalCount })
    : t("settings.users.resultLabel.other", { count: totalCount });

  return (
    <SettingsScaffold
      title="settings.users.title"
      description="settings.users.description"
      actions={<Button onClick={() => setInviteOpen(true)}>settings.users.inviteButton</Button>}
    >
      <FiltersToolbar
        activeFilters={activeFilters}
        onReset={() => setFilters(INITIAL_FILTERS)}
        primaryFilters={(
          <>
            <FilterDropdown
              aria-label="settings.users.filters.status"
              value={filters.status}
              onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}
            >
              <option value="">{t("settings.users.filters.allStatuses")}</option>
              <option value="Active">{t("status.active")}</option>
              <option value="Invited">{t("status.invited")}</option>
              <option value="Suspended">{t("status.suspended")}</option>
              <option value="Disabled">{t("status.disabled")}</option>
            </FilterDropdown>
            <FilterDropdown
              aria-label="settings.users.filters.role"
              value={filters.roleId}
              onChange={(event) => setFilters((current) => ({ ...current, roleId: event.target.value }))}
            >
              <option value="">{t("settings.users.filters.allRoles")}</option>
              {roles.map((role) => (
                <option key={role.id} value={role.id}>{role.name}</option>
              ))}
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="settings.users.filters.search"
            placeholder="settings.users.filters.searchPlaceholder"
            type="search"
            value={filters.search}
            onChange={(event) => setFilters((current) => ({ ...current, search: event.target.value }))}
          />
        )}
      />

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
          hasData={users.length > 0}
          columns={
            <tr>
              <th scope="col">settings.users.columns.name</th>
              <th scope="col">settings.users.columns.email</th>
              <th scope="col">common.status</th>
              <th scope="col">table.roles</th>
              <th scope="col">settings.users.columns.lastLogin</th>
              <th scope="col">settings.users.columns.createdAt</th>
              <th scope="col" className="hc-table__head-actions">common.actions</th>
            </tr>
          }
          rows={users.map((user) => {
            const roleValue = user.membershipId ? (roleEdits[user.membershipId] ?? user.roles[0]?.id ?? "") : "";
            const roleChanged = Boolean(user.membershipId && roleValue && roleValue !== (user.roles[0]?.id ?? ""));

            return (
              <tr key={user.membershipId ?? user.invitationId ?? user.email}>
                <td>
                  <div className="hc-table__cell-strong">
                    <span className="hc-table__title">{user.fullName || "settings.users.pendingName"}</span>
                    {user.isOwner ? <Badge tone="primary">settings.users.ownerBadge</Badge> : null}
                  </div>
                </td>
                <td>{user.email}</td>
                <td>
                  <div className="hc-table__meta-list">
                    <Badge tone={statusTone(user.accessStatus)}>{`status.${user.accessStatus.toLowerCase()}`}</Badge>
                    {user.invitationStatus ? <Badge tone="neutral">{`settings.invitations.status.${user.invitationStatus.toLowerCase()}`}</Badge> : null}
                    {user.emailVerified ? <Badge tone="success">settings.users.verified</Badge> : null}
                  </div>
                </td>
                <td>
                  {user.membershipId ? (
                    <div className="hc-access-role-edit">
                      <Select
                        aria-label="settings.users.fields.role"
                        value={roleValue}
                        onChange={(event) => {
                          if (!user.membershipId) return;
                          setRoleEdits((current) => ({ ...current, [user.membershipId!]: event.target.value }));
                        }}
                      >
                        {roles.filter((role) => role.isActive).map((role) => (
                          <option key={role.id} value={role.id}>{role.name}</option>
                        ))}
                      </Select>
                      <Button size="sm" variant="secondary" disabled={!roleChanged} onClick={() => saveRole(user)}>
                        settings.users.saveRole
                      </Button>
                    </div>
                  ) : (
                    <span className="hc-table__subtitle">{user.roles.map((role) => role.name).join(", ") || t("settings.notSet")}</span>
                  )}
                </td>
                <td>{user.lastLoginAt ? formatDate(user.lastLoginAt, { day: "numeric", month: "short", year: "numeric" }) : t("settings.users.never")}</td>
                <td>{formatDate(user.createdAt, { day: "numeric", month: "short", year: "numeric" })}</td>
                <td className="hc-table__cell-actions">
                  {user.membershipId ? (
                    <OverflowMenu
                      label="common.actions"
                      items={[
                        {
                          label: "settings.users.suspendAction",
                          disabled: user.accessStatus !== "Active",
                          tone: "danger",
                          onSelect: () => updateStatus(user, "Suspended"),
                        },
                        {
                          label: "settings.users.activateAction",
                          disabled: user.accessStatus !== "Suspended",
                          onSelect: () => updateStatus(user, "Active"),
                        },
                      ]}
                    />
                  ) : (
                    <span className="hc-table__subtitle">settings.users.invitationOnly</span>
                  )}
                </td>
              </tr>
            );
          })}
          footer={<Pagination currentPage={page} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={totalPages} onPageChange={setPage} />}
          emptyState={<EmptyState title="settings.users.emptyTitle" description="settings.users.emptyDescription" />}
        />
      )}

      <InviteDialog
        error={inviteError}
        form={inviteForm}
        loading={inviteLoading}
        open={inviteOpen}
        roles={roles}
        onCancel={() => {
          setInviteOpen(false);
          resetInviteForm();
        }}
        onChange={(field, value) => {
          setInviteForm((current) => ({ ...current, [field]: value }));
          setInviteError("");
        }}
        onSubmit={submitInvite}
      />
      {dialog}
    </SettingsScaffold>
  );
}
