import {
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent as ReactKeyboardEvent,
  type ReactElement,
} from "react";
import { createPortal } from "react-dom";
import { Link } from "react-router-dom";
import { cn } from "../../lib/cn";
import { useI18n } from "../../i18n";

export interface OverflowMenuItem {
  label: string;
  to?: string;
  onSelect?: () => void;
  tone?: "default" | "danger";
  disabled?: boolean;
}

interface OverflowMenuProps {
  items: OverflowMenuItem[];
  label?: string;
}

interface MenuPosition {
  top: number;
  left: number;
  minWidth: number;
  transformOrigin: "top right" | "bottom right";
}

const VIEWPORT_MARGIN = 12;

function isFocusable(target: EventTarget | null) {
  return target instanceof HTMLElement;
}

export function OverflowMenu({ items, label = "More" }: OverflowMenuProps) {
  const { translateText } = useI18n();
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState<MenuPosition | null>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const menuRef = useRef<HTMLDivElement | null>(null);
  const itemRefs = useRef<Array<HTMLElement | null>>([]);
  const menuId = useId();
  const enabledItems = useMemo(
    () => items.map((item, index) => ({ item, index })).filter(({ item }) => !item.disabled),
    [items],
  );

  useLayoutEffect(() => {
    if (!open || !triggerRef.current || !menuRef.current) {
      return;
    }

    function updatePosition() {
      if (!triggerRef.current || !menuRef.current) {
        return;
      }

      const triggerRect = triggerRef.current.getBoundingClientRect();
      const menuRect = menuRef.current.getBoundingClientRect();
      const spaceBelow = window.innerHeight - triggerRect.bottom - VIEWPORT_MARGIN;
      const spaceAbove = triggerRect.top - VIEWPORT_MARGIN;
      const shouldOpenUp = spaceBelow < menuRect.height && spaceAbove > spaceBelow;
      const top = shouldOpenUp
        ? Math.max(VIEWPORT_MARGIN, triggerRect.top - menuRect.height - 6)
        : Math.min(window.innerHeight - menuRect.height - VIEWPORT_MARGIN, triggerRect.bottom + 6);
      const left = Math.max(
        VIEWPORT_MARGIN,
        Math.min(triggerRect.right - menuRect.width, window.innerWidth - menuRect.width - VIEWPORT_MARGIN),
      );

      setPosition({
        top,
        left,
        minWidth: Math.max(triggerRect.width, 160),
        transformOrigin: shouldOpenUp ? "bottom right" : "top right",
      });
    }

    updatePosition();

    window.addEventListener("resize", updatePosition);
    window.addEventListener("scroll", updatePosition, true);

    return () => {
      window.removeEventListener("resize", updatePosition);
      window.removeEventListener("scroll", updatePosition, true);
    };
  }, [open]);

  useEffect(() => {
    if (!open) {
      return;
    }

    const firstEnabledIndex = enabledItems[0]?.index;
    if (typeof firstEnabledIndex === "number") {
      itemRefs.current[firstEnabledIndex]?.focus();
    } else {
      menuRef.current?.focus();
    }

    function handlePointerDown(event: PointerEvent) {
      const target = event.target;
      if (
        isFocusable(target)
        && !menuRef.current?.contains(target)
        && !triggerRef.current?.contains(target)
      ) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        event.preventDefault();
        setOpen(false);
        triggerRef.current?.focus();
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    window.addEventListener("keydown", handleKeyDown);

    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [enabledItems, open]);

  useEffect(() => {
    if (!open) {
      itemRefs.current = [];
    }
  }, [open]);

  if (items.length === 0) {
    return null;
  }

  function moveFocus(currentIndex: number, direction: 1 | -1) {
    if (enabledItems.length === 0) {
      return;
    }

    const enabledPosition = enabledItems.findIndex(({ index }) => index === currentIndex);
    const nextPosition = enabledPosition === -1
      ? 0
      : (enabledPosition + direction + enabledItems.length) % enabledItems.length;
    const nextIndex = enabledItems[nextPosition]?.index;

    if (typeof nextIndex === "number") {
      itemRefs.current[nextIndex]?.focus();
    }
  }

  function handleTriggerKeyDown(event: ReactKeyboardEvent<HTMLButtonElement>) {
    if (event.key === "ArrowDown" || event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      setOpen(true);
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      setOpen(true);

      requestAnimationFrame(() => {
        const lastEnabledIndex = enabledItems[enabledItems.length - 1]?.index;
        if (typeof lastEnabledIndex === "number") {
          itemRefs.current[lastEnabledIndex]?.focus();
        }
      });
    }
  }

  function handleMenuKeyDown(event: ReactKeyboardEvent<HTMLDivElement>) {
    const target = event.target;
    const currentIndex = isFocusable(target) ? Number(target.dataset.menuIndex ?? "-1") : -1;

    if (event.key === "Tab") {
      event.preventDefault();
      moveFocus(currentIndex, event.shiftKey ? -1 : 1);
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      moveFocus(currentIndex, 1);
      return;
    }

    if (event.key === "ArrowUp") {
      event.preventDefault();
      moveFocus(currentIndex, -1);
      return;
    }

    if (event.key === "Home") {
      event.preventDefault();
      const firstEnabledIndex = enabledItems[0]?.index;
      if (typeof firstEnabledIndex === "number") {
        itemRefs.current[firstEnabledIndex]?.focus();
      }
      return;
    }

    if (event.key === "End") {
      event.preventDefault();
      const lastEnabledIndex = enabledItems[enabledItems.length - 1]?.index;
      if (typeof lastEnabledIndex === "number") {
        itemRefs.current[lastEnabledIndex]?.focus();
      }
    }
  }

  function renderItem(item: OverflowMenuItem, index: number): ReactElement {
    const className = cn(
      "hc-overflow-menu__item",
      item.tone === "danger" ? "hc-overflow-menu__item--danger" : undefined,
    );

    if (item.to) {
      return (
        <Link
          key={`${item.label}-${item.to}`}
          ref={(node) => {
            itemRefs.current[index] = node;
          }}
          aria-disabled={item.disabled ? "true" : undefined}
          className={className}
          data-menu-index={index}
          role="menuitem"
          tabIndex={item.disabled ? -1 : 0}
          to={item.disabled ? "#" : item.to}
          onClick={(event) => {
            if (item.disabled) {
              event.preventDefault();
              return;
            }

            setOpen(false);
          }}
        >
          {translateText(item.label)}
        </Link>
      );
    }

    return (
      <button
        key={item.label}
        ref={(node) => {
          itemRefs.current[index] = node;
        }}
        className={className}
        data-menu-index={index}
        disabled={item.disabled}
        role="menuitem"
        tabIndex={item.disabled ? -1 : 0}
        type="button"
        onClick={() => {
          item.onSelect?.();
          setOpen(false);
        }}
      >
        {translateText(item.label)}
      </button>
    );
  }

  return (
    <>
      <button
        ref={triggerRef}
        aria-controls={open ? menuId : undefined}
        aria-expanded={open}
        aria-haspopup="menu"
        aria-label={translateText(label)}
        className="hc-button hc-button--ghost hc-button--sm hc-overflow-menu__trigger"
        type="button"
        onClick={() => setOpen((current) => !current)}
        onKeyDown={handleTriggerKeyDown}
      >
        {translateText(label)}
      </button>

      {open ? createPortal(
        <div
          ref={menuRef}
          id={menuId}
          className="hc-overflow-menu__panel"
          role="menu"
          style={{
            top: position?.top ?? -9999,
            left: position?.left ?? -9999,
            minWidth: position?.minWidth ?? 160,
            transformOrigin: position?.transformOrigin ?? "top right",
            visibility: position ? "visible" : "hidden",
          }}
          tabIndex={-1}
          onKeyDown={handleMenuKeyDown}
        >
          {items.map(renderItem)}
        </div>,
        document.body,
      ) : null}
    </>
  );
}
