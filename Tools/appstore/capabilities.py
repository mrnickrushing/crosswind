#!/usr/bin/env python3
"""Turns on Crosswind's App ID capabilities in App Store Connect (run by the Codemagic workflow `crosswind-capabilities`
in the train app).

  python3 Tools/appstore/capabilities.py   # needs APP_STORE_CONNECT_PRIVATE_KEY / _KEY_IDENTIFIER / _ISSUER_ID and PyJWT

  - ICLOUD (key-value storage: the airport's save follows the player's iCloud, Game/Platform/CloudSave.cs)
  - APPLE_ID_AUTH (Sign in with Apple, Game/Platform/AppleAccount.cs; primary App ID)
  - GAME_CENTER (leaderboards and achievements, Game/Platform/GameCenter.cs)

All must be on the bundle id before the App Store profile made at export can carry the entitlements that
Editor/IosCapabilities.cs writes into the Xcode project. Capabilities already on are left alone, so running it
again is safe. Prints the bundle id's capabilities at the end.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iap import call, pages, BUNDLE_ID  # noqa: E402  (same token and retries)

WANT = {
    "ICLOUD": [{"key": "ICLOUD_VERSION", "options": [{"key": "XCODE_6", "enabled": True}]}],
    "APPLE_ID_AUTH": [{"key": "APPLE_ID_AUTH_APP_CONSENT", "options": [{"key": "PRIMARY_APP_CONSENT", "enabled": True}]}],
    "GAME_CENTER": None,
}


def main():
    ids = call("GET", "/v1/bundleIds?filter[identifier]=%s&limit=10" % BUNDLE_ID)["data"]
    bundle = next((b for b in ids if b["attributes"]["identifier"] == BUNDLE_ID), None)
    if bundle is None:
        raise SystemExit("bundle id %s not found" % BUNDLE_ID)
    have = {c["attributes"]["capabilityType"] for c in pages("/v1/bundleIds/%s/bundleIdCapabilities" % bundle["id"])}
    print("bundle id", BUNDLE_ID, bundle["id"], "has:", ", ".join(sorted(have)) or "nothing")
    for cap, settings in WANT.items():
        if cap in have:
            print("  %s already on" % cap)
            continue
        call("POST", "/v1/bundleIdCapabilities", {"data": {
            "type": "bundleIdCapabilities",
            "attributes": {"capabilityType": cap, "settings": settings} if settings else {"capabilityType": cap},
            "relationships": {"bundleId": {"data": {"type": "bundleIds", "id": bundle["id"]}}},
        }})
        print("  %s turned on" % cap)
    have = {c["attributes"]["capabilityType"] for c in pages("/v1/bundleIds/%s/bundleIdCapabilities" % bundle["id"])}
    print("now:", ", ".join(sorted(have)))
    missing = [c for c in WANT if c not in have]
    if missing:
        raise SystemExit("still missing: " + ", ".join(missing))


if __name__ == "__main__":
    main()
