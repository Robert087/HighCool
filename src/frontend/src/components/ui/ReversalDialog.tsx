import { useEffect, useState } from "react";
import { useI18n } from "../../i18n";
import { Button } from "./Button";
import { Field } from "./Field";
import { Textarea } from "./Textarea";

interface ReversalDialogProps {
  confirmLabel?: string;
  description: string;
  impactSummary?: string;
  isLoading?: boolean;
  onCancel: () => void;
  onConfirm: (reason: string) => void;
  open: boolean;
  title: string;
}

export function ReversalDialog({
  confirmLabel = "dialog.reverseDocument",
  description,
  impactSummary,
  isLoading = false,
  onCancel,
  onConfirm,
  open,
  title,
}: ReversalDialogProps) {
  const [reason, setReason] = useState("");
  const { translateText } = useI18n();

  useEffect(() => {
    if (!open) {
      setReason("");
    }
  }, [open]);

  if (!open) {
    return null;
  }

  const trimmedReason = reason.trim();

  return (
    <div className="hc-confirmation-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-reversal-dialog-title">
      <div className="hc-confirmation-dialog__panel hc-confirmation-dialog__panel--warning">
        <div className="hc-confirmation-dialog__copy">
          <p className="hc-confirmation-dialog__eyebrow">{translateText("dialog.reversalRequired")}</p>
          <h2 className="hc-confirmation-dialog__title" id="hc-reversal-dialog-title">{translateText(title)}</h2>
          <p className="hc-confirmation-dialog__description">{translateText(description)}</p>
          {impactSummary ? <p className="hc-confirmation-dialog__description">{translateText(impactSummary)}</p> : null}
          <Field label="common.reason" required>
            <Textarea
              placeholder="dialog.reverseReasonPlaceholder"
              rows={4}
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </Field>
        </div>
        <div className="hc-confirmation-dialog__actions">
          <Button variant="ghost" onClick={onCancel} type="button">common.keepDocument</Button>
          <Button
            disabled={trimmedReason.length === 0 || isLoading}
            isLoading={isLoading}
            onClick={() => onConfirm(trimmedReason)}
            type="button"
          >
            {translateText(confirmLabel)}
          </Button>
        </div>
      </div>
    </div>
  );
}
