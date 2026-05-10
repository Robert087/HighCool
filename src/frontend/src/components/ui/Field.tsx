import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

export interface FieldProps extends HTMLAttributes<HTMLDivElement> {
  label: ReactNode;
  hint?: ReactNode;
  required?: boolean;
}

export function Field({
  children,
  className,
  hint,
  label,
  required = false,
  ...props
}: FieldProps) {
  const { translateText } = useI18n();

  return (
    <div className={cn("hc-field", className)} {...props}>
      <div className="hc-field__label">
        {localizeReactNode(label, translateText)}
        {required ? <span className="hc-field__required"> *</span> : null}
      </div>
      {children}
      {hint ? <div className="hc-field__hint">{localizeReactNode(hint, translateText)}</div> : null}
    </div>
  );
}
