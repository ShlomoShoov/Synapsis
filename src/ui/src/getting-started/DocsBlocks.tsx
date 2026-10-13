import { useState, type ReactNode } from 'react';
import styles from './GettingStartedPage.module.css';

export function Callout({ title, children }: { title: string; children: ReactNode }) {
  return (
    <aside className={styles.callout}>
      <strong>{title}</strong>
      <div>{children}</div>
    </aside>
  );
}

export function CodeBlock({ label, code }: { label: string; code: string }) {
  const [copied, setCopied] = useState(false);
  async function copy() {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  }
  return (
    <div className={styles.codeBlock}>
      <div className={styles.codeHead}>
        <span>{label}</span>
        <button type="button" onClick={copy} aria-label={`Copy ${label}`}>
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      <pre>
        <code>{code}</code>
      </pre>
    </div>
  );
}

export function ScreenshotPlaceholder({ caption }: { caption: string }) {
  return (
    <figure className={styles.screenshot}>
      <div>UI screenshot pending</div>
      <figcaption>{caption} · capture from the current running UI pending</figcaption>
    </figure>
  );
}
