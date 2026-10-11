/**
 * What each error code means, in one line, with the rule that gives it. The codes are a contract between the API
 * and its clients (src/dotnet/Synapsis.Sil/Models/Results/ErrorCodes.cs and tests/conformance); Observe shows these
 * lines next to the codes, so a reader does not have to look them up.
 */

/** One error code. */
export interface ErrorCodeInfo {
  /** The rule that checks it, as the API's documentation numbers them. */
  rule: string | null;
  text: string;
}

const codes: Record<string, ErrorCodeInfo> = {
  'E-BUNDLE': { rule: null, text: 'The request body is not a valid bundle.' },
  'E-TOO-LARGE': { rule: null, text: 'More than 5,000 records, or a body over the size limit.' },
  'E-UNAVAILABLE': { rule: null, text: 'The store did not accept the write; send again.' },
  'E-KIND': { rule: 'Rule 1', text: 'The kind is not entity, event, relation or conclusion.' },
  'E-SCHEMA': {
    rule: 'Rule 2',
    text: 'The record’s structure is wrong: a missing, unknown or mistyped field.',
  },
  'E-REGISTRY-SOURCE': {
    rule: 'Rule 3',
    text: 'The source is not declared in any schema package.',
  },
  'E-ID-NAMESPACE': {
    rule: 'Rule 4',
    text: 'The id does not match the kind, or its namespace belongs to another package.',
  },
  'E-REGISTRY-ENTITY-TYPE': { rule: 'Rule 5', text: 'An entity type that is not registered.' },
  'E-REGISTRY-EVENT-TYPE': { rule: 'Rule 5', text: 'An event type that is not registered.' },
  'E-REGISTRY-RELATION-TYPE': { rule: 'Rule 5', text: 'A relation type that is not registered.' },
  'E-REGISTRY-CONCLUSION-TYPE': {
    rule: 'Rule 5',
    text: 'A conclusion type that is not registered.',
  },
  'E-TIME-ALIGN': { rule: 'Rule 6', text: 'The start is not aligned to its precision.' },
  'E-TIME-ORDER': { rule: 'Rule 6', text: 'The end is not after the start.' },
  'E-KNOWN-BEFORE-OCCURRENCE': { rule: 'Rule 7', text: 'known_at is before the record started.' },
  'E-KNOWN-IN-FUTURE': {
    rule: 'Rule 7',
    text: 'known_at is more than 5 minutes after the server received the record.',
  },
  'E-PLACE-MISSING': { rule: 'Rule 8', text: 'The place has none of the four forms.' },
  'E-PLACE-CONFLICT': { rule: 'Rule 8', text: '“unplaced” is combined with another place form.' },
  'E-PLACE-FORM': { rule: 'Rule 8', text: 'A place form the event’s type does not allow.' },
  'E-PLACE-ADMIN': {
    rule: 'Rule 8',
    text: 'An unknown country code, or a subdivision of another country.',
  },
  'E-LOGICAL-SPACE': { rule: 'Rule 8', text: 'A logical space that is not registered.' },
  'E-LOGICAL-DEPTH': { rule: 'Rule 8', text: 'A logical path deeper than its space allows.' },
  'E-LOGICAL-VALUE': { rule: 'Rule 8', text: 'A value outside a closed logical space.' },
  'E-MEASURE': {
    rule: 'Rule 9',
    text: 'A measure the event’s type does not allow, or the same measure twice.',
  },
  'E-UNIT': { rule: 'Rule 9', text: 'A measure in another unit than the registered one.' },
  'E-MEASURE-NEGATIVE': {
    rule: 'Rule 9',
    text: 'A negative value for a measure that is not signed.',
  },
  'E-MEASURE-RANGE': {
    rule: 'Rule 9',
    text: 'A value below the measure’s minimum or above its maximum.',
  },
  'E-TEXT': {
    rule: 'Rule 10',
    text: 'Text where the type carries none, or none where it is required.',
  },
  'E-ATTRIBUTE': {
    rule: 'Rule 11',
    text: 'An undeclared attribute, a value that breaks its declaration, or a required one missing.',
  },
  'E-ID-CONFLICT': {
    rule: 'Rule 13',
    text: 'The same id from the same source twice in one batch, with different content.',
  },
  'E-ROLE': {
    rule: 'Rule 13',
    text: 'A role the type does not allow, or an entity of a type the role does not take.',
  },
  'E-REF-MISSING': {
    rule: 'Rule 13',
    text: 'A referenced record that is neither stored nor earlier in the batch.',
  },
  'E-RELATION-DOMAIN': {
    rule: 'Rule 13',
    text: 'A relation end whose entity type the relation does not allow.',
  },
  'E-PREMISE-MISSING': {
    rule: 'Rule 13',
    text: 'A conclusion premise that is neither stored nor earlier in the batch.',
  },
};

/** What an error code means; a neutral line for codes this client does not know yet. */
export function errorCodeInfo(code: string): ErrorCodeInfo {
  return (
    codes[code] ?? { rule: null, text: 'A code this version of the client does not describe yet.' }
  );
}
