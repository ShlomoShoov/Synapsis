"""Place names to ISO codes. A name that cannot be mapped with confidence is left unmapped (the record keeps its other
place forms), never guessed: a wrong country is worse than no country."""
from __future__ import annotations

import re
from functools import lru_cache

US_STATES = {
    "Alabama": "AL", "Alaska": "AK", "Arizona": "AZ", "Arkansas": "AR", "California": "CA", "Colorado": "CO",
    "Connecticut": "CT", "Delaware": "DE", "District of Columbia": "DC", "Florida": "FL", "Georgia": "GA",
    "Hawaii": "HI", "Idaho": "ID", "Illinois": "IL", "Indiana": "IN", "Iowa": "IA", "Kansas": "KS", "Kentucky": "KY",
    "Louisiana": "LA", "Maine": "ME", "Maryland": "MD", "Massachusetts": "MA", "Michigan": "MI", "Minnesota": "MN",
    "Mississippi": "MS", "Missouri": "MO", "Montana": "MT", "Nebraska": "NE", "Nevada": "NV", "New Hampshire": "NH",
    "New Jersey": "NJ", "New Mexico": "NM", "New York": "NY", "North Carolina": "NC", "North Dakota": "ND",
    "Ohio": "OH", "Oklahoma": "OK", "Oregon": "OR", "Pennsylvania": "PA", "Rhode Island": "RI",
    "South Carolina": "SC", "South Dakota": "SD", "Tennessee": "TN", "Texas": "TX", "Utah": "UT", "Vermont": "VT",
    "Virginia": "VA", "Washington": "WA", "West Virginia": "WV", "Wisconsin": "WI", "Wyoming": "WY",
    "Puerto Rico": "PR", "Guam": "GU", "Virgin Islands": "VI", "American Samoa": "AS", "Northern Mariana Islands": "MP",
}
_US_BY_ABBR = {v: v for v in US_STATES.values()}
STATES_FOR_MARKETS = [s for s in US_STATES if s not in ("Puerto Rico", "Guam", "Virgin Islands", "American Samoa",
                                                       "Northern Mariana Islands")]


def us_subdivision(name_or_abbr: str | None) -> str | None:
    """'Pennsylvania', 'PA' or 'pa' -> 'US-PA'."""
    if not name_or_abbr:
        return None
    s = str(name_or_abbr).strip()
    code = US_STATES.get(s) or _US_BY_ABBR.get(s.upper())
    return f"US-{code}" if code else None


# Names used by UCDP, GTD and JHU that ISO lookups do not resolve on their own. Former states keep their former
# ISO 3166-3 codes (SU, YU, CS, DD, ZR, YD, RH, NH), which the core vocabulary accepts.
COUNTRY_OVERRIDES = {
    "soviet union": "SU", "russia (soviet union)": "RU", "yugoslavia": "YU", "serbia (yugoslavia)": "RS",
    "czechoslovakia": "CS", "east germany (gdr)": "DD", "west germany (frg)": "DE", "zaire": "ZR",
    "dr congo (zaire)": "CD", "congo (kinshasa)": "CD", "congo (brazzaville)": "CG", "democratic republic of the congo": "CD",
    "republic of the congo": "CG", "people's republic of the congo": "CG", "south yemen": "YD", "north yemen": "YE",
    "yemen (north yemen)": "YE", "rhodesia": "RH", "zimbabwe (rhodesia)": "ZW", "new hebrides": "NH",
    "south vietnam": "VN", "vietnam (north vietnam)": "VN", "cambodia (kampuchea)": "KH", "madagascar (malagasy)": "MG",
    "myanmar (burma)": "MM", "burma": "MM", "ivory coast": "CI", "cote d'ivoire": "CI", "bosnia-herzegovina": "BA",
    "macedonia, fyr": "MK", "macedonia": "MK", "kingdom of eswatini (swaziland)": "SZ", "swaziland": "SZ",
    "korea, south": "KR", "south korea": "KR", "korea, north": "KP", "north korea": "KP", "taiwan*": "TW", "taiwan": "TW",
    "laos": "LA", "russia": "RU", "syria": "SY", "iran": "IR", "bolivia": "BO", "venezuela": "VE", "tanzania": "TZ",
    "moldova": "MD", "brunei": "BN", "vietnam": "VN", "holy see": "VA", "kosovo": "XK", "west bank and gaza": "PS",
    "west bank and gaza strip": "PS", "us": "US", "united states": "US", "east timor": "TL", "timor-leste": "TL",
    "micronesia": "FM", "cape verde": "CV", "cabo verde": "CV", "the gambia": "GM", "gambia": "GM",
    "united kingdom": "GB", "great britain": "GB", "falkland islands": "FK", "hong kong": "HK", "macau": "MO",
    "western sahara": "EH", "slovak republic": "SK", "czech republic": "CZ", "czechia": "CZ", "turkey": "TR",
    "turkiye": "TR", "international": None, "diamond princess": None, "ms zaandam": None, "summer olympics 2020": None,
    "winter olympics 2022": None, "antarctica": "AQ",
}


@lru_cache(maxsize=4096)
def country_iso2(name: str | None) -> str | None:
    """A country name -> ISO 3166-1 alpha-2, or None when not confidently known."""
    if not name:
        return None
    n = re.sub(r"\s+", " ", str(name)).strip()
    low = n.lower()
    if low in COUNTRY_OVERRIDES:
        return COUNTRY_OVERRIDES[low]
    import pycountry
    candidates = [n, re.sub(r"\s*\(.*?\)\s*", " ", n).strip()]
    for c in candidates:
        try:
            return pycountry.countries.lookup(c).alpha_2
        except LookupError:
            pass
    return None
