import type { InputHTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { useI18n } from "../../i18n";

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {}

export function Input({ className, ...props }: InputProps) {
  const { translateText } = useI18n();

  return (
    <input
      className={cn("hc-input", className)}
      aria-label={typeof props["aria-label"] === "string" ? translateText(props["aria-label"]) : props["aria-label"]}
      placeholder={typeof props.placeholder === "string" ? translateText(props.placeholder) : props.placeholder}
      title={typeof props.title === "string" ? translateText(props.title) : props.title}
      {...props}
    />
  );
}
