import { Fragment } from "react";
import { MoreVertical, type LucideIcon } from "lucide-react";
import { cn } from "../../lib/utils";
import { usePopover } from "./usePopover";

export type RowMenuItem =
  | { label: string; icon: LucideIcon; onSelect: () => void; danger?: boolean }
  | { separator: true };

/** Three-dot row actions menu. Clicks never bubble to the (clickable) table row. */
export function RowMenu({ items, label = "Actions" }: { items: RowMenuItem[]; label?: string }) {
  const { open, setOpen, pos, wrapRef, triggerRef, popRef } = usePopover("end");

  return (
    <div className="row-actions" onClick={(e) => e.stopPropagation()}>
      <div ref={wrapRef} className={cn("menu-wrap", open && "open")}>
        <button
          ref={triggerRef}
          type="button"
          className="act menu-btn"
          aria-label={label}
          title={label}
          aria-haspopup="menu"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          <MoreVertical />
        </button>
        <div ref={popRef} className="menu" role="menu" style={{ top: pos.top, left: pos.left }}>
          {items.map((it, i) =>
            "separator" in it ? (
              <div key={i} className="menu-sep" />
            ) : (
              <Fragment key={i}>
                <button
                  type="button"
                  role="menuitem"
                  className={cn("menu-i", it.danger && "danger")}
                  onClick={() => {
                    setOpen(false);
                    it.onSelect();
                  }}
                >
                  <it.icon />
                  <span>{it.label}</span>
                </button>
              </Fragment>
            )
          )}
        </div>
      </div>
    </div>
  );
}
