import type { InputHTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
  label: ReactNode;
  description?: ReactNode;
}

export function Checkbox({ className, description, label, ...props }: CheckboxProps) {
  return (
    <label className={cn("hc-checkbox", className)}>
      <input className="hc-checkbox__input" type="checkbox" {...props} />
      <span className="hc-checkbox__label">
        <span>{label}</span>
        {description ? <span className="hc-checkbox__description">{description}</span> : null}
      </span>
    </label>
  );
}
