import type { HTMLAttributes, PropsWithChildren } from "react";
import { cn } from "../../lib/cn";

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
  return (
    <span className={cn("hc-badge", `hc-badge--${tone}`, className)} {...props}>
      {children}
    </span>
  );
}
