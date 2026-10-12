/**
 * What each error code means, in one line. The codes are a contract between the API and its clients
 * (src/dotnet/Synapsis.Api/Services/RecordValidator.cs); Observe shows these lines next to the codes, so a reader does
 * not have to look them up. The feeders' own measure check (E-MEASURE, E-MEASURE-NEGATIVE, E-MEASURE-RANGE) refuses
 * records before sending, so those codes are in the feeders' rejections.jsonl, not in this log.
 */

/** One error code. */
export interface ErrorCodeInfo {
  /** The step that checks it. */
  rule: string | null;
  text: string;
}

const codes: Record<string, ErrorCodeInfo> = {
  'E-JSON': {
    rule: 'Reading',
    text: 'The record cannot be read: not an object, an unknown kind, or a field of the wrong type.',
  },
  'E-MISSING': { rule: 'Required fields', text: 'A required field is missing or empty.' },
  'E-DUPLICATE-ID': {
    rule: 'Same batch',
    text: 'The same id from the same source appears earlier in the batch with other content.',
  },
};

/** What an error code means; a neutral line for codes this client does not know yet. */
export function errorCodeInfo(code: string): ErrorCodeInfo {
  return (
    codes[code] ?? { rule: null, text: 'A code this version of the client does not describe yet.' }
  );
}
