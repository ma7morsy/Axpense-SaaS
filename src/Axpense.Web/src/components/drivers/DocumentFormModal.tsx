import { useEffect, useRef, useState, type FormEvent } from "react";
import { Camera, FileText } from "lucide-react";
import { Modal } from "../ui/Modal";
import { Button } from "../ui/Button";
import { useToast } from "../ui/Toast";
import { ApiError, driversApi } from "../../lib/api";
import type { DriverDocument } from "../../lib/types";
import { todayPlusDays } from "../../lib/utils";

const MAX_BYTES = 5 * 1024 * 1024;
const ACCEPT = ".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf";

/** Add a document (name, expiry, optional scan) to a driver's file. */
export function DocumentFormModal({
  open,
  driverId,
  driverName,
  onClose,
  onSaved,
}: {
  open: boolean;
  driverId: string;
  driverName: string;
  onClose: () => void;
  onSaved: (doc: DriverDocument) => void;
}) {
  const toast = useToast();
  const inputRef = useRef<HTMLInputElement>(null);
  const [name, setName] = useState("");
  const [expiryDate, setExpiryDate] = useState(todayPlusDays(365));
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName("");
    setExpiryDate(todayPlusDays(365));
    setFile(null);
    setErrors({});
  }, [open]);

  useEffect(() => {
    if (!file || !file.type.startsWith("image/")) {
      setPreview(null);
      return;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  const choose = (f: File | undefined) => {
    if (!f) return;
    const okType = /\.(jpe?g|png|pdf)$/i.test(f.name);
    if (!okType) return setErrors((e) => ({ ...e, file: "Only JPG, PNG or PDF files are allowed." }));
    if (f.size > MAX_BYTES) return setErrors((e) => ({ ...e, file: "The file must be 5 MB or smaller." }));
    setErrors(({ file: _removed, ...rest }) => rest);
    setFile(f);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const missing: Record<string, string> = {};
    if (!name.trim()) missing.name = "Required";
    if (!expiryDate) missing.expiryDate = "Required";
    if (Object.keys(missing).length) {
      setErrors(missing);
      toast("Fill in the highlighted fields to continue", "bad");
      return;
    }
    setSaving(true);
    try {
      const doc = await driversApi.addDocument(driverId, { name: name.trim(), expiryDate, file });
      toast("Document added");
      onSaved(doc);
    } catch (err) {
      if (err instanceof ApiError) setErrors(err.details);
      toast(err instanceof Error ? err.message : "Could not add the document", "bad");
    } finally {
      setSaving(false);
    }
  };

  const bad = (k: string) => (errors[k] ? { borderColor: "var(--red)" } : undefined);
  const errText = (k: string) =>
    errors[k] && errors[k] !== "Required" ? (
      <span className="hint" style={{ color: "var(--red)" }}>
        {errors[k]}
      </span>
    ) : null;

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Add document"
      description={driverName}
      footer={
        <>
          <Button variant="secondary" type="button" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="document-form" disabled={saving}>
            {saving ? "Uploading…" : "Add document"}
          </Button>
        </>
      }
    >
      <form id="document-form" onSubmit={submit} noValidate>
        <div className="form-grid">
          <div className="f full">
            <label htmlFor="doc-name">
              Document name<span className="req"> *</span>
            </label>
            <input
              id="doc-name"
              autoFocus
              value={name}
              placeholder="Defensive driving certificate"
              onChange={(e) => setName(e.target.value)}
              style={bad("name")}
            />
            {errText("name")}
          </div>
          <div className="f">
            <label htmlFor="doc-expiry">
              Expiry date<span className="req"> *</span>
            </label>
            <input id="doc-expiry" type="date" value={expiryDate} onChange={(e) => setExpiryDate(e.target.value)} style={bad("expiryDate")} />
            {errText("expiryDate") ?? <span className="hint">Reminder fires 30 days ahead</span>}
          </div>
          <div className="f full">
            <label>Scan or photo</label>
            <div
              className="drop"
              role="button"
              tabIndex={0}
              style={bad("file")}
              onClick={() => inputRef.current?.click()}
              onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && inputRef.current?.click()}
              onDragOver={(e) => e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault();
                choose(e.dataTransfer.files?.[0]);
              }}
            >
              {preview ? (
                <img src={preview} alt="Preview" />
              ) : file ? (
                <div>
                  <FileText style={{ margin: "0 auto" }} />
                  <div style={{ marginTop: 6 }}>{file.name}</div>
                  <div className="hint">Click to replace</div>
                </div>
              ) : (
                <div>
                  <Camera style={{ margin: "0 auto" }} />
                  <div style={{ marginTop: 6 }}>Click to upload a photo</div>
                  <div className="hint">JPG, PNG or PDF · up to 5 MB</div>
                </div>
              )}
            </div>
            <input ref={inputRef} type="file" accept={ACCEPT} hidden onChange={(e) => choose(e.target.files?.[0])} />
            {errText("file")}
          </div>
        </div>
      </form>
    </Modal>
  );
}
