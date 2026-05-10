import { Children, cloneElement, isValidElement, type ReactNode } from "react";

export function interpolate(template: string, values?: Record<string, string | number | null | undefined>) {
  if (!values) {
    return template;
  }

  return template.replace(/\{(\w+)\}/g, (_, key: string) => String(values[key] ?? ""));
}

export function localizeReactNode(
  node: ReactNode,
  translateText: (value: string) => string,
): ReactNode {
  if (typeof node === "string") {
    return translateText(node);
  }

  if (typeof node === "number" || node == null || typeof node === "boolean") {
    return node;
  }

  if (Array.isArray(node)) {
    return node.map((child, index) => <>{localizeReactNode(child, translateText)}</>);
  }

  if (isValidElement(node)) {
    const localizedChildren = "children" in node.props
      ? Children.map(node.props.children, (child) => localizeReactNode(child, translateText))
      : node.props.children;

    const nextProps: Record<string, unknown> = { children: localizedChildren };

    for (const propName of ["title", "placeholder", "aria-label"]) {
      const propValue = (node.props as Record<string, unknown>)[propName];
      if (typeof propValue === "string") {
        nextProps[propName] = translateText(propValue);
      }
    }

    return cloneElement(node, nextProps);
  }

  return node;
}
