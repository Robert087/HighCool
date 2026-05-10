import { useEffect, useState } from "react";
import { Button, Card, Checkbox, EmptyState, Field, Input, SkeletonLoader, useToast } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { getSecuritySettings, updateSecuritySettings, type SecuritySettings } from "../../services/settingsApi";

const emptySecuritySettings: SecuritySettings = {
  minimumPasswordLength: 8,
  requireUppercase: true,
  requireLowercase: true,
  requireNumber: true,
  requireSymbol: true,
  sessionTimeoutMinutes: 480,
  forceTwoFactor: false,
  inviteExpiryDays: 7,
  allowedEmailDomains: null,
  loginAttemptLimit: 5,
  auditRetentionDays: 365,
  enableEmailOtp: false,
};

export function SettingsSecurityPage() {
  const { showToast } = useToast();
  const [form, setForm] = useState<SecuritySettings>(emptySecuritySettings);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await getSecuritySettings();
        if (active) {
          setForm(response);
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

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    try {
      setSaving(true);
      const response = await updateSecuritySettings(form);
      setForm(response);
      setError("");
      showToast({
        tone: "success",
        title: "settings.security.savedTitle",
        description: "settings.security.savedDescription",
      });
    } catch (saveError) {
      setError(saveError instanceof ApiError ? saveError.message : "settings.saveError");
    } finally {
      setSaving(false);
    }
  }

  return (
    <SettingsScaffold title="settings.security.title" description="settings.security.description">
      <Card padding="lg">
        {loading ? (
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="4rem" />
          </div>
        ) : error ? (
          <EmptyState title="settings.errorTitle" description={error} />
        ) : (
          <form className="hc-settings-form" onSubmit={handleSubmit}>
            <div className="hc-settings-form__grid">
              <Field label="settings.security.fields.minimumPasswordLength">
                <Input min={6} type="number" value={form.minimumPasswordLength} onChange={(event) => setForm((current) => ({ ...current, minimumPasswordLength: Number(event.target.value || 8) }))} />
              </Field>
              <Field label="settings.security.fields.sessionTimeoutMinutes">
                <Input min={15} type="number" value={form.sessionTimeoutMinutes} onChange={(event) => setForm((current) => ({ ...current, sessionTimeoutMinutes: Number(event.target.value || 480) }))} />
              </Field>
              <Field label="settings.security.fields.inviteExpiryDays">
                <Input min={1} type="number" value={form.inviteExpiryDays} onChange={(event) => setForm((current) => ({ ...current, inviteExpiryDays: Number(event.target.value || 7) }))} />
              </Field>
              <Field label="settings.security.fields.loginAttemptLimit">
                <Input min={1} type="number" value={form.loginAttemptLimit} onChange={(event) => setForm((current) => ({ ...current, loginAttemptLimit: Number(event.target.value || 5) }))} />
              </Field>
              <Field label="settings.security.fields.auditRetentionDays">
                <Input min={30} type="number" value={form.auditRetentionDays} onChange={(event) => setForm((current) => ({ ...current, auditRetentionDays: Number(event.target.value || 365) }))} />
              </Field>
              <Field label="settings.security.fields.allowedEmailDomains">
                <Input value={form.allowedEmailDomains ?? ""} onChange={(event) => setForm((current) => ({ ...current, allowedEmailDomains: event.target.value || null }))} />
              </Field>
            </div>

            <div className="hc-settings-form__checks">
              <Checkbox checked={form.requireUppercase} label="settings.security.fields.requireUppercase" onChange={(event) => setForm((current) => ({ ...current, requireUppercase: event.target.checked }))} />
              <Checkbox checked={form.requireLowercase} label="settings.security.fields.requireLowercase" onChange={(event) => setForm((current) => ({ ...current, requireLowercase: event.target.checked }))} />
              <Checkbox checked={form.requireNumber} label="settings.security.fields.requireNumber" onChange={(event) => setForm((current) => ({ ...current, requireNumber: event.target.checked }))} />
              <Checkbox checked={form.requireSymbol} label="settings.security.fields.requireSymbol" onChange={(event) => setForm((current) => ({ ...current, requireSymbol: event.target.checked }))} />
              <Checkbox checked={form.forceTwoFactor} label="settings.security.fields.forceTwoFactor" onChange={(event) => setForm((current) => ({ ...current, forceTwoFactor: event.target.checked }))} />
              <Checkbox checked={form.enableEmailOtp} label="settings.security.fields.enableEmailOtp" onChange={(event) => setForm((current) => ({ ...current, enableEmailOtp: event.target.checked }))} />
            </div>

            <div className="hc-settings-form__actions">
              <Button isLoading={saving} type="submit">settings.save</Button>
            </div>
          </form>
        )}
      </Card>
    </SettingsScaffold>
  );
}
