import type { PropsWithChildren, SelectHTMLAttributes } from "react";
import { cn } from "../../lib/cn";
import { localizeReactNode, useI18n } from "../../i18n";

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {}

export function Select({ children, className, ...props }: PropsWithChildren<SelectProps>) {
  const { translateText } = useI18n();

  return (
    <select
      className={cn("hc-select", className)}
      aria-label={typeof props["aria-label"] === "string" ? translateText(props["aria-label"]) : props["aria-label"]}
      title={typeof props.title === "string" ? translateText(props.title) : props.title}
      {...props}
    >
      {localizeReactNode(children, translateText)}
    </select>
  );
}
