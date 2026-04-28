import type { InputHTMLAttributes, ReactNode } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "type"> {
  label: ReactNode;
  description?: ReactNode;
}

export function Checkbox({ className, description, label, ...props }: CheckboxProps) {
  const { translateText } = useI18n();

  return (
    <label className={cn("hc-checkbox", className)}>
      <input className="hc-checkbox__input" type="checkbox" {...props} />
      <span className="hc-checkbox__label">
        <span>{localizeReactNode(label, translateText)}</span>
        {description ? <span className="hc-checkbox__description">{localizeReactNode(description, translateText)}</span> : null}
      </span>
    </label>
  );
}
