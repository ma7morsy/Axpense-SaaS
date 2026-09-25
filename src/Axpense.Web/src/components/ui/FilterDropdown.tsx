import { Check, ChevronDown } from "lucide-react";
import { cn } from "../../lib/utils";
import { usePopover } from "./usePopover";

/** Labelled filter dropdown ("Status · Any status") matching the console filter bar. */
export function FilterDropdown({
  label,
  value,
  options,
  onChange,
  allLabel = "All",
}: {
  label: string;
  value: string;
  /** Plain values, or { value, label } pairs when the shown text differs from the value. */
  options: readonly (string | { value: string; label: string })[];
  onChange: (value: string) => void;
  allLabel?: string;
}) {
  const { open, setOpen, pos, wrapRef, triggerRef, popRef } = usePopover("start");
  const opts = options.map((o) => (typeof o === "string" ? { value: o, label: o } : o));
  const isSet = value !== "";
  const shown = isSet ? (opts.find((o) => o.value === value)?.label ?? value) : allLabel;
  const pick = (v: string) => {
    onChange(v);
    setOpen(false);
  };

  return (
    <div ref={wrapRef} className={cn("dd", open && "open", isSet && "set")}>
      <button
        ref={triggerRef}
        type="button"
        className="dd-btn"
        aria-haspopup="listbox"
        aria-expanded={open}
        title={`${label}: ${shown}`}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="dd-t">
          <span className="lb">{label}</span>
          <span className="vl">{shown}</span>
        </span>
        {isSet && (
          <span
            role="button"
            tabIndex={0}
            className="clr"
            title={`Clear ${label}`}
            onClick={(e) => {
              e.stopPropagation();
              pick("");
            }}
          >
            &times;
          </span>
        )}
        <ChevronDown className="cv" />
      </button>
      <div ref={popRef} className="dd-pop" role="listbox" style={{ top: pos.top, left: pos.left }}>
        <div className="dd-list">
          {[{ value: "", label: allLabel }, ...opts].map((o, i) => (
            <div key={o.value || "__all"}>
              {i === 1 && <div className="dd-sep" />}
              <button type="button" role="option" aria-selected={o.value === value} className={cn("dd-op", o.value === value && "on")} onClick={() => pick(o.value)}>
                <Check className="tick" />
                <span className="tx">{o.label}</span>
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
