import type { HTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";

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
  return (
    <div className={cn("hc-field", className)} {...props}>
      <div className="hc-field__label">
        {label}
        {required ? <span className="hc-field__required"> *</span> : null}
      </div>
      {children}
      {hint ? <div className="hc-field__hint">{hint}</div> : null}
    </div>
  );
}
