import { useCallback, useMemo, useState } from "react";
import { useI18n } from "../../i18n";
import { Button } from "./Button";

type ConfirmationTone = "default" | "danger" | "warning";

interface ConfirmationDialogProps {
  cancelLabel?: string;
  confirmLabel?: string;
  description: string;
  onCancel: () => void;
  onConfirm: () => void;
  open: boolean;
  title: string;
  tone?: ConfirmationTone;
}

interface ConfirmationOptions {
  cancelLabel?: string;
  confirmLabel?: string;
  description: string;
  title: string;
  tone?: ConfirmationTone;
}

interface PendingConfirmation {
  options: ConfirmationOptions;
  resolve: (value: boolean) => void;
}

function ConfirmationDialog({
  cancelLabel = "common.keepCurrentChanges",
  confirmLabel = "common.confirm",
  description,
  onCancel,
  onConfirm,
  open,
  title,
  tone = "default",
}: ConfirmationDialogProps) {
  const { translateText } = useI18n();

  if (!open) {
    return null;
  }

  const confirmVariant = tone === "danger" ? "danger" : "primary";
  const toneClassName = tone === "danger"
    ? "hc-confirmation-dialog__panel--danger"
    : tone === "warning"
      ? "hc-confirmation-dialog__panel--warning"
      : "";

  return (
    <div className="hc-confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-confirmation-dialog-title">
      <div className={`hc-confirmation-dialog__panel ${toneClassName}`.trim()}>
        <div className="hc-confirmation-dialog__copy">
          <p className="hc-confirmation-dialog__eyebrow">{translateText("dialog.pleaseConfirm")}</p>
          <h2 className="hc-confirmation-dialog__title" id="hc-confirmation-dialog-title">{translateText(title)}</h2>
          <p className="hc-confirmation-dialog__description">{translateText(description)}</p>
        </div>
        <div className="hc-confirmation-dialog__actions">
          <Button variant="ghost" onClick={onCancel} type="button">{translateText(cancelLabel)}</Button>
          <Button variant={confirmVariant} onClick={onConfirm} type="button">{translateText(confirmLabel)}</Button>
        </div>
      </div>
    </div>
  );
}

export function useConfirmationDialog() {
  const [pendingConfirmation, setPendingConfirmation] = useState<PendingConfirmation | null>(null);

  const closeDialog = useCallback((result: boolean) => {
    setPendingConfirmation((current) => {
      current?.resolve(result);
      return null;
    });
  }, []);

  const confirm = useCallback((options: ConfirmationOptions) => {
    return new Promise<boolean>((resolve) => {
      setPendingConfirmation({ options, resolve });
    });
  }, []);

  const dialog = useMemo(() => (
    <ConfirmationDialog
      cancelLabel={pendingConfirmation?.options.cancelLabel}
      confirmLabel={pendingConfirmation?.options.confirmLabel}
      description={pendingConfirmation?.options.description ?? ""}
      onCancel={() => closeDialog(false)}
      onConfirm={() => closeDialog(true)}
      open={pendingConfirmation !== null}
      title={pendingConfirmation?.options.title ?? ""}
      tone={pendingConfirmation?.options.tone}
    />
  ), [closeDialog, pendingConfirmation]);

  return { confirm, dialog };
}
