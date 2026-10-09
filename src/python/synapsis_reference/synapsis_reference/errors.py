"""Stable error codes and the exception the HTTP layer turns into a JSON error.

The codes are the contract shared with the C# API: both implementations must return the same code for the same
input (the conformance suite in tests/conformance checks this)."""
from __future__ import annotations

# --- record rules, in the order the validator applies them --------------------------------------------------------
E_KIND = "E-KIND"
E_SCHEMA = "E-SCHEMA"
E_REGISTRY_SOURCE = "E-REGISTRY-SOURCE"
E_ID_NAMESPACE = "E-ID-NAMESPACE"
E_REGISTRY_TYPE = {
    "entity": "E-REGISTRY-ENTITY-TYPE",
    "event": "E-REGISTRY-EVENT-TYPE",
    "relation": "E-REGISTRY-RELATION-TYPE",
    "conclusion": "E-REGISTRY-CONCLUSION-TYPE",
}
E_TIME_ALIGN = "E-TIME-ALIGN"
E_TIME_ORDER = "E-TIME-ORDER"
E_KNOWN_BEFORE_OCCURRENCE = "E-KNOWN-BEFORE-OCCURRENCE"
E_KNOWN_IN_FUTURE = "E-KNOWN-IN-FUTURE"
E_PLACE_MISSING = "E-PLACE-MISSING"
E_PLACE_CONFLICT = "E-PLACE-CONFLICT"
E_PLACE_FORM = "E-PLACE-FORM"
E_PLACE_ADMIN = "E-PLACE-ADMIN"
E_LOGICAL_SPACE = "E-LOGICAL-SPACE"
E_LOGICAL_DEPTH = "E-LOGICAL-DEPTH"
E_LOGICAL_VALUE = "E-LOGICAL-VALUE"
E_MEASURE = "E-MEASURE"
E_UNIT = "E-UNIT"
E_MEASURE_NEGATIVE = "E-MEASURE-NEGATIVE"
E_MEASURE_RANGE = "E-MEASURE-RANGE"
E_TEXT = "E-TEXT"
E_ATTRIBUTE = "E-ATTRIBUTE"
E_ROLE = "E-ROLE"
E_REF_MISSING = "E-REF-MISSING"
E_RELATION_DOMAIN = "E-RELATION-DOMAIN"
E_PREMISE_MISSING = "E-PREMISE-MISSING"
E_ID_CONFLICT = "E-ID-CONFLICT"

# --- request-level codes ---------------------------------------------------------------------------------------------
E_BUNDLE = "E-BUNDLE"
E_PARAM = "E-PARAM"
E_TOO_LARGE = "E-TOO-LARGE"
E_AUTH = "E-AUTH"
E_NOT_FOUND = "E-NOT-FOUND"
E_UNAVAILABLE = "E-UNAVAILABLE"

# --- schema package codes --------------------------------------------------------------------------------------------
E_SCHEMA_PACKAGE = "E-SCHEMA-PACKAGE"
E_SCHEMA_REF = "E-SCHEMA-REF"
E_SCHEMA_BREAKING = "E-SCHEMA-BREAKING"
E_SCHEMA_OWNERSHIP = "E-SCHEMA-OWNERSHIP"
E_SCHEMA_VERSION = "E-SCHEMA-VERSION"


class ApiError(Exception):
    """An error the HTTP layer returns as {"code", "message", "details"} with the given status."""

    def __init__(self, status: int, code: str, message: str, details: list | None = None):
        super().__init__(message)
        self.status = status
        self.code = code
        self.message = message
        self.details = details or []

    def body(self) -> dict:
        out = {"code": self.code, "message": self.message}
        if self.details:
            out["details"] = self.details
        return out


class RecordError(Exception):
    """One record broke one rule. The validator stops at the first failure, so a record has at most one."""

    def __init__(self, code: str, path: str, message: str):
        super().__init__(message)
        self.code = code
        self.path = path
        self.message = message
