#!/usr/bin/env python3
"""Creates Crosswind's Game Center leaderboards and achievements in App Store Connect from docs/appstore/gamecenter.json
(run by the Codemagic workflow `crosswind-gamecenter` in the train app, which holds the App Store Connect key).

  python3 Tools/appstore/gamecenter.py   # needs APP_STORE_CONNECT_PRIVATE_KEY / _KEY_IDENTIFIER / _ISSUER_ID and PyJWT

LISTING_BASE is where docs/appstore/... is read from: this checkout by default, or a URL such as
https://raw.githubusercontent.com/mrnickrushing/crosswind/<branch>/.

It turns Game Center on for the app if it isn't, then for every leaderboard and achievement in the file makes sure
there is: the item (ids as Game/Platform/GameCenter.cs sends them), its first version, an en-US name and text, and
its picture (docs/appstore/gamecenter/*.png, from Tools/art/gc_badges.py). Anything already there is left alone, so
running it again is safe; an item that fails is reported and the rest still run. Uses the v2 Game Center resources
(the v1 ones are deprecated). The items go live with the app version they are submitted with; they work in TestFlight
builds as soon as they exist.
"""
import json
import os
import sys
import urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from iap import BUNDLE_ID, call, pages  # noqa: E402  (same key, same API helpers)

BASE = os.environ.get("LISTING_BASE") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
LOCALE = "en-US"


def read(rel):
    if BASE.startswith("http"):
        with urllib.request.urlopen(BASE.rstrip("/") + "/" + rel) as r:
            return r.read()
    with open(os.path.join(BASE, rel), "rb") as f:
        return f.read()


def upload(kind, loc_id, name, data):
    """Reserves the picture on a localization, sends its parts to Apple's upload URLs and commits it."""
    img = call("POST", "/v2/" + kind, {"data": {
        "type": kind, "attributes": {"fileName": name, "fileSize": len(data)},
        "relationships": {"localization": {"data": {"type": kind.replace("Images", "Localizations"), "id": loc_id}}}}})["data"]
    for op in img["attributes"]["uploadOperations"]:
        req = urllib.request.Request(op["url"], data=data[op["offset"]:op["offset"] + op["length"]], method=op["method"],
                                     headers={h["name"]: h["value"] for h in op.get("requestHeaders", [])})
        with urllib.request.urlopen(req) as r:
            r.read()
    call("PATCH", "/v2/%s/%s" % (kind, img["id"]), {"data": {"type": kind, "id": img["id"], "attributes": {"uploaded": True}}})


def ensure(kind, existing, detail, item, attributes, loc_attributes):
    """One leaderboard or achievement: the item with its first version, the en-US text, the picture."""
    plural = "gameCenter%ss" % kind                       # gameCenterAchievements / gameCenterLeaderboards
    have = existing.get(item["id"])
    if have:
        rid = have["id"]
        print("exists ", item["id"], rid)
    else:
        rid = call("POST", "/v2/" + plural, {
            "data": {"type": plural, "attributes": attributes, "relationships": {
                "gameCenterDetail": {"data": {"type": "gameCenterDetails", "id": detail}},
                "versions": {"data": [{"type": "gameCenter%sVersions" % kind, "id": "${v1}"}]}}},
            "included": [{"type": "gameCenter%sVersions" % kind, "id": "${v1}"}]})["data"]["id"]
        print("created", item["id"], rid)
    versions = pages("/v2/%s/%s/versions?limit=50" % (plural, rid))
    if not versions:
        versions = [call("POST", "/v2/gameCenter%sVersions" % kind, {"data": {
            "type": "gameCenter%sVersions" % kind,
            "relationships": {kind.lower(): {"data": {"type": plural, "id": rid}}}}})["data"]]
        print("  first version")
    version = max(versions, key=lambda v: v["attributes"].get("version") or 0)
    locs = pages("/v2/gameCenter%sVersions/%s/localizations?limit=50" % (kind, version["id"]))
    loc = next((x for x in locs if x["attributes"]["locale"] == LOCALE), None)
    if loc is None:
        attrs = dict(loc_attributes, locale=LOCALE)
        loc = call("POST", "/v2/gameCenter%sLocalizations" % kind, {"data": {
            "type": "gameCenter%sLocalizations" % kind, "attributes": attrs,
            "relationships": {"version": {"data": {"type": "gameCenter%sVersions" % kind, "id": version["id"]}}}}})["data"]
        print("  %s text" % LOCALE)
    image = call("GET", "/v2/gameCenter%sLocalizations/%s/image" % (kind, loc["id"]), ok404=True)
    state = ((image or {}).get("data") or {}).get("attributes", {}).get("assetDeliveryState") or {}
    if image and image.get("data") and state.get("state") in ("FAILED", "AWAITING_UPLOAD"):
        call("DELETE", "/v2/gameCenter%sImages/%s" % (kind, image["data"]["id"]))
        image = None
    if not (image and image.get("data")):
        data = read("docs/appstore/" + item["image"])
        upload("gameCenter%sImages" % kind, loc["id"], os.path.basename(item["image"]), data)
        print("  picture (%d bytes)" % len(data))


def main():
    G = json.loads(read("docs/appstore/gamecenter.json"))
    assert sum(a["points"] for a in G["achievements"]) <= 1000 and all(1 <= a["points"] <= 100 for a in G["achievements"])
    apps = [a for a in call("GET", "/v1/apps?filter[bundleId]=%s" % BUNDLE_ID)["data"] if a["attributes"]["bundleId"] == BUNDLE_ID]
    if not apps:
        raise SystemExit("No App Store Connect app with bundle id " + BUNDLE_ID)
    app = apps[0]["id"]
    print("APPLE_ID", app, apps[0]["attributes"]["name"])
    detail = call("GET", "/v1/apps/%s/gameCenterDetail" % app, ok404=True)
    if detail and detail.get("data"):
        detail = detail["data"]["id"]
        print("Game Center already on", detail)
    else:
        detail = call("POST", "/v1/gameCenterDetails", {"data": {
            "type": "gameCenterDetails",
            "relationships": {"app": {"data": {"type": "apps", "id": app}}}}})["data"]["id"]
        print("Game Center turned on", detail)

    failed = []

    def each(kind, items, existing, attributes, loc_attributes):
        for item in items:
            try:
                ensure(kind, existing, detail, item, attributes(item), loc_attributes(item))
            except SystemExit as e:        # call() stops with the API's answer
                print("FAILED", item["id"], "\n", e)
                failed.append(item["id"])
            except Exception as e:
                print("FAILED", item["id"], "\n", repr(e))
                failed.append(item["id"])

    boards = {b["attributes"]["vendorIdentifier"]: b for b in pages("/v1/gameCenterDetails/%s/gameCenterLeaderboardsV2?limit=200" % detail)}
    each("Leaderboard", G["leaderboards"], boards,
         lambda b: {"referenceName": b["name"], "vendorIdentifier": b["id"], "defaultFormatter": "INTEGER",
                    "scoreSortType": "DESC", "submissionType": "BEST_SCORE"},
         lambda b: {"name": b["name"], "description": b["description"], "formatterSuffix": b["suffix"],
                    "formatterSuffixSingular": b["singular"]})
    achievements = {a["attributes"]["vendorIdentifier"]: a for a in pages("/v1/gameCenterDetails/%s/gameCenterAchievementsV2?limit=200" % detail)}
    each("Achievement", G["achievements"], achievements,
         lambda a: {"referenceName": a["name"], "vendorIdentifier": a["id"], "points": a["points"],
                    "showBeforeEarned": True, "repeatable": False},
         lambda a: {"name": a["name"], "beforeEarnedDescription": a["before"], "afterEarnedDescription": a["after"]})

    print("GAMECENTER", "done" if not failed else "FAILED: " + ", ".join(failed))
    for b in pages("/v1/gameCenterDetails/%s/gameCenterLeaderboardsV2?limit=200" % detail):
        print("BOARD", b["attributes"]["vendorIdentifier"], b["id"])
    print("ACHIEVEMENTS", len(pages("/v1/gameCenterDetails/%s/gameCenterAchievementsV2?limit=200" % detail)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
