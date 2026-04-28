import type { HTMLAttributes, PropsWithChildren } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

type BadgeTone = "neutral" | "primary" | "success" | "warning" | "danger";

export interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
}

export function Badge({
  children,
  className,
  tone = "neutral",
  ...props
}: PropsWithChildren<BadgeProps>) {
  const { translateText } = useI18n();

  return (
    <span className={cn("hc-badge", `hc-badge--${tone}`, className)} {...props}>
      {localizeReactNode(children, translateText)}
    </span>
  );
}
