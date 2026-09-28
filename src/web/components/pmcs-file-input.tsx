"use client";

import { useEffect, useId, useRef } from "react";

interface PmcsFileInputProps {
  readonly label: string;
  readonly accept: string;
  readonly file: File | null;
  readonly disabled?: boolean;
  readonly onFileChange: (file: File | null) => void;
}

export function PmcsFileInput({ label, accept, file, disabled = false, onFileChange }: PmcsFileInputProps) {
  const inputId = useId();
  const statusId = useId();
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!file && input.current) input.current.value = "";
  }, [file]);

  return (
    <div className="pmcs-file-input">
      <label htmlFor={inputId}>{label}</label>
      <div className="pmcs-file-input-row">
        <label className="pmcs-file-input-control" htmlFor={inputId}>
          <span className="pmcs-file-input-button" aria-hidden="true">انتخاب فایل</span>
          <span className="pmcs-file-input-name" id={statusId} aria-live="polite">{file?.name ?? "فایلی انتخاب نشده"}</span>
          <input id={inputId} ref={input} type="file" accept={accept} disabled={disabled}
            aria-label={label} aria-describedby={statusId}
            onChange={(event) => onFileChange(event.target.files?.[0] ?? null)} />
        </label>
        {file && <button type="button" className="pmcs-file-input-clear" disabled={disabled}
          onClick={() => { if (input.current) input.current.value = ""; onFileChange(null); }}>حذف انتخاب</button>}
      </div>
    </div>
  );
}
