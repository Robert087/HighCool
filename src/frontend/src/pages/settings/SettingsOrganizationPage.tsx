import { useEffect, useState } from "react";
import { Button, Card, Checkbox, EmptyState, Field, Input, SkeletonLoader, useToast } from "../../components/ui";
import { SettingsScaffold } from "./SettingsScaffold";
import { ApiError } from "../../services/api";
import { getOrganizationSettings, updateOrganizationSettings, type OrganizationSettings } from "../../services/settingsApi";

const emptyOrganizationSettings: OrganizationSettings = {
  id: "",
  name: "",
  logo: null,
  address: null,
  phone: null,
  taxId: null,
  commercialRegistry: null,
  defaultCurrency: "EGP",
  timezone: "Africa/Cairo",
  defaultLanguage: "en",
  rtlEnabled: false,
  fiscalYearStartMonth: 1,
  purchaseOrderPrefix: "PO",
  purchaseReceiptPrefix: "PR",
  purchaseReturnPrefix: "RTN",
  paymentPrefix: "PAY",
  defaultWarehouseId: null,
  autoPostDrafts: false,
};

export function SettingsOrganizationPage() {
  const { showToast } = useToast();
  const [form, setForm] = useState<OrganizationSettings>(emptyOrganizationSettings);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const response = await getOrganizationSettings();
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
      const response = await updateOrganizationSettings(form);
      setForm(response);
      setError("");
      showToast({
        tone: "success",
        title: "settings.organization.savedTitle",
        description: "settings.organization.savedDescription",
      });
    } catch (saveError) {
      setError(saveError instanceof ApiError ? saveError.message : "settings.saveError");
    } finally {
      setSaving(false);
    }
  }

  return (
    <SettingsScaffold title="settings.organization.title" description="settings.organization.description">
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
              <Field label="settings.organization.fields.name" required>
                <Input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.phone">
                <Input value={form.phone ?? ""} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value || null }))} />
              </Field>
              <Field label="settings.organization.fields.address">
                <Input value={form.address ?? ""} onChange={(event) => setForm((current) => ({ ...current, address: event.target.value || null }))} />
              </Field>
              <Field label="settings.organization.fields.taxId">
                <Input value={form.taxId ?? ""} onChange={(event) => setForm((current) => ({ ...current, taxId: event.target.value || null }))} />
              </Field>
              <Field label="settings.organization.fields.currency" required>
                <Input value={form.defaultCurrency} onChange={(event) => setForm((current) => ({ ...current, defaultCurrency: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.timezone" required>
                <Input value={form.timezone} onChange={(event) => setForm((current) => ({ ...current, timezone: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.language" required>
                <Input value={form.defaultLanguage} onChange={(event) => setForm((current) => ({ ...current, defaultLanguage: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.fiscalYearStart" required>
                <Input
                  min={1}
                  max={12}
                  type="number"
                  value={form.fiscalYearStartMonth}
                  onChange={(event) => setForm((current) => ({ ...current, fiscalYearStartMonth: Number(event.target.value || 1) }))}
                />
              </Field>
              <Field label="settings.organization.fields.purchaseOrderPrefix" required>
                <Input value={form.purchaseOrderPrefix} onChange={(event) => setForm((current) => ({ ...current, purchaseOrderPrefix: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.purchaseReceiptPrefix" required>
                <Input value={form.purchaseReceiptPrefix} onChange={(event) => setForm((current) => ({ ...current, purchaseReceiptPrefix: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.purchaseReturnPrefix" required>
                <Input value={form.purchaseReturnPrefix} onChange={(event) => setForm((current) => ({ ...current, purchaseReturnPrefix: event.target.value }))} />
              </Field>
              <Field label="settings.organization.fields.paymentPrefix" required>
                <Input value={form.paymentPrefix} onChange={(event) => setForm((current) => ({ ...current, paymentPrefix: event.target.value }))} />
              </Field>
            </div>

            <div className="hc-settings-form__checks">
              <Checkbox
                checked={form.rtlEnabled}
                label="settings.organization.fields.rtlEnabled"
                onChange={(event) => setForm((current) => ({ ...current, rtlEnabled: event.target.checked }))}
              />
              <Checkbox
                checked={form.autoPostDrafts}
                label="settings.organization.fields.autoPostDrafts"
                onChange={(event) => setForm((current) => ({ ...current, autoPostDrafts: event.target.checked }))}
              />
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
