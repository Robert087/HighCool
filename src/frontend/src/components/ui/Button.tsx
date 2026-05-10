import type { ButtonHTMLAttributes, PropsWithChildren } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger";
type ButtonSize = "sm" | "md";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  isLoading?: boolean;
}

export function Button({
  children,
  className,
  disabled,
  isLoading = false,
  size = "md",
  type = "button",
  variant = "primary",
  ...props
}: PropsWithChildren<ButtonProps>) {
  const { translateText } = useI18n();

  return (
    <button
      className={cn("hc-button", `hc-button--${variant}`, `hc-button--${size}`, className)}
      disabled={disabled || isLoading}
      type={type}
      aria-label={typeof props["aria-label"] === "string" ? translateText(props["aria-label"]) : props["aria-label"]}
      title={typeof props.title === "string" ? translateText(props.title) : props.title}
      {...props}
    >
      {isLoading ? translateText("app.loading") : localizeReactNode(children, translateText)}
    </button>
  );
}
